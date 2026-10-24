# Knockback Arena character v2 ("CubeCritter"): one skinned mesh, face as geometry islands + blend shapes.
# Run inside Blender:  exec(open(".../build_v2.py").read()); build()
# Conventions: Z up, front = -Y, character's left = +X (.L), 1 unit = 1 m, pivot on the ground.
import bpy, bmesh, math
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

P = dict(
    N=20,                 # quads per cube-face edge on the body
    body_w=1.00, body_d=0.92, body_h=0.94, body_bottom=0.10,   # W:H:D = 1 : 0.94 : 0.92
    p=3.8,                # superellipsoid exponent (2 = ellipsoid, inf = box)
    taper=0.05,           # bottom slightly wider than top
    # face sits low (baby schema): heights from body bottom as fractions of H
    eye_x=0.195, eye_z=0.10 + 0.46 * 0.94, eye_a=0.062, eye_b=0.104, eye_h=0.024,
    mouth_z=0.10 + 0.29 * 0.94, mouth_w=0.058,
    cheek_x=0.265, cheek_z=0.10 + 0.31 * 0.94, cheek_r=0.125,
    eps=0.0015,           # gap between face islands and the body surface
    # limbs and hooks
    arm_z=0.10 + 0.47 * 0.94, arm_y=0.02, arm_len=0.29, arm_r=0.088, arm_splay=24,
    foot_x=0.225, foot_y=-0.06, foot_size=(0.28, 0.38, 0.16),
    curl_x=0.07, curl_y=0.07,
    emblem_z=0.10 + 0.55 * 0.94, emblem_size=0.40,
)

MATS = ["Body", "Eye", "EyeShine", "Mouth", "Tongue", "Blush", "Feet", "Emblem"]
M = {n: i for i, n in enumerate(MATS)}

# ---------------------------------------------------------------- body
def body_point(d):
    """Direction -> point on the tapered superellipsoid body (object space, origin at ground)."""
    a, b, c = P["body_w"] / 2, P["body_d"] / 2, P["body_h"] / 2
    p = P["p"]
    s = (abs(d.x / a) ** p + abs(d.y / b) ** p + abs(d.z / c) ** p) ** (-1 / p)
    v = d * s
    zn = (v.z + c) / (2 * c)                      # 0 bottom .. 1 top
    k = 1 + P["taper"] * (0.5 - zn)
    return Vector((v.x * k, v.y * k, v.z + c + P["body_bottom"]))

def add_body(bm, uv):
    N = P["N"]; verts = {}; faces = []
    def key(v): return tuple(round(x, 5) for x in v)
    tiles = [(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)]
    for fi, (axis, sign) in enumerate([(a, s) for a in range(3) for s in (-1, 1)]):
        grid = []
        for i in range(N + 1):
            row = []
            for j in range(N + 1):
                u = math.tan((i / N * 2 - 1) * math.pi / 4)
                v = math.tan((j / N * 2 - 1) * math.pi / 4)
                co = [0, 0, 0]; co[axis] = sign; co[(axis + 1) % 3] = u; co[(axis + 2) % 3] = v
                k = key(co)
                if k not in verts:
                    verts[k] = bm.verts.new(body_point(Vector(co).normalized()))
                row.append((verts[k], (i / N, j / N)))
            grid.append(row)
        tx, ty = tiles[fi]
        for i in range(N):
            for j in range(N):
                q = [grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]]
                if sign < 0: q.reverse()
                f = bm.faces.new([x[0] for x in q]); f.material_index = M["Body"]; f.smooth = True
                for loop, (_, (a, b)) in zip(f.loops, q):
                    loop[uv].uv = ((tx + 0.02 + a * 0.96) / 3, (ty + 0.02 + b * 0.96) / 2)
                faces.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return list(verts.values())

# ---------------------------------------------------------------- 2D face shapes
# Every island is an (ns+1) x (nt+1) grid over (s, t) in [-1, 1]^2.
# A shape maps (s, t) -> (x, z, h): position on the front plane + height above the body surface.

