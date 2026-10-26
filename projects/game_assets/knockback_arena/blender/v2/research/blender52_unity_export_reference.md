# Blender 5.2.2 LTS → Unity 6000.3.9f1: verified bpy reference

Everything here was run headless (`Blender -b --factory-startup --python …`). Unity-side claims were checked by importing the FBX files into a throwaway Unity 6000.3.9f1 project in batchmode (`unity_probe/`, output in `unity_probe_out.txt`). Each claim names the test script it comes from.

## Q2 verdict (bones + blend shapes in one Unity clip)

**Use drivers.** Make one armature Action per clip. Each action keys the bones plus custom properties on a non-deform `Face` bone, and the shape keys are driven by those properties. Export FBX with `bake_anim_use_all_actions=True`.

In Unity, each clip then contains its own bone curves and `blendShape.*` curves (0–100). Verified: `t2_fbx.py drivers` and the Unity probe. The same rig also exports correctly to glTF (ACTIONS mode).

| Setup | FBX in Unity | glTF |
|---|---|---|
| Drivers from bone custom properties | Correct per clip | Correct per clip |
| One 2-slot Action per clip (OBJECT + KEY slots) | Bones correct; **every** clip gets the shape motion of whichever action is assigned to the Key | Only the Key's assigned action |
| Separate Key actions (`Idle_Face`…) | Same failure | Separate `*_Face` animation |
| NLA strips laid out one after another on Rig **and** Key, `use_nla_strips=True` | Correct; clip names `Idle` (no `Rig|`) | n/a |
| Same-named NLA tracks on Rig + Key | n/a | Correct (`NLA_TRACKS` or `ACTIONS` + merge `NLA_TRACK`) |

**Why:** in all-actions mode the FBX exporter only swaps `ob.animation_data.action`. It samples every shape key from the evaluated scene in every take (`export_fbx_bin.py`, `fbx_animations()` / `fbx_animations_do()`). Drivers re-evaluate when the armature action changes; a Key datablock's own action never gets swapped.

```python
face = rig.pose.bones["Face"]           # non-deform bone; dropped by use_armature_deform_only
key = body.data.shape_keys
for s in ("MouthOpen", "Pucker", "CheekPuff", "Blink"):
    face[s] = 0.0
    face.id_properties_ui(s).update(min=0.0, max=1.0)
    drv = key.key_blocks[s].driver_add("value").driver
    drv.type = 'AVERAGE'                 # default is SCRIPTED with expression "1.0"!
    v = drv.variables.new(); v.type = 'SINGLE_PROP'
    v.targets[0].id = rig; v.targets[0].data_path = f'pose.bones["Face"]["{s}"]'
# in each clip action (assigned to rig):
face["MouthOpen"] = 1.0; face.keyframe_insert('["MouthOpen"]', frame=11, group="Face")
```
Key **every** face property in **every** clip. Unkeyed values leak between clips (see gotcha 6).

## Q1 Slotted actions (`t1_actions.py`)

`action.fcurves` **no longer exists** (AttributeError). F-curves live at `action.layers[i].strips[j].channelbag(slot).fcurves`.

```python
from bpy_extras import anim_utils
act = bpy.data.actions.new("Idle"); act.use_fake_user = True
rig.animation_data_create(); rig.animation_data.action = act   # slot is None until the first key
pb = rig.pose.bones["Spine"]
pb.rotation_quaternion = (0.995, 0.0998, 0, 0)
pb.keyframe_insert("rotation_quaternion", frame=21, group="Spine")  # creates layer/strip/slot "OBRig"
pb.keyframe_insert("location", frame=21); pb.keyframe_insert("scale", frame=21)
slot = rig.animation_data.action_slot
cb = anim_utils.action_get_channelbag_for_slot(act, slot)  # == act.layers[0].strips[0].channelbag(slot)
for fc in cb.fcurves:
    for kp in fc.keyframe_points:
        kp.interpolation = 'BEZIER'; kp.easing = 'EASE_IN_OUT'
        kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
    fc.modifiers.new('CYCLES'); fc.update()    # first key == last key -> seamless loop
act.use_frame_range = True; act.frame_start, act.frame_end = 1, 40   # act.frame_range -> (1, 40)
act.use_cyclic = True   # metadata only, adds no modifiers
fc = cb.fcurves.find('pose.bones["Spine"].rotation_quaternion', index=1)
```
Fast path with no pose needed: `s = act.slots.new(id_type='OBJECT', name="Rig")` (gives identifier `OBRig`), then `act.layers.new("Base").strips.new(type='KEYFRAME').channelbags.new(s).fcurves.new(path, index=i, group_name="Head")`, then `keyframe_points.add(n)` + `foreach_set("co", [...])` + `fc.update()`. `act.fcurve_ensure_for_datablock(rig, path, index=2)` also works.

