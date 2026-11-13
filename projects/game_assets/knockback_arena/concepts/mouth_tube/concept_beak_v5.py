# Concept v5: pill-eye face + beak-like cylinder snouts, front / 3-4 / side views.
#   blender -b CubeCritter_v2.blend --python concept_beak_v5.py -- <out_dir>
# Loads Ezel's preview helpers, edits only in memory, never saves the .blend.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

TUBE_OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(TUBE_OUT, exist_ok=True)
HERE = os.path.dirname(bpy.data.filepath)
exec(open(os.path.join(HERE, "render_previews.py")).read().rsplit("argv = sys.argv", 1)[0])

setup_look()
MD = bpy.data.materials
shared = {n: MD["M_CC_" + n] for n in ("Eye", "EyeShine", "Mouth", "Tongue", "Blush", "Feet") if ("M_CC_" + n) in MD}
per = {p: {"Body": MD.get("M_CC_Body_" + p) or vinyl("M_CC_Body_" + p, h),
           "Emblem": MD.get("M_CC_Emblem_" + p) or MD.get("M_CC_Emblem_Tomato")} for p, h in PLAYERS.items()}
for n in ("SpitBlob", "SpitDroplet.0", "SpitDroplet.1"):
    o = bpy.data.objects.get(n)
    if o: o.hide_render = True
if rig.animation_data: rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.location, pb.rotation_euler, pb.scale = (0, 0, 0), (0, 0, 0), (1, 1, 1)
for k in rig.pose.bones["Face"].keys(): rig.pose.bones["Face"][k] = 0.0
apply_player(ob, "Tomato", shared, per)
bpy.context.view_layer.update()
BODY = per["Tomato"]["Body"]
HOLE = shared["Mouth"]

def toy(name, hexcol, rough=0.28, metal=0.0, emit=0.0):
    m = MD.get(name) or MD.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(hexcol)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    b.inputs["Coat Weight"].default_value = 1.0
    b.inputs["Coat Roughness"].default_value = 0.04
    if emit:
        b.inputs["Emission Color"].default_value = srgb(hexcol); b.inputs["Emission Strength"].default_value = emit
    return m

INK = toy("MF_Ink", "#121218", rough=0.12)
WHITE = toy("MF_White", "#FFFFFF", rough=0.4, emit=2.0)
BLUSH = toy("MF_Blush", "#FF8FA8", rough=0.6)
CREAM = toy("MF_Cream", "#FFF3DE")
PINK = toy("MF_Pink", "#FF7FA0")
DARK = toy("MF_TomatoDark", "#9C2C18")

# ------------------------------------------------------------------ remove the original face (in memory only)
ORIG_FACE_SLOTS = {1, 2, 3, 4, 5}       # Eye, EyeShine, Mouth, Tongue, Blush
orig_me = ob.data
bare_me = ob.data.copy(); bare_me.name = "CC_Bare"
bm = bmesh.new(); bm.from_mesh(bare_me)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in ORIG_FACE_SLOTS], context='FACES')
bm.to_mesh(bare_me); bm.free()

def use_mesh(bare):
    ob.data = bare_me if bare else orig_me
    bpy.context.view_layer.update()

# ------------------------------------------------------------------ surface helpers
use_mesh(True)
deps = bpy.context.evaluated_depsgraph_get()
EV = ob.evaluated_get(deps)
def surf(x, z, off=0.0):
    hit, loc, nrm, _ = EV.ray_cast(Vector((x, -2.0, z)), Vector((0, 1, 0)))
    if not hit: loc, nrm = Vector((x, -P["body_d"] / 2, z)), Vector((0, -1, 0))
    if nrm.y > 0: nrm = -nrm
    return loc + nrm * off, nrm

TEMP = []
def keep(o): TEMP.append(o); return o
def clear_temp():
    for o in TEMP:
        if o.name in bpy.data.objects: bpy.data.objects.remove(o)
    TEMP.clear()

def blob(x, z, a, b, depth, mat, rot=0.0, off=0.0, round_box=False):
    """Flattened dome (or pill) lying on the face. a, b: half width/height; depth: how far it stands out."""
    loc, nrm = surf(x, z, off)
    if round_box:                                      # pill: superquadric, rounded-box section
        me = bpy.data.meshes.new("pill"); bm = bmesh.new()
        nu, nv = 32, 24
        rows = [[bm.verts.new(superquadric(i / nu, j / nv, a, depth, b, 3.2)) for i in range(nu)] for j in range(nv + 1)]
        for j in range(nv):
            for i in range(nu):
                bm.faces.new([rows[j][i], rows[j][(i + 1) % nu], rows[j + 1][(i + 1) % nu], rows[j + 1][i]])
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new("pill", me); sc.collection.objects.link(o)
        bpy.context.view_layer.objects.active = o; o.select_set(True)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=32, ring_count=16)
        o = bpy.context.active_object
        o.scale = (a, depth, b)
    o.location = loc
    q = Vector((0, -1, 0)).rotation_difference(nrm)
    o.rotation_euler = (q.to_matrix() @ Matrix.Rotation(rot, 3, 'Y')).to_euler()
    bpy.ops.object.shade_smooth()
    o.data.materials.append(mat)
    return keep(o)