def disc(cx, cz, a, b, H):
    def f(s, t):
        u = s * math.sqrt(max(0.0, 1 - t * t / 2)); v = t * math.sqrt(max(0.0, 1 - s * s / 2))
        return cx + a * u, cz + b * v, H * math.sqrt(max(0.0, 1 - u * u - v * v)) ** 0.7
    return f

def band(path, half_w, H, cap=6):
    """Stroke of half-width half_w along path(s)->(x,z), rounded caps, s in [-1,1]."""
    def f(s, t):
        x, z = path(s)
        e = 1e-3
        x1, z1 = path(min(1, s + e)); x0, z0 = path(max(-1, s - e))
        tx, tz = x1 - x0, z1 - z0; L = math.hypot(tx, tz) or 1
        nx, nz = -tz / L, tx / L
        w = half_w * max(0.0, 1 - abs(s) ** cap) ** 0.5
        return x + nx * w * t, z + nz * w * t, H * math.sqrt(max(0.0, 1 - t * t))
    return f

def collapsed(cx, cz):
    return lambda s, t: (cx, cz, -0.012)          # hidden just inside the body

def eye_shapes(side):
    cx, cz, a, b, H = side * P["eye_x"], P["eye_z"], P["eye_a"], P["eye_b"], P["eye_h"]
    base = disc(cx, cz, a, b, H)
    def blink(s, t):   # soft U-shaped closed lid line
        return band(lambda q: (cx + 1.15 * a * q, cz - 0.30 * b - 0.020 * (1 - q * q)), 0.0105, 0.006)(s, t)
    def happy(s, t):   # ^ arc
        return band(lambda q: (cx + 1.25 * a * q, cz - 0.15 * b + 0.055 * (1 - q * q) ** 0.8), 0.014, 0.008)(s, t)
    def angry(s, t):   # top-inner part pressed down like a frown brow
        x, z, h = base(s, t)
        inner = -side * (x - cx) / a              # +1 toward the nose
        zl = cz + b * (0.62 - 0.55 * inner)
        if z > zl: z = zl + (z - zl) * 0.12
        return x, z, h
    def wide(s, t):
        x, z, h = base(s, t)
        return cx + (x - cx) * 1.18, cz + (z - cz) * 1.12, h * 1.1
    return dict(Basis=base, Blink=blink, EyesHappy=happy, EyesAngry=angry, EyesWide=wide,
                EyesHurt=collapsed(cx, cz))

def shine_shapes(side, which):
    """Catchlights sit on the eye dome and follow the eye's keys; they shrink into the closed-eye stroke."""
    cx, cz, a, b, H = side * P["eye_x"], P["eye_z"], P["eye_a"], P["eye_b"], P["eye_h"]
    ox, oz, r = (0.38 * a, 0.40 * b, 0.30 * a) if which == 0 else (-0.30 * a, -0.40 * b, 0.13 * a)
    def on_eye(sx=1.0, sz=1.0, sh=1.0, dz=0.0):
        def f(s, t):
            x, z, _ = disc(cx + ox, cz + oz + dz, r, r * 1.15, 0)(s, t)
            x, z = cx + (x - cx) * sx, cz + (z - cz) * sz
            u, v = (x - cx) / (a * sx), (z - cz) / (b * sz)
            return x, z, sh * H * max(0.0, 1 - u * u - v * v) ** 0.35 + 0.0012
        return f
    def into(x, z):  # shrink to a point just under a stroke's surface
        return lambda s, t: (x, z, 0.001)
    return dict(Basis=on_eye(), EyesAngry=on_eye(dz=-0.18 * b if which == 0 else 0.0),
                EyesWide=on_eye(1.18, 1.12, 1.1),
                Blink=into(cx, cz - 0.30 * b - 0.020), EyesHappy=into(cx, cz - 0.15 * b + 0.055),
                EyesHurt=into(cx + side * 0.6 * a, cz))