- Assigning an action to a **second** object leaves `action_slot = None` (the slot shows in `action_suitable_slots`), so set `ob.animation_data.action_slot = act.slots["OBRig"]` explicitly.
- NLA: `track.strips.new(name, start, act)` auto-assigns `strip.action_slot`.
- The FBX exporter takes each take's range from `act.frame_range` (code-read, not export-tested). For loops, set `frame_end = last_key - 1`. Unity clip length = (end − start) / fps; verified 41 frames → 1.333 s.

**Shape keys:** `kb.keyframe_insert("value", frame=f)` keys the Key datablock (`mesh.shape_keys.animation_data`), slot `KEKey`. If the Key is assigned the same action as the rig, that action gains a second slot.

## Q3 FBX export for Unity (`t3_opprops.out`, `t5_orient.py`, Unity probe)

```python
bpy.ops.export_scene.fbx(filepath=out, object_types={'ARMATURE', 'MESH'},
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    use_space_transform=True, bake_space_transform=False,
    add_leaf_bones=False, use_armature_deform_only=True, armature_nodetype='NULL',
    primary_bone_axis='Y', secondary_bone_axis='X',
    mesh_smooth_type='FACE', use_mesh_modifiers=True,
    bake_anim=True, bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
```
What Unity 6000.3.9f1 showed:

- **Scale:** `FBX_SCALE_ALL` writes `UnitScaleFactor=100` and gives scale 1 and fileScale 1. The default `FBX_SCALE_NONE` gives scale **100** with fileScale 0.01, so avoid it.
- **Facing:** a character facing Blender **-Y** faces **Unity +Z**. Verified with the MouthOpen delta direction = (0,0,1).
- **Child rotation:** the `Rig` and `Body` children get **270° X**. The prefab root is identity, so this is harmless in practice.
  - `bake_space_transform=True` clears it on `Body` only. `Rig` keeps 270, because the exporter only bakes meshes and empties (`fbx_utils.py:1749`).
  - `axis_forward='Y', axis_up='Z'` plus Unity `ModelImporter.bakeAxisConversion=true` gives all-zero rotations, but the character then faces **-Z**. Modelling it facing +Y would fix that (UNVERIFIED).
- Round-trip back into Blender reproduced the evaluated vertices with 0.000000 error for every config.

All export kwargs and enum values are dumped in `t3_opprops.out`.

## Q4 glTF (`t4_gltf.py`)

`bpy.ops.export_scene.gltf(filepath=…, export_format='GLB', export_animation_mode='ACTIONS'|'ACTIVE_ACTIONS'|'BROADCAST'|'NLA_TRACKS'|'SCENE', export_merge_animation='ACTION'|'NLA_TRACK'|'NONE', export_morph_animation=True)`

In `ACTIONS` mode, only Key actions that are assigned or in the Key's NLA get exported. Correct merged clips come from:
- drivers;
- same-named NLA tracks on Rig and Key;
- a 2-slot action pushed into NLA on both IDs.

glTFast in Unity is UNVERIFIED.

## Q5 EEVEE and rendering (`t6_eevee.py`, rendered 3 PNGs + 1 MP4)

