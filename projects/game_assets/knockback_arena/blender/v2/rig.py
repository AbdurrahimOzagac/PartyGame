# Rig for CubeCritter v2. Run after build_v2.py (needs P, arm_point, foot_point, curl_path).
# Hierarchy (Unity Generic, in-place clips):
#   Root ─┬─ Foot.L / Foot.R          (feet never inherit body squash)
#         └─ COG ── Body ─┬─ Top ── Curl.1 ── Curl.2
#                         ├─ Arm.L / Arm.R
#                         └─ Muzzle   (non-deforming socket: projectile spawn, points forward)
# Vertical bones point +Z with roll 0, so local Y = up, local X = right(+X), local Z = forward(-Y):
#   rot X+ = lean forward, rot Z = side roll, rot Y = yaw, scale Y = squash/stretch.
import bpy, math
from mathutils import Vector, Matrix

def smooth(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)

def body_weights(z):
    zr = (z - P["body_bottom"]) / P["body_h"]
    top = 0.85 * smooth(0.48, 0.95, zr)
    return {"Body": 1 - top, "Top": top}

def build_rig(ob):
    for o in list(bpy.data.objects):
        if o.name == "CubeCritter": bpy.data.objects.remove(o)
    arm = bpy.data.armatures.new("CubeCritter_Rig")
    rig = bpy.data.objects.new("CubeCritter", arm)
    bpy.context.scene.collection.objects.link(rig)
    arm.display_type = 'STICK'; rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.selected_objects: o.select_set(False)
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.edit_bones
    def bone(name, head, tail, parent=None, deform=True, roll=0.0):
        b = eb.new(name); b.head = head; b.tail = tail; b.roll = roll
        if parent: b.parent = eb[parent]; b.use_connect = False
        b.use_deform = deform
        return b
    zb, top = P["body_bottom"], P["body_bottom"] + P["body_h"]
    bone("Root", (0, 0, 0), (0, 0, 0.08), deform=False)
    bone("COG", (0, 0, zb), (0, 0, zb + 0.20), "Root", deform=False)
    bone("Body", (0, 0, zb), (0, 0, zb + 0.52), "COG")
    bone("Top", (0, 0, zb + 0.52), (0, 0, top), "Body")
    # every deforming child shares Body's rest orientation (up, roll 0): squash never shears them at rest
    for side, sfx in ((1, "L"), (-1, "R")):
        f, sh = arm_point(side)
        bone("Arm." + sfx, sh, sh + Vector((0, 0, 0.12)), "Body")
        ff, c = foot_point(side)
        h = Vector((c.x, c.y, P["foot_size"][2] * 0.40))
        bone("Foot." + sfx, h, h + Vector((0, 0, 0.12)), "Root")
    cb = Vector((P["curl_x"], P["curl_y"], top))
    tilt = Matrix.Rotation(math.radians(-14), 3, 'Y')
    c0, c1 = (cb + tilt @ curl_path(s) for s in (0.12, 0.55))
    bone("Curl.1", c0, c0 + Vector((0, 0, 0.05)), "Top")
    bone("Curl.2", c1, c1 + Vector((0, 0, 0.05)), "Curl.1")
    mz = P["mouth_z"]
    bone("Muzzle", (0, -P["body_d"] / 2 - 0.02, mz), (0, -P["body_d"] / 2 - 0.16, mz), "Body", deform=False)
    # Face: control bone only. Its custom props drive the shape keys, so face animation lives in the armature
    # action and the FBX exporter bakes per-clip blendShape curves (it only swaps the armature's action per take).
    bone("Face", (0, -P["body_d"] / 2 - 0.05, P["eye_z"]), (0, -P["body_d"] / 2 - 0.05, P["eye_z"] + 0.08), "Body", deform=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    # skin
    ob.parent = rig; ob.matrix_parent_inverse.identity()
    ob.vertex_groups.clear()
    vg = {n: ob.vertex_groups.new(name=n) for n in ("Body", "Top", "Arm.L", "Arm.R", "Foot.L", "Foot.R", "Curl.1", "Curl.2")}
    part_of = [0] * len(ob.data.vertices)
    ob.data.attributes["part"].data.foreach_get("value", part_of)
    names = {v: k for k, v in PARTS.items()}
    samples = [cb + tilt @ curl_path(k / 200) for k in range(201)]
    for v in ob.data.vertices:
        part = names[part_of[v.index]]
        if part == "Curl":
            # weight by position along the curl: buried base -> Top, then Curl.1 -> Curl.2
            sv = min(range(201), key=lambda k: (samples[k] - v.co).length_squared) / 200
            a = smooth(0.08, 0.28, sv); b = smooth(0.45, 0.80, sv)
            ws = {"Top": 1 - a, "Curl.1": a * (1 - b), "Curl.2": a * b}
        elif part in vg:
            ws = {part: 1.0}
        else:                                      # body, face islands, emblem ride the body
            ws = body_weights(v.co.z)
        for n, w in ws.items():
            if w > 1e-4: vg[n].add([v.index], w, 'REPLACE')
    for m in [m for m in ob.modifiers if m.type == 'ARMATURE']: ob.modifiers.remove(m)
    mod = ob.modifiers.new("Armature", 'ARMATURE'); mod.object = rig
    for pb in rig.pose.bones:
        pb.rotation_mode = 'XYZ'                   # authored in small Euler angles; FBX bakes per frame anyway
    add_face_drivers(rig, ob)
    print("rig bones", [b.name for b in arm.bones])
    return rig

def add_face_drivers(rig, ob):
    face = rig.pose.bones["Face"]
    key = ob.data.shape_keys
    for kb in key.key_blocks[1:]:
        face[kb.name] = 0.0
        face.id_properties_ui(kb.name).update(min=0.0, max=1.0, soft_min=0.0, soft_max=1.0)
        kb.driver_remove("value")
        fc = kb.driver_add("value")
        d = fc.driver; d.type = 'AVERAGE'          # default is a scripted "1.0"
        for v in list(d.variables): d.variables.remove(v)
        var = d.variables.new(); var.name = "w"; var.type = 'SINGLE_PROP'
        var.targets[0].id_type = 'OBJECT'; var.targets[0].id = rig
        var.targets[0].data_path = f'pose.bones["Face"]["{kb.name}"]'
        for m in list(fc.modifiers): fc.modifiers.remove(m)
