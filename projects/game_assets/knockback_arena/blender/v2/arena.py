# Ice arena kit + world props, generated. Blender Z-up, metres; walkable top at z = 0.
# Every piece is one mesh with a colour attribute "Col" that is DATA for the Unity shader KnockbackArena/Ice
# (exported linear): R = height t (1 top, 0.5 bottom of the lip, 0 underside tip), G = snow/frost mask,
# B = AO / facet shade, A = sparkle allow. Colours, frost, cracks and the dark tile seams live in the shader
# (world-space mapped, so tiles need no UVs and join seamlessly). See research/ice_brief.md.
#
# Kit (4 m grid; rim noise fades to one canonical profile at tile ends, so any tile joins any other):
#   Ice_Top_4x4     interior tile                    Ice_Edge_4_A/B  one open side (-Y)
#   Ice_Corner      quarter disc, open on -Y and +X, radius 4 (four corners alone = a round 8 m floe)
#   Island_Underside_16   decorative chunk under the default 16 m arena (2-3 tiers, off-centre tip)
#   Crystal_Cluster_A/B/C, Floe_A/B/C (background), Podium_Ice (results)
# Collision is NOT here: Unity builds one floor collider for the whole layout (per-tile colliders snag boxes).
import bpy, math, random
from mathutils import Vector, noise

TILE = 4.0
LIP_IN = 0.30        # frost band + rounded snow nose start this far inside the rim (flush with the floor)
CLIFF = 1.4          # edge cliff height below the lip

# shader channel helpers: (t, snow, ao, sparkle)
TOP = (1.0, 0.0, 1.0, 1.0)
SNOW = (1.0, 1.0, 1.0, 0.6)

def clamp01(x): return min(1.0, max(0.0, x))

def side(t, rnd=None, snow=0.0, ao=1.0, sparkle=0.25):
    """Side/underside channels: t = height (0.5 under the lip .. 0 at the tip), random facet shade in AO."""
    shade = ao * (1.0 if rnd is None else rnd.uniform(0.84, 1.0))
    return (clamp01(t), clamp01(snow), shade, sparkle)

def strata(depth):
    """Thin pale bands in the cliff (frozen layers), strongest near the top."""
    return max(0.0, 1 - abs(math.sin(depth * 2.3 + 0.6)) * 6.0) * 0.45 * max(0.0, 1 - depth / 6)


class MeshBuilder:
    """Collect faces with per-corner channel colours; build one mesh. Faces are CCW seen from outside."""
    def __init__(self):
        self.v, self.f, self.col, self.smooth = [], [], [], []

    def vert(self, co):
        self.v.append(Vector(co)); return len(self.v) - 1

    def face(self, idx, cols, smooth=False):
        if len(set(idx)) < 3: return
        self.f.append(list(idx))
        self.col.append(cols if isinstance(cols[0], (list, tuple)) else [cols] * len(idx))
        self.smooth.append(smooth)

    def build(self, name, link=True):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(v) for v in self.v], [], self.f)
        attr = me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
        for p, cols in zip(me.polygons, self.col):
            for li, c in zip(p.loop_indices, cols): attr.data[li].color = c
        for p, s in zip(me.polygons, self.smooth): p.use_smooth = s
        me.validate(clean_customdata=False)
        ob = bpy.data.objects.new(name, me)
        if link: bpy.context.scene.collection.objects.link(ob)
        return ob


# ---------------------------------------------------------------- paths (rim polylines with outward normals)
def line_path(a, b, step=0.4):
    a, b = Vector(a), Vector(b)
    L = (b - a).length; n = max(2, round(L / step) + 1)
    t = (b - a).normalized(); out = Vector((t.y, -t.x))      # right-hand side is outward
    return [(a.lerp(b, i / (n - 1)), out.copy(), L * i / (n - 1)) for i in range(n)], L

def arc_path(c, r, a0, a1, step=0.4, closed=False):
    """Arc around centre c, angles in radians, CCW from a0 to a1; outward normal = radial."""
    c = Vector(c); L = abs(a1 - a0) * r; n = max(3, round(L / step) + 1)
    pts = []
    for i in range(n - (1 if closed else 0)):
        a = a0 + (a1 - a0) * i / (n - 1)
        d = Vector((math.cos(a), math.sin(a)))
        pts.append((c + d * r, d, L * i / (n - 1)))
    return pts, L

