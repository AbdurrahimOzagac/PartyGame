# Lookdev / preview stage. Render-only objects live in the "Stage" collection (never exported).
import bpy, math, os
from mathutils import Vector

HERE = "/Users/ezelbayraktar/Documents/Development/PartyGame/projects/game_assets/knockback_arena/blender/v2"

def get_coll(name):
    c = bpy.data.collections.get(name)
    if not c:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c

def clear_coll(c):
    for o in list(c.objects): bpy.data.objects.remove(o)

def build_stage(ground_color=(0.62, 0.80, 0.98, 1)):
    sc = bpy.context.scene
    st = get_coll("Stage"); clear_coll(st)
    # world: sky HDRI, kept dim so the key light shapes the form
    w = bpy.data.worlds.get("LookdevWorld") or bpy.data.worlds.new("LookdevWorld")
    w.use_nodes = True
    nt = w.node_tree; nt.nodes.clear()
    env = nt.nodes.new("ShaderNodeTexEnvironment")
    env.image = bpy.data.images.load(os.path.join(HERE, "hdri/sunflowers_puresky_2k.hdr"), check_existing=True)
    bg = nt.nodes.new("ShaderNodeBackground"); bg.inputs["Strength"].default_value = 0.8
    out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(env.outputs["Color"], bg.inputs["Color"])
    nt.links.new(bg.outputs["Background"], out.inputs["Surface"])
    sc.world = w
    # ground disc
    bpy.ops.mesh.primitive_circle_add(vertices=96, radius=6, fill_type='NGON', location=(0, 0, 0))
    g = bpy.context.active_object; g.name = "Stage_Ground"
    gm = bpy.data.materials.get("M_Stage_Ground") or bpy.data.materials.new("M_Stage_Ground")
    gm.use_nodes = True
    gb = next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    gb.inputs["Base Color"].default_value = ground_color
    gb.inputs["Roughness"].default_value = 0.55
    g.data.materials.clear(); g.data.materials.append(gm)
    # lights: soft key (front-left-top), cool rim (back-right), warm fill
    def area(name, loc, energy, size, color, target=(0, 0, 0.5)):
        ld = bpy.data.lights.new(name, 'AREA'); ld.energy = energy; ld.size = size; ld.color = color
        ob = bpy.data.objects.new(name, ld); bpy.context.collection.objects.link(ob)
        ob.location = loc
        d = Vector(target) - Vector(loc)
        ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        return ob
    area("Key", (-2.6, -3.2, 3.6), 650, 3.0, (1.0, 0.97, 0.92))
    area("Rim", (2.4, 2.8, 2.6), 520, 1.6, (0.80, 0.90, 1.0))
    area("Fill", (3.2, -2.4, 1.2), 140, 3.5, (1.0, 0.95, 0.90))
    # camera
    cd = bpy.data.cameras.new("Cam"); cd.lens = 70
    cam = bpy.data.objects.new("Cam", cd); bpy.context.collection.objects.link(cam)
    cam.location = (2.2, -3.6, 1.45)
    cam.rotation_euler = (Vector((0, 0, 0.52)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    for o in bpy.context.collection.objects:
        if o.name.startswith(("Key", "Rim", "Fill", "Cam", "Stage_")) and o.name not in st.objects:
            for c in o.users_collection: c.objects.unlink(o)
            st.objects.link(o)
    # render
    try: sc.render.engine = 'BLENDER_EEVEE'
    except TypeError as e: print(e)
    sc.render.resolution_x, sc.render.resolution_y = 1280, 1080
    sc.view_settings.view_transform = 'AgX'
    try: sc.view_settings.look = 'AgX - Medium High Contrast'
    except Exception as e: print("look", e)
    ee = sc.eevee
    for attr, val in (("use_raytracing", True), ("use_shadows", True), ("taa_render_samples", 64)):
        if hasattr(ee, attr): setattr(ee, attr, val)
    return cam