def hurt_shapes(side):
    """> < squeezed eyes, hidden until EyesHurt."""
    cx, cz, a, b = side * P["eye_x"], P["eye_z"], P["eye_a"], P["eye_b"]
    W, Hh = 1.05 * a, 0.62 * b
    def path(q):
        r = math.sqrt(q * q + 0.02)               # rounded apex
        return cx + side * W * (1.45 * r - 0.75), cz + Hh * q
    shape = band(path, 0.0125, 0.008)
    return dict(Basis=collapsed(cx, cz), EyesHurt=shape)

def mouth_shapes():
    mx, mz, W = 0.0, P["mouth_z"], P["mouth_w"]
    smile = band(lambda q: (mx + W * q, mz - 0.016 * (1 - q * q)), 0.0085, 0.004)
    def open_d(s, t):   # D-shaped open smile
        top = mz + 0.006 - 0.006 * s * s
        bot = mz - 0.060 * math.sqrt(max(0.0, 1 - s * s)) ** 0.9 - 0.004
        k = (t + 1) / 2
        return mx + 1.12 * W * s * (1 - 0.08 * (1 - k)), bot + (top - bot) * k, 0.003
    def ring(r, ry, oz):  # filled O
        f = disc(mx, mz + oz, r, ry, 0.003)
        return f
    def wmouth(s, t):
        return band(lambda q: (mx + 1.05 * W * q, mz + 0.004 - 0.020 * abs(math.sin(math.pi * q)) ** 0.85), 0.0085, 0.004)(s, t)
    frown = band(lambda q: (mx + 0.9 * W * q, mz - 0.012 + 0.014 * (1 - q * q)), 0.0085, 0.004)
    return dict(Basis=smile, MouthOpen=open_d, MouthO=ring(0.050, 0.058, -0.014),
                MouthSmallO=ring(0.016, 0.018, -0.002), MouthW=wmouth, MouthFrown=frown)

def tongue_shapes():
    mx, mz, W = 0.0, P["mouth_z"], P["mouth_w"]
    tongue_d = disc(mx, mz - 0.044, 0.70 * W, 0.024, 0.0045)
    tongue_o = disc(mx, mz - 0.014 - 0.038, 0.032, 0.016, 0.0045)
    hide = lambda s, t: (mx, mz - 0.03, -0.012)
    return dict(Basis=hide, MouthOpen=tongue_d, MouthO=tongue_o)

def blush_shapes(side):
    cx, cz, r = side * P["cheek_x"], P["cheek_z"], P["cheek_r"]
    base = disc(cx, cz, 0.080, 0.040, 0.0025)
    def big(s, t):
        x, z, h = base(s, t); return cx + (x - cx) * 1.15, cz + (z - cz) * 1.15, h
    return dict(Basis=base, CheekPuff=big)

ISLANDS = [  # (name, shapes fn, material, ns, nt)
    ("Eye.L", lambda: eye_shapes(+1), "Eye", 16, 16), ("Eye.R", lambda: eye_shapes(-1), "Eye", 16, 16),
    ("Shine0.L", lambda: shine_shapes(+1, 0), "EyeShine", 6, 6), ("Shine0.R", lambda: shine_shapes(-1, 0), "EyeShine", 6, 6),
    ("Shine1.L", lambda: shine_shapes(+1, 1), "EyeShine", 4, 4), ("Shine1.R", lambda: shine_shapes(-1, 1), "EyeShine", 4, 4),
    ("Hurt.L", lambda: hurt_shapes(+1), "Eye", 20, 4), ("Hurt.R", lambda: hurt_shapes(-1), "Eye", 20, 4),
    ("Mouth", mouth_shapes, "Mouth", 20, 8), ("Tongue", tongue_shapes, "Tongue", 8, 8),
    ("Blush.L", lambda: blush_shapes(+1), "Blush", 8, 8), ("Blush.R", lambda: blush_shapes(-1), "Blush", 8, 8),
]

