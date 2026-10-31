# One-command rebuild of the ice arena, background and props:
#   Blender -b --factory-startup --python make_world.py -- [--no-export]
# kit pieces -> default 16 m arena (tiles placed + underside + floor collider) -> backdrop ring -> podium ->
# crown + jump pickup -> save IceArena.blend -> FBX into Assets/ShooterParty/MiniGames/KnockbackArena/Models/{Arena,Props}.
import bpy, os, sys, math, random
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for f in ("arena.py", "props.py", "world.py"):
    exec(open(os.path.join(HERE, f)).read().split('if __name__ == "__main__":')[0])
UNITY = "/Users/ezelbayraktar/Documents/Development/PartyGame/Assets/ShooterParty/MiniGames/KnockbackArena/Models"

for o in list(bpy.data.objects): bpy.data.objects.remove(o)
sc = bpy.context.scene
sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0
mat = ice_material()

def coll(name):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in sc.collection.children: sc.collection.children.link(c)
    return c

def move(o, c):
    for u in list(o.users_collection): u.objects.unlink(o)
    c.objects.link(o)

kit = coll("Kit"); arena = coll("Arena_Ice_16"); back = coll("Backdrop"); props = coll("Props")
pieces = kit_pieces()
for o in pieces.values():
    o.data.materials.append(mat); move(o, kit)

# ---- default arena: tile instances (linked meshes) parented under one root
root = bpy.data.objects.new("Arena_Ice_16", None); arena.objects.link(root)
for i, (name, x, y, rot) in enumerate(layout_4x4()):
    o = bpy.data.objects.new(f"{name}_{i:02d}", pieces[name].data); arena.objects.link(o)
    o.location = (x, y, 0); o.rotation_euler = (0, 0, math.radians(rot)); o.parent = root
us = bpy.data.objects.new("Island_Underside", pieces["Island_Underside_16"].data); arena.objects.link(us); us.parent = root
# one floor collider for the whole layout (tiles are visual only): rounded square slab, top at z = 0
mb = MeshBuilder()
ring = rounded_square(8.0, 4.0, step=0.5)
top = [mb.vert((p.x, p.y, 0.0)) for p in ring]; bot = [mb.vert((p.x, p.y, -1.0)) for p in ring]
w = (1, 1, 1, 1)
mb.face(top, w); mb.face(bot[::-1], w)
for i in range(len(ring)):
    j = (i + 1) % len(ring)
    mb.face([bot[i], bot[j], top[j], top[i]], w)
col = mb.build("Floor_Collider"); move(col, arena); col.parent = root

# ---- backdrop ring (35-80 m out, y -15..+10; every spawn sees something on the horizon)
broot = bpy.data.objects.new("Arena_Backdrop", None); back.objects.link(broot)
spots = [("Floe_A", 48, 20, -6, 0.0), ("Crystal_Cluster_A", 40, 75, 4, 1.0), ("Floe_B", 70, 135, -12, 0.0),
         ("Crystal_Cluster_C", 55, 190, -4, 1.0), ("Floe_C", 38, 245, 6, 0.0), ("Crystal_Cluster_B", 62, 300, -8, 1.0),
         ("Floe_A", 80, 330, 8, 0.0), ("Floe_C", 66, 95, 9, 0.0)]
rnd = random.Random(5)
for i, (name, dist, az, z, tilt) in enumerate(spots):
    o = bpy.data.objects.new(f"{name}_{i}", pieces[name].data); back.objects.link(o); o.parent = broot
    a = math.radians(az)
    o.location = (math.cos(a) * dist, math.sin(a) * dist, z)
    o.rotation_euler = (math.radians(rnd.uniform(-6, 6)) * tilt, math.radians(rnd.uniform(-6, 6)) * tilt, rnd.uniform(0, math.tau))
    o.scale = (1.0 + 0.3 * (dist > 60),) * 3

# ---- podium + props
pod = pieces["Podium_Ice"]; move(pod, props)
crown = build_crown(); pick = build_jump_pickup()
for o in (crown, pick): move(o, props)

def export(objs, path, colors='LINEAR'):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, object_types={'MESH', 'EMPTY'}, use_selection=True, bake_anim=False,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', colors_type=colors, use_custom_props=False,
                             path_mode='STRIP', embed_textures=False, use_mesh_modifiers=True)

if "--no-export" not in args:
    os.makedirs(UNITY + "/Arena", exist_ok=True); os.makedirs(UNITY + "/Props", exist_ok=True)
    export([root] + list(root.children), UNITY + "/Arena/Arena_Ice_16.fbx")
    export([broot] + list(broot.children), UNITY + "/Arena/Arena_Backdrop.fbx")
    for name in ("Ice_Top_4x4", "Ice_Edge_4_A", "Ice_Edge_4_B", "Ice_Corner", "Island_Underside_16",
                 "Crystal_Cluster_A", "Crystal_Cluster_B", "Crystal_Cluster_C", "Floe_A", "Floe_B", "Floe_C"):
        export_piece(pieces[name], f"{UNITY}/Arena/Kit_{name}.fbx")
    export_piece(pod, UNITY + "/Arena/Podium_Ice.fbx")
    for o in (crown, pick):
        export([o], f"{UNITY}/Props/{o.name}.fbx", colors='NONE')
    print("exported", sorted(os.listdir(UNITY + "/Arena")), sorted(os.listdir(UNITY + "/Props")))

# lay the loose pieces out beside the arena for whoever opens the .blend (exports above used them at the origin)
for i, name in enumerate(["Ice_Top_4x4", "Ice_Edge_4_A", "Ice_Edge_4_B", "Ice_Corner", "Floe_A", "Floe_B", "Floe_C",
                          "Crystal_Cluster_A", "Crystal_Cluster_B", "Crystal_Cluster_C", "Island_Underside_16"]):
    pieces[name].location = (-30 + i * 7.5 if i < 10 else 0, -34 if i < 10 else -60, 0)
pod.location = (-6, -20, 0); crown.location = (-1.5, -20, 0); pick.location = (1.5, -20, 0.6)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "IceArena.blend"))
print("MAKE_WORLD DONE")