def poly_path(corners, step=0.4):
    """Closed polygon (CCW): straight runs, normals per segment."""
    pts, s0 = [], 0.0
    for i in range(len(corners)):
        seg, L = line_path(corners[i], corners[(i + 1) % len(corners)], step)
        pts += [(p, n, s + s0) for p, n, s in seg[:-1]]; s0 += L
    return pts, s0


# ---------------------------------------------------------------- the rim: snow nose + faceted cliff + icicles
def lip_profile(b, d):
    """(out, z) from the frost band inward edge, over the rounded snow nose, down to the cliff. Max 8 mm proud."""
    return [(-LIP_IN, 0.0), (-0.17, 0.006), (-0.02, 0.008), (0.07 * b, -0.012),
            (0.10 * b, -0.075), (0.075 * b, -0.15 * d), (0.0, -0.18 * d)]

def rim(mb, path, L, closed=False, depth=CLIFF, scale=1.0, seed=0, icicles=True, cliff_rows=4, jag=0.35, icicle_rate=0.4):
    rnd = random.Random(seed)
    n = len(path)
    cols_lip, cols_cliff, rim_pts = [], [], []
    for i, (p, out, s) in enumerate(path):
        w = 1.0 if closed else math.sin(math.pi * clamp01(s / L)) ** 0.6   # 0 at open ends: canonical profile
        # lumpy snow: bulge and droop vary along the rim (drips hang between the icicles)
        b = 1 + 0.9 * noise.noise(Vector((s * 1.6 + seed * 7.3, seed * 1.7, 0.5))) * w
        d = max(0.6, 1 + 1.3 * noise.noise(Vector((s * 2.1, seed * 3.1, 2.5))) * w)
        cols_lip.append([mb.vert((p.x + out.x * o * scale, p.y + out.y * o * scale, z * scale)) for o, z in lip_profile(b, d)])
        top_z = -0.18 * d * scale
        col = []
        for r in range(cliff_rows + 1):
            t = r / cliff_rows
            z = top_z + (-(depth * scale) - top_z) * t
            if 0 < r < cliff_rows: z += rnd.uniform(-0.08, 0.08) * scale * w
            if r == cliff_rows: z -= jag * scale * (0.5 + 0.5 * noise.noise(Vector((s * 1.7, seed, 9.0)))) * w
            o = (-0.03 - 0.32 * t ** 1.3) * scale
            if r > 0: o += rnd.uniform(-0.06, 0.06) * scale * (w if r < cliff_rows else 1)
            col.append(mb.vert((p.x + out.x * o, p.y + out.y * o, z)))
        cols_cliff.append(col)
        rim_pts.append((p, out, s, b, d))
    m = n if closed else n - 1
    for i in range(m):
        j = (i + 1) % n
        for k in range(len(cols_lip[i]) - 1):
            mb.face([cols_lip[i][k], cols_lip[i][k + 1], cols_lip[j][k + 1], cols_lip[j][k]], SNOW, smooth=True)
        for k in range(cliff_rows):
            a, b_, c, d_ = cols_cliff[i][k], cols_cliff[i][k + 1], cols_cliff[j][k + 1], cols_cliff[j][k]
            def ch(vi):
                dep = -mb.v[vi].z / scale
                return side(0.5 - 0.2 * clamp01(dep / depth), None, strata(dep), 1.0, 0.25)
            # two triangles per quad with their own facet shade: reads as cut crystal
            for tri in ((a, b_, c), (a, c, d_)):
                sh = rnd.uniform(0.84, 1.0)
                mb.face(list(tri), [(x[0], x[1], x[2] * sh, x[3]) for x in map(ch, tri)])
    if icicles:
        for i in range(1, n - 1):
            p, out, s, b, d = rim_pts[i]
            if rnd.random() > icicle_rate: continue
            ln = rnd.uniform(0.3, 0.9) * (1.35 if rnd.random() < 0.2 else 1.0)
            base = Vector((p.x + out.x * 0.05 * b * scale, p.y + out.y * 0.05 * b * scale, -0.12 * d * scale))
            base += Vector((-out.y, out.x, 0)) * rnd.uniform(-0.15, 0.15) * scale
            icicle(mb, base, ln * scale, rnd.uniform(0.045, 0.085) * scale, rnd, lean=out * 0.05)
    return cols_lip

