# Concept v4: face ideas x spit-tube variants (mix and match sheet).
#   blender -b CubeCritter_v2.blend --python concept_face_v4.py -- <out_dir>
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

def face_big():            # huge round eyes, two catchlights
    for s in (1, -1):
        blob(s * 0.19, EZ, 0.085, 0.092, 0.018, INK, off=0.004)
        blob(s * 0.19 + 0.028, EZ + 0.035, 0.030, 0.032, 0.010, WHITE, off=0.019)
        blob(s * 0.19 - 0.030, EZ - 0.030, 0.013, 0.013, 0.008, WHITE, off=0.018)
        blob(s * 0.30, EZ - 0.115, 0.06, 0.03, 0.004, BLUSH, off=0.001)

def face_brows():          # small dot eyes + chunky determined brows
    for s in (1, -1):
        blob(s * 0.17, EZ - 0.01, 0.036, 0.046, 0.014, INK, off=0.005)
        blob(s * 0.17 - 0.010, EZ + 0.012, 0.011, 0.012, 0.006, WHITE, off=0.017)
        stroke([(s * 0.25, EZ + 0.118), (s * 0.17, EZ + 0.102), (s * 0.095, EZ + 0.070)], 0.023, INK, off=0.008)

def face_happy():          # ^ ^ smiling eyes, big blush, little tongue-less grin vibe
    for s in (1, -1):
        stroke(arc(s * 0.18, EZ - 0.02, 0.062, 0.052, n=18), 0.015, INK, off=0.005)
        blob(s * 0.29, EZ - 0.10, 0.07, 0.036, 0.004, BLUSH, off=0.001)

FACES = [("Mevcut", None), ("Hap", face_pill), ("Iri", face_big), ("Kasli", face_brows), ("Mutlu", face_happy)]

# ------------------------------------------------------------------ tube variants (all round cylinders)
# R, L: radius/length; lip: ring size; tilt: nose up (deg); waist: pinch in the middle; bands: [(u, half_width, mat)]
TUBES = [
    dict(name="Klasik", R=0.098, L=0.25, lip=0.030, lipmat=CREAM, tilt=4, waist=0.0, bands=[], bell=0.0),
    dict(name="Tombul", R=0.125, L=0.16, lip=0.036, lipmat=CREAM, tilt=4, waist=0.0, bands=[], bell=0.0),
    dict(name="Bel", R=0.095, L=0.27, lip=0.026, lipmat=PINK, tilt=8, waist=0.45, bands=[], bell=0.35),
    dict(name="Halkali", R=0.095, L=0.25, lip=0.030, lipmat=CREAM, tilt=4, waist=0.0,
         bands=[(0.32, 0.10, CREAM)], bell=0.0),
]

def tube_profile(T, mt, n=40):
    L, r = T["L"], T["R"]
    rr = T["lip"]; tc = L - rr
    rc_tip = r * (1 + T["bell"]) - 0.35 * rr
    pts = []
    for i in range(6):                                 # soft flare into the face
        k = i / 5
        pts.append((-0.04 + 0.05 * k, r + 0.035 * (1 - k) ** 2, mt))
    t1 = pts[-1][0]
    def R(u):
        v = r * (1 - T["waist"] * math.sin(math.pi * u) ** 2 * 0.5) * (1 + T["bell"] * u ** 4)
        for bu, bw, _ in T["bands"]:
            v += 0.022 * max(0.0, 1 - ((u - bu) / bw) ** 2) ** 0.5
        return v
    def M(u):
        for bu, bw, bm_ in T["bands"]:
            if abs(u - bu) < bw * 0.9: return bm_
        return mt
    a0 = -math.acos(max(-1.0, min(1.0, (R(1.0) - rc_tip) / rr)))
    t_join = tc + rr * math.sin(a0)
    for i in range(1, n + 1):
        u = i / n
        pts.append((t1 + (t_join - t1) * u, R(u), M(u)))
    for i in range(1, 19):
        a = a0 + (math.pi - a0) * i / 18
        pts.append((tc + rr * math.sin(a), rc_tip + rr * math.cos(a), T["lipmat"]))
    ri = rc_tip - rr; depth = tc - 0.08
    for i in range(1, 6):
        k = i / 5
        pts.append((tc - (tc - depth) * k, ri * (1 - 0.1 * k), HOLE))
    pts.append((depth - 0.004, 0.0, HOLE))
    return pts

def tube(T, mt):
    anchor, _ = surf(0.0, P["mouth_z"])
    tilt = math.radians(T["tilt"])
    fwd = Vector((0, -math.cos(tilt), math.sin(tilt)))
    side = Vector((1, 0, 0)); up = fwd.cross(side).normalized()
    pts = tube_profile(T, mt)
    me = bpy.data.meshes.new("tube"); bm = bmesh.new(); mats = []
    for p in pts:
        if p[2] not in mats: mats.append(p[2])
    seg = 48; rings = []
    for t, r, m in pts:
        rings.append([bm.verts.new(anchor + fwd * t + (side * math.cos(2 * math.pi * k / seg)
                                                        + up * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
    for j in range(len(rings) - 1):
        for k in range(seg):
            k2 = (k + 1) % seg
            f = bm.faces.new([rings[j][k], rings[j][k2], rings[j + 1][k2], rings[j + 1][k]])
            f.smooth = True; f.material_index = mats.index(pts[j + 1][2])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    o = bpy.data.objects.new("tube", me); sc.collection.objects.link(o)
    sub = o.modifiers.new("sub", 'SUBSURF'); sub.levels = 1; sub.render_levels = 2
    return keep(o)

sc.eevee.taa_render_samples = 32
cam("Cam_Mix", (1.35, -2.45, 0.85), (0.03, -0.4, 0.46), lens=54)
for fname, fn in FACES:
    for T in TUBES:
        use_mesh(fn is not None)
        if fn: fn()
        tube(T, BODY)
        still(os.path.join(TUBE_OUT, f"{fname}_{T['name']}.png"), (560, 540))
        clear_temp()
use_mesh(False)
print("CONCEPT DONE")
