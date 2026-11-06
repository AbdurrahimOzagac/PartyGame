# FBX export for Unity 6 (settings verified in a throwaway Unity 6000.3.9f1 project, see REFERENCE notes):
# FBX_SCALE_ALL (no 100x), -Z forward / Y up (Blender -Y front -> Unity +Z), no leaf bones, all actions baked.
import bpy, os, shutil

UNITY = "/Users/ezelbayraktar/Documents/Development/PartyGame/Assets/ShooterParty/MiniGames/KnockbackArena"
HERE = "/Users/ezelbayraktar/Documents/Development/PartyGame/projects/game_assets/knockback_arena/blender/v2"

COMMON = dict(apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
              use_space_transform=True, bake_space_transform=False, add_leaf_bones=False,
              primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
              mesh_smooth_type='FACE', use_mesh_modifiers=True, use_selection=True, use_custom_props=False,
              path_mode='STRIP', embed_textures=False)

def select_only(objs):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]

def export_character(rig, ob, path):
    # rest pose + zero face so the bind pose and default blend shape weights are neutral
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    for k in rig.pose.bones["Face"].keys(): rig.pose.bones["Face"][k] = 0.0
    bpy.context.view_layer.update()
    assert all(m.type == 'ARMATURE' for m in ob.modifiers), "non-armature modifiers drop shape keys from FBX"
    # all-actions mode applies EVERY action in the file to the rig: drop render-only preview actions first
    for o in bpy.data.objects:
        if o is not rig and o.animation_data: o.animation_data_clear()
    for a in [a for a in bpy.data.actions if not a.name.startswith("CC_")]: bpy.data.actions.remove(a)
    select_only([rig, ob])
    bpy.ops.export_scene.fbx(filepath=path, object_types={'ARMATURE', 'MESH'},
                             use_armature_deform_only=False,       # keep Root/COG/Muzzle/Face transforms
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
                             bake_anim_step=1.0, bake_anim_simplify_factor=0.0, **COMMON)
    rig.animation_data.action = bpy.data.actions.get("CC_Idle")

def export_blob(blob, path):
    blob.animation_data_clear()
    if blob.data.shape_keys: blob.data.shape_keys.animation_data_clear()
    for kb in blob.data.shape_keys.key_blocks[1:]: kb.value = 0.0
    loc, sc = blob.location.copy(), blob.scale.copy()
    blob.location = (0, 0, 0); blob.scale = (1, 1, 1)
    select_only([blob])
    bpy.ops.export_scene.fbx(filepath=path, object_types={'MESH'}, bake_anim=False, **COMMON)
    blob.location, blob.scale = loc, sc

def export_all():
    os.makedirs(UNITY + "/Models", exist_ok=True); os.makedirs(UNITY + "/Textures", exist_ok=True)
    rig, ob = bpy.data.objects["CubeCritter"], bpy.data.objects["CubeCritter_Mesh"]
    export_character(rig, ob, UNITY + "/Models/CubeCritter.fbx")
    export_blob(bpy.data.objects["SpitBlob"], UNITY + "/Models/SpitBlob.fbx")
    for t in ("T_CC_Blush.png", "T_CC_Emblems.png"):
        shutil.copy(os.path.join(HERE, "textures", t), os.path.join(UNITY, "Textures", t))
    print("exported", sorted(os.listdir(UNITY + "/Models")))