def icicle(mb, base, length, r0, rnd, sides=6, rows=4, lean=Vector((0, 0, 0))):
    """Tapered, slightly leaning cone. Pale body to a near-white tip (snow mask rises toward the tip)."""
    rings = []
    rot = rnd.uniform(0, math.tau)
    for k in range(rows):
        t = k / rows
        r = r0 * (1 - t) ** 1.25
        c = base + Vector((lean.x * t * t, lean.y * t * t, -length * t))
        rings.append([mb.vert((c.x + math.cos(rot + a * math.tau / sides) * r, c.y + math.sin(rot + a * math.tau / sides) * r, c.z))
                      for a in range(sides)])
    tip = mb.vert(base + Vector((lean.x, lean.y, -length)))
    def ch(t): return (0.92, 0.25 + 0.6 * t, 1.0, 0.8)
    for k in range(rows - 1):
        c0, c1 = ch(k / rows), ch((k + 1) / rows)
        for a in range(sides):
            b = (a + 1) % sides
            mb.face([rings[k][a], rings[k + 1][a], rings[k + 1][b], rings[k][b]], [c0, c1, c1, c0], smooth=True)
    cl = ch((rows - 1) / rows)
    for a in range(sides):
        b = (a + 1) % sides
        mb.face([rings[-1][a], tip, rings[-1][b]], [cl, ch(1.0), cl], smooth=True)


def top_polygon(mb, pts2d, z=0.0, ch=TOP):
    idx = [mb.vert((x, y, z)) for x, y in pts2d]
    mb.face(idx, ch)
    return idx


# ---------------------------------------------------------------- kit tiles
H = TILE / 2

def tile_top():
    mb = MeshBuilder()
    top_polygon(mb, [(-H, -H), (H, -H), (H, H), (-H, H)])
    # short skirt so a missing neighbour never shows a paper-thin edge
    sk = [mb.vert(p) for p in [(-H, -H, -0.25), (H, -H, -0.25), (H, H, -0.25), (-H, H, -0.25)]]
    for i in range(4):
        j = (i + 1) % 4
        mb.face([i, sk[i], sk[j], j], side(0.48))
    return mb.build("Ice_Top_4x4")

def tile_edge(seed, name):
    mb = MeshBuilder()
    top_polygon(mb, [(-H, -H + LIP_IN), (H, -H + LIP_IN), (H, H), (-H, H)])
    path, L = line_path((-H, -H), (H, -H))
    rim(mb, path, L, seed=seed)
    return mb.build(name)

def tile_corner(seed=11):
    """Open on -Y and +X. Quarter disc of radius TILE around the inner corner (-H, +H)."""
    mb = MeshBuilder()
    c = Vector((-H, H))
    path, L = arc_path(c, TILE, -math.pi / 2, 0.0)
    r = TILE - LIP_IN
    pts = [(c.x, c.y)] + [(c.x + math.cos(a) * r, c.y + math.sin(a) * r)
                          for a in [-math.pi / 2 + (math.pi / 2) * i / 24 for i in range(25)]]
    top_polygon(mb, pts)
    rim(mb, path, L, seed=seed)
    return mb.build("Ice_Corner")


# ---------------------------------------------------------------- underside + floes
def rounded_square(half, radius, step=0.6):
    """CCW outline of a rounded square (centre origin)."""
    pts = []
    for cx, cy, a0 in [(half - radius, -half + radius, -math.pi / 2), (half - radius, half - radius, 0.0),
                       (-half + radius, half - radius, math.pi / 2), (-half + radius, -half + radius, math.pi)]:
        n = max(2, round(radius * math.pi / 2 / step))
        for i in range(n + 1):
            a = a0 + math.pi / 2 * i / n
            pts.append(Vector((cx + math.cos(a) * radius, cy + math.sin(a) * radius)))
    return pts

def resample(outline, step):
    out = []
    for i in range(len(outline)):
        a, b = outline[i], outline[(i + 1) % len(outline)]
        n = max(1, round((b - a).length / step))
        out += [a.lerp(b, k / n) for k in range(n)]
    return out