# ---------------------------------------------------------------- body deformation keys
def cheek_field(co, amt=1.0):
    """CheekPuff displacement: two broad cheek balloons + a slight forward pucker at the mouth."""
    d = Vector((0, 0, 0))
    if co.y >= 0: return d
    front = min(1.0, -co.y / (P["body_d"] / 2)) ** 3          # fade out around the sides
    for side in (+1, -1):
        dx, dz = (co.x - side * P["cheek_x"]) / 0.19, (co.z - P["cheek_z"]) / 0.17
        r2 = dx * dx + dz * dz
        if r2 < 1:
            d += Vector((side * 0.30, -1.0, -0.10)).normalized() * 0.055 * (1 - r2) ** 2 * front
    dx, dz = co.x / 0.10, (co.z - P["mouth_z"]) / 0.08
    r2 = dx * dx + dz * dz
    if r2 < 1:
        d += Vector((0, -0.016, 0)) * (1 - r2) ** 2 * front
    return d * amt

BODY_KEYS = {"CheekPuff": cheek_field}


# ---------------------------------------------------------------- rigid parts (arms, feet, curl) + back emblem
def add_grid_solid(bm, uv, fn, nu, nv, mat, closed_u=True):
    """Quad surface over (u in [0,1) around, v in [0,1] along); fn(u, v) -> Vector. Poles are welded."""
    rows = []
    for j in range(nv + 1):
        rows.append([bm.verts.new(fn(i / nu, j / nv)) for i in range(nu)])
    faces = []
    for j in range(nv):
        for i in range(nu):
            i2 = (i + 1) % nu
            f = bm.faces.new([rows[j][i], rows[j][i2], rows[j + 1][i2], rows[j + 1][i]])
            f.material_index = M[mat]; f.smooth = True
            for loop, (a, b) in zip(f.loops, [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]):
                loop[uv].uv = (a / nu, b / nv)
            faces.append(f)
    verts = [v for r in rows for v in r]
    bmesh.ops.remove_doubles(bm, verts=verts, dist=1e-5)
    verts = [v for v in verts if v.is_valid]
    faces = [f for f in faces if f.is_valid]
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return verts

def superquadric(u, v, rx, ry, rz, p=2.4):
    """u around (0..1), v pole-to-pole (0 = -Z pole, 1 = +Z pole)."""
    th = u * 2 * math.pi; ph = (v - 0.5) * math.pi
    def sp(x, e): return math.copysign(abs(x) ** e, x)
    e = 2 / p
    cph, sph = math.cos(ph), math.sin(ph)
    return Vector((rx * sp(cph, e) * sp(math.cos(th), e), ry * sp(cph, e) * sp(math.sin(th), e), rz * sp(sph, e)))

def arm_point(side):
    L, R = P["arm_len"], P["arm_r"]
    sh = Vector((side * (P["body_w"] / 2 - 0.02), P["arm_y"], P["arm_z"]))
    rot = Matrix.Rotation(math.radians(-side * P["arm_splay"]), 4, 'Y') @ Matrix.Rotation(math.radians(8), 4, 'X')
    def f(u, v):
        q = superquadric(u, v, R, R * 0.82, L / 2, 2.3)
        k = 0.78 + 0.34 * (1 - v)                 # mitten: fatter toward the tip (v=0 is the tip)
        q = Vector((q.x * k, q.y * k, q.z - L / 2 + 0.02))
        return sh + (rot @ q)
    return f, sh

def foot_point(side):
    sx, sy, sz = P["foot_size"]
    c = Vector((side * P["foot_x"], P["foot_y"], 0))
    def f(u, v):
        q = superquadric(u, v, sx / 2, sy / 2, sz / 2, 2.4)
        toe = max(0.0, -q.y / (sy / 2))           # 0 at heel .. 1 at toe
        q.x *= 1 + 0.10 * toe
        q.z = (q.z * (0.62 if q.z < 0 else 1.0)) + sz * 0.5 * 0.62   # flat-ish sole on z=0
        q.z += 0.012 * toe * toe * (1 if q.z > sz * 0.3 else 0.3)  # slight toe spring
        q.rotate(Matrix.Rotation(math.radians(-side * 6), 3, 'Z'))  # toes out a little
        return c + q
    return f, c