def stroke(pts2d, radius, mat, off=0.004):
    """Tube along points on the face (brows, ^ eyes, smiles). pts2d: [(x, z), ...]"""
    cu = bpy.data.curves.new("stroke", 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = 6; cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(pts2d) - 1)
    for p, (x, z) in zip(sp.points, pts2d):
        loc, _ = surf(x, z, off); p.co = (loc.x, loc.y, loc.z, 1)
    o = bpy.data.objects.new("stroke", cu); sc.collection.objects.link(o)
    cu.materials.append(mat)
    return keep(o)

def arc(cx, cz, w, h, n=16, flip=False):
    return [(cx + w * (2 * i / n - 1), cz + (h if not flip else -h) * (1 - (2 * i / n - 1) ** 2)) for i in range(n + 1)]

EZ = P["eye_z"] + 0.03     # eyes sit a bit higher so the big snout has room

def face_pill():           # tall pill eyes with a white dot, like the team's reference
    for s in (1, -1):
        blob(s * 0.17, EZ + 0.01, 0.042, 0.085, 0.016, INK, round_box=True, off=0.008)
        blob(s * 0.17 - 0.012, EZ + 0.055, 0.016, 0.018, 0.008, WHITE, off=0.022)
        blob(s * 0.275, EZ - 0.10, 0.055, 0.028, 0.004, BLUSH, off=0.001)


# ------------------------------------------------------------------ v5: beak-like cylinder snouts (round bore, crisp cut tip)
BEAK = toy("MB_Beak", "#FFA51F", rough=0.22)
BEAK_D = toy("MB_BeakDark", "#E07410", rough=0.22)
BEAK_L = toy("MB_BeakLight", "#FFC75A", rough=0.22)
NOSTRIL = toy("MB_Nostril", "#5A2A10", rough=0.4)

# R, L: radius/length; edge: rounding of the cut tip; tilt: nose up (deg)
# groove: side seam depth (upper/lower beak line); visor: upper beak overhang length; tipband: darker hard tip (0..1 of L)
BEAKS = [
    dict(name="Duz", title="A  Düz kesik", R=0.095, L=0.24, edge=0.010, tilt=4, nostrils=True),
    dict(name="Siperli", title="B  Üst gaga siperli", R=0.095, L=0.22, edge=0.010, tilt=4, nostrils=True, visor=0.05),
    dict(name="Cizgili", title="C  Çene çizgili", R=0.100, L=0.24, edge=0.010, tilt=4, nostrils=True, groove=0.016,
         upper=BEAK, lower=BEAK_L),
    dict(name="Tombul", title="D  Tombul, koyu uç", R=0.120, L=0.16, edge=0.014, tilt=4, nostrils=True, tipband=0.28),
    dict(name="Uzun", title="E  Uzun ince", R=0.072, L=0.33, edge=0.008, tilt=10, nostrils=True),
]

def beak_frame(B):
    anchor, _ = surf(0.0, P["mouth_z"])
    tilt = math.radians(B["tilt"])
    fwd = Vector((0, -math.cos(tilt), math.sin(tilt)))
    side = Vector((1, 0, 0)); up = fwd.cross(side).normalized()
    if up.z < 0: up = -up
    return anchor, fwd, side, up

def beak_profile(B, n=30):
    """(t, r, tag, u): straight cylinder from inside the face, rounded cut edge, flat front ring, round bore."""
    L, R, e = B["L"], B["R"], B["edge"]
    ri = R * 0.55
    pts = [(-0.05, R, "out", 0.0)]
    for i in range(1, n + 1):
        u = i / n
        pts.append((-0.05 + (L - e + 0.05) * u, R, "out", u))
    for i in range(1, 7):
        a = math.pi / 2 * i / 6
        pts.append((L - e + e * math.sin(a), R - e + e * math.cos(a), "tip", 1.0))
    pts.append((L, ri + e, "tip", 1.0))
    for i in range(1, 7):
        a = math.pi / 2 * i / 6
        pts.append((L - e + e * math.cos(a), ri + e - e * math.sin(a), "hole", 1.0))
    depth = L - 0.10
    pts.append((depth, ri, "hole", 1.0))
    pts.append((depth - 0.004, 0.0, "hole", 1.0))
    return pts

def beak_mat(B, tag, u, ang):
    if tag == "hole": return HOLE
    if B.get("tipband") and (tag == "tip" or u > 1 - B["tipband"]): return BEAK_D
    if "upper" in B:
        if tag == "out" and u > 0.05 and abs(math.sin(ang)) < 0.07: return NOSTRIL
        return B["upper"] if math.sin(ang) >= 0 else B["lower"]
    return BEAK