def underside(name, outline, depth=11.0, rings=9, seed=3, top_z=-1.2, inset0=0.45, chunky=0.55, tiers=(0.28, 0.62),
              tip_off=(0.2, -0.12), icicles=0.5, mb=None):
    """Inverted faceted chunk in tiers: rings of the outline shrink toward an off-centre lumpy tip.
    With mb given, adds into it and returns None; otherwise builds its own object."""
    rnd = random.Random(seed)
    own = mb is None
    mb = mb or MeshBuilder()
    ctr0 = sum(outline, Vector((0, 0))) / len(outline)
    base = resample(outline, 1.1)
    R = max((p - ctr0).length for p in base)
    def shrink(t):
        # steeper just below each tier line: reads as stacked ledges, not a cone
        s = (1 - t) ** 1.15
        for tl in tiers: s -= 0.09 * (1 / (1 + math.exp(-(t - tl) * 40)))
        return max(0.03, s)
    ring_ids, zs = [], []
    for k in range(rings):
        t = k / (rings - 1)
        z = top_z - depth * (t ** 0.9)
        ctr = ctr0 + Vector(tip_off) * R * t * t
        ids = []
        for p in base:
            d = p - ctr0
            dn = d.normalized() if d.length > 1e-6 else Vector((1, 0))
            ang = math.atan2(d.y, d.x)
            q = ctr + d * shrink(t) - dn * (inset0 + rnd.uniform(0, chunky) * (0.3 + t)) * (1 - t * 0.6)
            q += dn * noise.noise(Vector((math.cos(ang) * 2, math.sin(ang) * 2, k * 0.7 + seed))) * chunky * R * 0.08
            ids.append(mb.vert((q.x, q.y, z + rnd.uniform(-0.3, 0.3) * (0 < k < rings - 1))))
        ring_ids.append(ids); zs.append(z)
    tipc = ctr0 + Vector(tip_off) * R
    tip = mb.vert((tipc.x + rnd.uniform(-0.6, 0.6), tipc.y + rnd.uniform(-0.6, 0.6), top_z - depth - 0.8))
    n = len(base)
    def tval(zv): return 0.42 * clamp01(1 - (top_z - zv) / (depth + 0.8))
    for k in range(rings - 1):
        for i in range(n):
            j = (i + 1) % n
            a, b, c, d = ring_ids[k][i], ring_ids[k + 1][i], ring_ids[k + 1][j], ring_ids[k][j]
            for tri in ((a, b, c), (a, c, d)):
                sh = rnd.uniform(0.82, 1.0)
                mb.face(list(tri), [side(tval(mb.v[v].z), None, strata(top_z - mb.v[v].z) * 0.6, sh, 0.0) for v in tri])
    for i in range(n):
        j = (i + 1) % n
        sh = rnd.uniform(0.82, 1.0)
        mb.face([ring_ids[-1][i], tip, ring_ids[-1][j]], [side(tval(mb.v[v].z), None, 0, sh, 0.0) for v in (ring_ids[-1][i], tip, ring_ids[-1][j])])
    mb.face(ring_ids[0], side(0.45, None, 0, 0.8, 0.0))   # cap: closes the chunk if seen through gaps
    for _ in range(int(n * icicles)):                      # icicles hanging off the ledges
        k = rnd.randint(2, rings - 2); i = rnd.randrange(n)
        p = mb.v[ring_ids[k][i]]
        icicle(mb, p.copy(), rnd.uniform(0.6, 2.2) * (depth / 11) ** 0.5, rnd.uniform(0.12, 0.3) * (depth / 11) ** 0.5, rnd, sides=5)
    return mb.build(name) if own else None

def floe(name, radius, seed, depth=None, with_icicles=True):
    """Small floating island: round top with a frosty rim, faceted underside. Background decoration."""
    mb = MeshBuilder()
    path, L = arc_path((0, 0), radius, 0, math.tau, step=0.4 * max(1, radius / 3), closed=True)
    r = radius - LIP_IN
    top_polygon(mb, [(math.cos(a) * r, math.sin(a) * r) for a in [math.tau * i / 48 for i in range(48)]], ch=(1.0, 0.55, 1.0, 1.0))
    rim(mb, path, L, closed=True, depth=0.9, seed=seed, icicles=with_icicles)
    outline = [Vector((math.cos(a) * radius * 0.96, math.sin(a) * radius * 0.96)) for a in [math.tau * i / 18 for i in range(18)]]
    underside(name, outline, depth=depth or radius * 1.7, rings=6, seed=seed, top_z=-0.7, inset0=0.2, chunky=0.4,
              tiers=(0.4,), tip_off=(0.15, 0.1), icicles=0.3, mb=mb)
    return mb.build(name)