def curl_path(s):
    """Goo curl on the top: rises, then hooks forward. s in [0,1]."""
    pts = [(0, 0.03, -0.06), (0, 0.02, 0.025), (0, 0.0, 0.085), (0, -0.035, 0.118), (0, -0.068, 0.108), (0, -0.074, 0.080)]
    n = len(pts) - 1; x = min(n - 1e-6, s * n); i = int(x); t = x - i
    p0, p1, p2, p3 = [Vector(pts[max(0, min(n, k))]) for k in (i - 1, i, i + 1, i + 2)]
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)

def curl_point():
    base = Vector((P["curl_x"], P["curl_y"], P["body_bottom"] + P["body_h"]))
    tilt = Matrix.Rotation(math.radians(-14), 3, 'Y')
    def f(u, v):
        s = v
        c = curl_path(s); e = 1e-3
        tng = (curl_path(min(1, s + e)) - curl_path(max(0, s - e))).normalized()
        n1 = tng.cross(Vector((1, 0, 0))).normalized(); n2 = tng.cross(n1).normalized()
        r = 0.115 * (1 - s) ** 1.45 + 0.017
        if s > 0.9: r *= math.sqrt(max(0.0, 1 - ((s - 0.9) / 0.1) ** 2)) * 0.98 + 0.02 * (1 - s)
        th = u * 2 * math.pi
        q = c + (n1 * math.cos(th) + n2 * math.sin(th)) * r
        return base + tilt @ q
    return f, base

PARTS = {"Body": 0, "Face": 1, "Emblem": 2, "Arm.L": 3, "Arm.R": 4, "Foot.L": 5, "Foot.R": 6, "Curl": 7}

def add_parts(bm, uv):
    # tag verts with an int layer: bmesh reuses freed slots, so creation order != vertex order
    layer = bm.verts.layers.int["part"]
    ranges = {}
    def track(name, fn, nu, nv, mat):
        vs = add_grid_solid(bm, uv, fn, nu, nv, mat)
        for v in vs: v[layer] = PARTS[name]
        ranges[name] = vs
    for side, sfx in ((1, "L"), (-1, "R")):
        track("Arm." + sfx, arm_point(side)[0], 16, 12, "Body")
        track("Foot." + sfx, foot_point(side)[0], 20, 12, "Feet")
    track("Curl", curl_point()[0], 12, 18, "Body")
    return ranges

# ---------------------------------------------------------------- assembly
def all_key_names():
    names = []
    for _, fn, _, _, _ in ISLANDS:
        for k in fn():
            if k != "Basis" and k not in names: names.append(k)
    for k in BODY_KEYS:
        if k not in names: names.append(k)
    return names

def project(bvh, x, z, h):
    hit = bvh.ray_cast(Vector((x, -2.0, z)), Vector((0, 1, 0)))
    if hit[0] is None:
        return Vector((x, -P["body_d"] / 2, z))
    loc, nrm = hit[0], hit[1]
    if nrm.y > 0: nrm = -nrm                      # front surface normal always faces -Y
    return loc + nrm * (P["eps"] + h)