def beak_mesh(B):
    anchor, fwd, side, up = beak_frame(B)
    pts = beak_profile(B)
    seg = 64
    me = bpy.data.meshes.new("beak"); bm = bmesh.new(); mats = []
    def mi(m):
        if m not in mats: mats.append(m)
        return mats.index(m)
    rings = []
    for t, r, tag, u in pts:
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            rr = r
            g = B.get("groove", 0.0)
            if g and tag == "out" and u > 0.05:          # seam along both sides: reads as upper/lower beak
                d = min(abs(math.sin(a)), 1.0)
                rr -= g * math.exp(-(d / 0.09) ** 2)
            ring.append(bm.verts.new(anchor + fwd * t + (side * math.cos(a) + up * math.sin(a)) * rr))
        rings.append(ring)
    for j in range(len(rings) - 1):
        t, r, tag, u = pts[j + 1]
        for k in range(seg):
            k2 = (k + 1) % seg
            f = bm.faces.new([rings[j][k], rings[j][k2], rings[j + 1][k2], rings[j + 1][k]])
            f.smooth = True
            f.material_index = mi(beak_mat(B, tag, u, 2 * math.pi * (k + 0.5) / seg))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    o = bpy.data.objects.new("beak", me); sc.collection.objects.link(o)
    sub = o.modifiers.new("sub", 'SUBSURF'); sub.levels = 1; sub.render_levels = 2
    keep(o)
    if B.get("visor"): visor(B, anchor, fwd, side, up)
    if B.get("nostrils"): nostrils(B, anchor, fwd, side, up)
    return o

def visor(B, anchor, fwd, side, up):
    """Upper beak: a half shell over the top that runs past the round mouth, like a bird's upper mandible."""
    L, R, th = B["L"], B["R"] - 0.004, 0.022
    loop = []
    n = 14
    t0, t1 = L * 0.35, L + B["visor"]
    for i in range(n + 1):                    # outer edge, tip rounds down toward the opening
        u = i / n
        loop.append((t0 + (t1 - t0) * u, R + 0.004 + th - 0.010 * u ** 3))
    for i in range(1, 7):
        a = math.pi * i / 6
        loop.append((t1 + th * 0.5 * math.sin(a), R + 0.004 + th / 2 - 0.010 + (th / 2) * math.cos(a)))
    for i in range(n, -1, -1):
        u = i / n
        loop.append((t0 + (t1 - t0) * u, R + 0.004 - 0.010 * u ** 3))
    seg = 32; a0, a1 = math.radians(-12), math.radians(192)
    bm = bmesh.new(); rows = []
    for k in range(seg + 1):
        a = a0 + (a1 - a0) * k / seg
        rows.append([bm.verts.new(anchor + fwd * t + (side * math.cos(a) + up * math.sin(a)) * r) for t, r in loop])
    m = len(loop)
    for k in range(seg):
        for i in range(m):
            i2 = (i + 1) % m
            f = bm.faces.new([rows[k][i], rows[k][i2], rows[k + 1][i2], rows[k + 1][i]]); f.smooth = True
    bm.faces.new(rows[0]); bm.faces.new(list(reversed(rows[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("visor"); bm.to_mesh(me); bm.free()
    me.materials.append(BEAK_D)
    o = bpy.data.objects.new("visor", me); sc.collection.objects.link(o)
    sub = o.modifiers.new("sub", 'SUBSURF'); sub.levels = 1; sub.render_levels = 2
    keep(o)

def nostrils(B, anchor, fwd, side, up):
    R = B["R"] + (0.016 if B.get("visor") else 0.0)
    t = min(0.07, B["L"] * 0.32)
    for s in (1, -1):
        a = math.pi / 2 + s * 0.42
        n = side * math.cos(a) + up * math.sin(a)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=24, ring_count=12)
        o = bpy.context.active_object
        x = fwd.cross(n).normalized()
        o.matrix_world = Matrix((
            (x.x * 0.014, fwd.x * 0.022, n.x * 0.006, (anchor + fwd * t + n * (R + 0.006)).x),
            (x.y * 0.014, fwd.y * 0.022, n.y * 0.006, (anchor + fwd * t + n * (R + 0.006)).y),
            (x.z * 0.014, fwd.z * 0.022, n.z * 0.006, (anchor + fwd * t + n * (R + 0.006)).z),
            (0, 0, 0, 1)))
        bpy.ops.object.shade_smooth(); o.data.materials.append(NOSTRIL)
        keep(o)

VIEWS = [("Onden", (0.0, -2.75, 0.62), (0.0, -0.4, 0.47), 60),
         ("Capraz", (1.35, -2.45, 0.85), (0.03, -0.4, 0.46), 54),
         ("Yandan", (3.1, -0.3, 0.62), (0.0, -0.3, 0.48), 50)]

sc.eevee.taa_render_samples = 32
use_mesh(True)
for B in BEAKS:
    face_pill()
    beak_mesh(B)
    for vname, loc_, tgt, lens in VIEWS:
        cam("Cam_" + vname, loc_, tgt, lens=lens)
        still(os.path.join(TUBE_OUT, f"{B['name']}_{vname}.png"), (560, 540))
    clear_temp()
use_mesh(False)
print("CONCEPT DONE")