def crystal(mb, base, direction, length, radius, rnd, sides=6):
    """Hex prism with a pointed tip; alternating facets pale and mid so it sparkles as it turns."""
    d = direction.normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    u = d.cross(up).normalized(); w = d.cross(u)
    rot = rnd.uniform(0, math.tau)
    def ring(c, r): return [mb.vert(c + (u * math.cos(rot + a * math.tau / sides) + w * math.sin(rot + a * math.tau / sides)) * r) for a in range(sides)]
    b0 = ring(base - d * 0.3, radius * 0.92); b1 = ring(base + d * length * 0.72, radius)
    tip = mb.vert(base + d * length)
    for a in range(sides):
        b = (a + 1) % sides
        fs = 0.35 if a % 2 else 0.1
        lo, hi = (0.5, fs, 0.92, 0.6), (0.8, fs + 0.25, 1.0, 1.0)
        mb.face([b0[a], b0[b], b1[b], b1[a]], [lo, lo, hi, hi])
        mb.face([b1[a], b1[b], tip], [hi, hi, (0.9, 0.7, 1.0, 1.0)])
    mb.face(b0[::-1], (0.45, 0.0, 0.8, 0.0))

def crystal_cluster(name, seed, n=6, size=1.0):
    rnd = random.Random(seed)
    mb = MeshBuilder()
    for i in range(n):
        ang = rnd.uniform(0, math.tau); tilt = rnd.uniform(0.0, 0.55) * (0 if i == 0 else 1)
        d = Vector((math.cos(ang) * math.sin(tilt), math.sin(ang) * math.sin(tilt), math.cos(tilt)))
        base = Vector((math.cos(ang), math.sin(ang), 0)) * rnd.uniform(0, 0.5) * size * (0 if i == 0 else 1)
        ln = size * (rnd.uniform(2.6, 4.2) if i == 0 else rnd.uniform(1.0, 2.6))
        crystal(mb, base, d, ln, size * rnd.uniform(0.28, 0.5) * (1.3 if i == 0 else 1), rnd)
    # the chunk they grow from, so they read as a floating piece, not a stuck-on prop
    outline = [Vector((math.cos(a), math.sin(a))) * size * 1.0 for a in [math.tau * i / 9 for i in range(9)]]
    underside(name, outline, depth=size * 1.8, rings=4, seed=seed, top_z=0.15, inset0=0.0, chunky=0.3, tiers=(), tip_off=(0.1, 0.0), icicles=0.3, mb=mb)
    return mb.build(name)


# ---------------------------------------------------------------- podium
def podium(name="Podium_Ice"):
    """Four octagonal ice columns with snowy caps; 2nd-1st-3rd-4th left to right. Tops are the stand points."""
    spec = [(-1.65, 0.85, 2), (-0.55, 1.25, 1), (0.55, 0.62, 3), (1.65, 0.38, 4)]   # x, height, place
    mb = MeshBuilder()
    for x, h, place in spec:
        first = len(mb.v)
        r = 0.56
        corners = [Vector((math.cos(a) * r, math.sin(a) * r)) for a in [math.pi / 8 + math.tau * i / 8 for i in range(8)]]
        path, L = poly_path(corners, step=0.15)
        top_polygon(mb, [(p.x * (r - LIP_IN * 0.45) / r, p.y * (r - LIP_IN * 0.45) / r) for p in corners], ch=(1.0, 0.7, 1.0, 1.0))
        rim(mb, path, L, closed=True, depth=1.0, scale=0.45, seed=place, icicles=False, cliff_rows=3, jag=0.0)
        rnd = random.Random(place)
        top = [mb.vert((p.x, p.y, -0.08)) for p in corners]
        bot = [mb.vert((p.x * 1.06, p.y * 1.06, -h)) for p in corners]
        for i in range(8):
            j = (i + 1) % 8
            sh = rnd.uniform(0.84, 1.0)
            mb.face([top[i], bot[i], bot[j], top[j]], [side(0.5, None, 0.1, sh), side(0.3, None, 0, sh), side(0.3, None, 0, sh), side(0.5, None, 0.1, sh)])
        for v in mb.v[first:]: v += Vector((x, 0, h))   # built with the top at 0; floor at z = 0, top at h
    return mb.build(name), spec