```python
sc.render.engine = 'BLENDER_EEVEE'          # 'BLENDER_EEVEE_NEXT' raises TypeError in 5.2
ee = sc.eevee
ee.use_raytracing = True; ee.ray_tracing_method = 'SCREEN'   # or 'PROBE'
ee.ray_tracing_options.resolution_scale = '1'; ee.ray_tracing_options.use_denoise = True
ee.use_fast_gi = True; ee.fast_gi_method = 'GLOBAL_ILLUMINATION'  # or 'AMBIENT_OCCLUSION_ONLY' (AO replacement)
ee.use_shadows = True; ee.shadow_ray_count = 2; ee.shadow_step_count = 6
ee.taa_render_samples = 64
sc.render.film_transparent = True
im = sc.render.image_settings
im.media_type = 'IMAGE'; im.file_format = 'PNG'; im.color_mode = 'RGBA'; im.color_depth = '8'
sc.render.filepath = "//frames/f_"; bpy.ops.render.render(animation=True)
im.media_type = 'VIDEO'; im.file_format = 'FFMPEG'; im.color_mode = 'RGB'
ff = sc.render.ffmpeg; ff.format = 'MPEG4'; ff.codec = 'H264'
ff.constant_rate_factor = 'HIGH'; ff.ffmpeg_preset = 'GOOD'
```
- `media_type` options: `IMAGE`, `MULTI_LAYER_IMAGE`, `VIDEO`.
- The default view transform is `AgX`.
- There is no bloom setting on `scene.eevee`; use the compositor Glare node (UNVERIFIED).

Principled BSDF inputs in 5.2:
- Base Color, Metallic, Roughness, IOR, Alpha, **Thin Wall** (bool), Normal, Weight, Diffuse Roughness
- Subsurface Weight / Radius / Scale / IOR / Anisotropy
- Specular IOR Level, Specular Tint, Anisotropic, Anisotropic Rotation, Tangent
- Transmission Weight
- Coat Weight / Roughness / IOR / Tint / Normal
- Sheen Weight / Roughness / Tint
- Emission Color, Emission Strength
- Thin Film Thickness, Thin Film IOR

## Q6 Gotchas hit while testing

1. **New shape keys default to `value = 1.0`.** Set `kb.value = 0.0`, otherwise the FBX writes DeformPercent=100 and Unity's default pose has every blend shape fully on.
2. **FBX + `use_mesh_modifiers=True` + any non-armature modifier drops ALL shape keys silently.** This includes the GN "Smooth by Angle" modifier that `bpy.ops.object.shade_auto_smooth()` adds. Use `bpy.ops.object.shade_smooth_by_angle(angle=…)` or `mesh.set_sharp_from_angle(angle=…)` instead: they add no modifier, and the hard normals plus blend shapes export (`t7b_normals.py`).
3. `bpy.ops.object.modifier_apply(modifier="Bevel")` works in `-b` with an active object, or inside `with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):`. On a mesh with shape keys it raises `RuntimeError: Modifier cannot be applied to a mesh with shape keys`, so apply Subdivision/Bevel **before** adding shape keys.
4. Removed: `Mesh.use_auto_smooth`, `auto_smooth_angle`, `calc_normals_split`, `create_normals_split`. Available: `mesh.shade_smooth()` and `mesh.set_sharp_from_angle()`. `Material.use_nodes` is deprecated (removal planned in 6.0); new materials already have a Principled node tree.
5. **`ActionSlot.users()` segfaults** right after `bpy.data.objects.new` + link + action assignment. Call `bpy.context.view_layer.update()` first (`t1_crash_repro2.py`).
6. A bone not keyed by the current action keeps its last evaluated pose, and the FBX all-actions bake writes that leftover into every take. Before export, run `for pb in rig.pose.bones: pb.matrix_basis = Matrix()`, or key every bone in every action.
7. `bpy.ops.wm.read_factory_settings()` invalidates cached `bpy.context.scene` references (ReferenceError).
8. bmesh: `create_uvsphere(bm, u_segments, v_segments, radius)`, `create_cone(radius1, radius2, depth)`, `create_cube(size)`, `bevel(geom, offset, segments, profile, affect='EDGES')` all OK.
9. FBX take names are `Rig|<Action>` in all-actions mode and `<strip name>` in NLA mode. Every action in `bpy.data` that matches the rig gets exported, so delete stale ones.
