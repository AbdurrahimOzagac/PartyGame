# One-command rebuild of CubeCritter v2 from an empty Blender file:
#   Blender -b --factory-startup --python make_all.py -- [--out path.blend] [--no-export]
# stage -> body + face islands + blend shapes -> materials -> rig + face drivers -> 13 clips -> spit blob
# -> validation (fails loudly) -> save -> FBX export to the Unity project.
import bpy, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = args[args.index("--out") + 1] if "--out" in args else os.path.join(HERE, "CubeCritter_v2.blend")
for f in ("lookdev.py", "build_v2.py", "materials.py", "rig.py", "anim.py", "clips.py", "blob.py", "validate.py", "export.py", "sheet.py"):
    exec(open(os.path.join(HERE, f)).read())

for o in list(bpy.data.objects): bpy.data.objects.remove(o)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
sc = bpy.context.scene
sc.render.fps = 30
sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0

build_stage()
ob = build()
shared, per = build_materials()
apply_player(ob, "Tomato", shared, per)
rig = build_rig(ob)
author_all(rig)
blob = build_blob(body_mat=per["Tomato"]["Body"])
problems = validate(rig, ob)
assert not problems, problems

def into(coll_name, objs):
    c = bpy.data.collections.get(coll_name) or bpy.data.collections.new(coll_name)
    if c.name not in sc.collection.children: sc.collection.children.link(c)
    for o in objs:
        for u in list(o.users_collection): u.objects.unlink(o)
        c.objects.link(o)

into("CubeCritter", [rig, ob])
into("Props", [blob])
rig.data.name, ob.data.name, ob.data.shape_keys.name = "CubeCritter_Rig", "CubeCritter_Mesh", "CubeCritter_Keys"
rig.animation_data.action = bpy.data.actions["CC_Idle"]
sc.frame_start, sc.frame_end = 1, 61
sc.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=out)
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_mainfile()
if "--no-export" not in args:
    export_all()
    rig.animation_data.action = bpy.data.actions["CC_Idle"]
    bpy.ops.wm.save_mainfile()
print("MAKE_ALL DONE ->", out)