def build_face_and_keys(ob):
    me = ob.data
    base_co = [v.co.copy() for v in me.vertices]
    body_count = len(base_co)
    names = all_key_names()
    # body surface per key (only body-deforming keys change it)
    body_cos = {"Basis": base_co}
    for k in names:
        f = BODY_KEYS.get(k)
        body_cos[k] = [c + f(c) for c in base_co] if f else base_co
    polys = [tuple(p.vertices) for p in me.polygons]
    bvhs = {}
    def bvh_for(k):
        key = k if k in BODY_KEYS else "Basis"
        if key not in bvhs: bvhs[key] = BVHTree.FromPolygons(body_cos[key], polys)
        return bvhs[key]
    # build islands
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    tag = bm.verts.layers.int["part"]             # created in build(); adding layers later kills BMVert refs
    island_info = []
    for name, fn, mat, ns, nt in ISLANDS:
        shapes = fn()
        start = len(bm.verts)
        grid = []
        bvh0 = bvh_for("Basis")
        for i in range(ns + 1):
            row = []
            for j in range(nt + 1):
                s, t = i / ns * 2 - 1, j / nt * 2 - 1
                v = bm.verts.new(project(bvh0, *shapes["Basis"](s, t))); v[tag] = PARTS["Face"]
                row.append(v)
            grid.append(row)
        for i in range(ns):
            for j in range(nt):
                f = bm.faces.new([grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]])
                f.material_index = M[mat]; f.smooth = True
                for loop, (a, b) in zip(f.loops, [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]):
                    loop[uv].uv = (a / ns, b / nt)
        island_info.append((name, shapes, ns, nt, start))
    # island winding is fixed by construction ((+s, +t) -> normal -Y); no recalc, collapsed islands have no area
    # back emblem: flat square decal on the +Y face, UV 0..1 (atlas cell chosen per player in the material)
    n_e = 10; half = P["emblem_size"] / 2; erow = []
    for i in range(n_e + 1):
        row = []
        for j in range(n_e + 1):
            x, z = half * (1 - 2 * i / n_e), P["emblem_z"] + half * (2 * j / n_e - 1)   # s runs along -X (reads from behind)
            hit = bvh0.ray_cast(Vector((x, 2.0, z)), Vector((0, -1, 0)))
            loc, nrm = hit[0], hit[1]
            if nrm.y < 0: nrm = -nrm
            v = bm.verts.new(loc + nrm * 0.0015); v[tag] = PARTS["Emblem"]
            row.append(v)
        erow.append(row)
    for i in range(n_e):
        for j in range(n_e):
            f = bm.faces.new([erow[i][j], erow[i + 1][j], erow[i + 1][j + 1], erow[i][j + 1]])
            f.material_index = M["Emblem"]; f.smooth = True
            for loop, (a, b) in zip(f.loops, [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]):
                loop[uv].uv = (a / n_e, b / n_e)
    parts = add_parts(bm, uv)
    bm.verts.index_update()
    ranges = {k: [v.index for v in vs] for k, vs in parts.items()}
    bm.to_mesh(me); bm.free()
    # shape keys
    if me.shape_keys: ob.shape_key_clear()
    ob.shape_key_add(name="Basis", from_mix=False)
    for k in names:
        kb = ob.shape_key_add(name=k, from_mix=False)
        kb.value = 0.0                            # 5.2 creates new keys at 1.0
        bvh = bvh_for(k)
        for i, c in enumerate(body_cos[k]): kb.data[i].co = c
        for name, shapes, ns, nt, start in island_info:
            fn = shapes.get(k)
            idx = start
            for i in range(ns + 1):
                for j in range(nt + 1):
                    s, t = i / ns * 2 - 1, j / nt * 2 - 1
                    if fn is not None:
                        kb.data[idx].co = project(bvh, *fn(s, t))
                    elif k in BODY_KEYS:  # island rides on the deformed body
                        kb.data[idx].co = project(bvh, *shapes["Basis"](s, t))
                    idx += 1
    return names, ranges

def srgb(h, a=1.0):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c) + (a,)

def build():
    for o in list(bpy.data.objects):
        if o.name.startswith(("CubeCritter", "CC_")): bpy.data.objects.remove(o)
    for o in list(bpy.data.objects):
        if o.name == "Body": bpy.data.objects.remove(o)
    bm = bmesh.new(); uv = bm.loops.layers.uv.new("UVMap"); bm.verts.layers.int.new("part")
    add_body(bm, uv)
    me = bpy.data.meshes.new("CubeCritter_Mesh"); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("CubeCritter_Mesh", me)
    bpy.context.scene.collection.objects.link(ob)
    for n in MATS:
        m = bpy.data.materials.get("M_CC_" + n) or bpy.data.materials.new("M_CC_" + n)
        me.materials.append(m)
    keys, ranges = build_face_and_keys(ob)
    print("mesh", len(me.vertices), "verts", sum(len(p.vertices) - 2 for p in me.polygons), "tris", "keys", keys)
    print("parts", {k: len(v) for k, v in ranges.items()})
    return ob
