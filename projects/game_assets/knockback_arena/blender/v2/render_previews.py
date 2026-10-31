# Background preview renders for CubeCritter v2.
#   Blender -b CubeCritter_v2.blend --python render_previews.py -- [what ...]
# what: lineup turntable clips gameplay backs faces (default: all). Output: renders/v2/
import bpy, sys, os, math, subprocess, shutil
from mathutils import Vector

HERE = os.path.dirname(bpy.data.filepath)
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "renders", "v2"))
FFMPEG = "/opt/homebrew/bin/ffmpeg"
for f in ("build_v2.py", "materials.py", "rig.py", "anim.py", "clips.py", "blob.py", "sheet.py"):
    exec(open(os.path.join(HERE, f)).read())

sc = bpy.context.scene
rig, ob = bpy.data.objects["CubeCritter"], bpy.data.objects["CubeCritter_Mesh"]

# ------------------------------------------------------------------ look
def setup_look(bg_top="#62BDFF", bg_bottom="#D8EEFF", ground="#CFE6FA"):
    w = sc.world; nt = w.node_tree
    env = next(n for n in nt.nodes if n.type == "TEX_ENVIRONMENT")
    bg = next(n for n in nt.nodes if n.type == "BACKGROUND")
    out = next(n for n in nt.nodes if n.type == "OUTPUT_WORLD")
    for n in [n for n in nt.nodes if n.name.startswith("CC_")]: nt.nodes.remove(n)
    lp = nt.nodes.new("ShaderNodeLightPath"); lp.name = "CC_lp"
    tc = nt.nodes.new("ShaderNodeTexCoord"); tc.name = "CC_tc"
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); sep.name = "CC_sep"
    ramp = nt.nodes.new("ShaderNodeValToRGB"); ramp.name = "CC_ramp"
    ramp.color_ramp.elements[0].position = 0.25; ramp.color_ramp.elements[0].color = srgb(bg_bottom)
    ramp.color_ramp.elements[1].position = 0.95; ramp.color_ramp.elements[1].color = srgb(bg_top)
    cbg = nt.nodes.new("ShaderNodeBackground"); cbg.name = "CC_camBG"; cbg.inputs["Strength"].default_value = 1.0
    mix = nt.nodes.new("ShaderNodeMixShader"); mix.name = "CC_mix"
    nt.links.new(tc.outputs["Window"], sep.inputs[0]); nt.links.new(sep.outputs["Y"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], cbg.inputs["Color"])
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs["Fac"])
    nt.links.new(bg.outputs["Background"], mix.inputs[1]); nt.links.new(cbg.outputs["Background"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    ramp.color_ramp.elements[0].position = 0.42
    # ground fades radially into the exact background colour: no horizon line ("infinite studio")
    g = bpy.data.objects["Stage_Ground"]; g.scale = (6, 6, 1)
    gm = g.active_material; gnt = gm.node_tree
    gb = next(n for n in gnt.nodes if n.type == "BSDF_PRINCIPLED")
    gb.inputs["Base Color"].default_value = srgb(ground); gb.inputs["Roughness"].default_value = 0.75
    gout = next(n for n in gnt.nodes if n.type == "OUTPUT_MATERIAL")
    for n in [n for n in gnt.nodes if n.name.startswith("CC_")]: gnt.nodes.remove(n)
    gtc = gnt.nodes.new("ShaderNodeTexCoord"); gtc.name = "CC_gtc"
    glen = gnt.nodes.new("ShaderNodeVectorMath"); glen.name = "CC_glen"; glen.operation = 'LENGTH'
    gmr = gnt.nodes.new("ShaderNodeMapRange"); gmr.name = "CC_gmr"
    gmr.inputs["From Min"].default_value = 1.2; gmr.inputs["From Max"].default_value = 3.6   # object space (disc r=6, scale 6)
    gem = gnt.nodes.new("ShaderNodeEmission"); gem.name = "CC_gem"; gem.inputs["Color"].default_value = srgb(bg_bottom)
    gmix = gnt.nodes.new("ShaderNodeMixShader"); gmix.name = "CC_gmix"
    gnt.links.new(gtc.outputs["Object"], glen.inputs[0])
    gnt.links.new(glen.outputs["Value"], gmr.inputs["Value"]); gnt.links.new(gmr.outputs["Result"], gmix.inputs["Fac"])
    gnt.links.new(gb.outputs["BSDF"], gmix.inputs[1]); gnt.links.new(gem.outputs["Emission"], gmix.inputs[2])
    gnt.links.new(gmix.outputs["Shader"], gout.inputs["Surface"])
    for n, e in (("Key", 190), ("Rim", 240), ("Fill", 45)): bpy.data.objects[n].data.energy = e
    ee = sc.eevee
    for a, v in (("use_raytracing", True), ("use_shadows", True), ("taa_render_samples", 48), ("use_fast_gi", True)):
        if hasattr(ee, a): setattr(ee, a, v)
    try: ee.fast_gi_method = 'GLOBAL_ILLUMINATION'
    except Exception: pass
    sc.render.fps = 30
    # Standard keeps the player hexes as authored (closest to Unity URP without tonemapping); AgX muddies chartreuse
    sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'; sc.view_settings.exposure = 0.0

def cam(name, loc, target, lens=50):
    c = face_cam(name, loc=loc, target=target, lens=lens); sc.camera = c; return c

def stamp(text):
    r = sc.render
    for a in [a for a in dir(r) if a.startswith("use_stamp_") and isinstance(getattr(r, a), bool)]: setattr(r, a, False)
    r.use_stamp = bool(text); r.use_stamp_note = bool(text); r.stamp_note_text = text or ""; r.stamp_font_size = 22
    r.stamp_background = (0, 0, 0, 0.35)

def still(path, res):
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.image_settings.media_type = 'IMAGE'; sc.render.image_settings.file_format = 'PNG'
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)

def sequence(name, res, f0, f1, label=None, fps=30):
    """Render frames f0..f1 to PNGs, encode MP4 with ffmpeg."""
    d = os.path.join(OUT, "_frames", name); shutil.rmtree(d, ignore_errors=True); os.makedirs(d)
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.image_settings.media_type = 'IMAGE'; sc.render.image_settings.file_format = 'PNG'
    stamp(label)
    for i, f in enumerate(range(f0, f1 + 1)):
        sc.frame_set(f); sc.render.filepath = os.path.join(d, f"{i:04d}.png")
        bpy.ops.render.render(write_still=True)
    stamp(None)
    mp4 = os.path.join(OUT, name + ".mp4")
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", str(fps), "-i", os.path.join(d, "%04d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", mp4], check=True)
    return mp4

# ------------------------------------------------------------------ players
def clone_player(pname, x, shared, per):
    """Rig + mesh copy with its own drivers and materials (object-linked)."""
    r2 = rig.copy(); r2.data = rig.data; r2.animation_data_clear(); sc.collection.objects.link(r2)
    o2 = ob.copy(); o2.data = ob.data.copy(); sc.collection.objects.link(o2)
    o2.parent = r2; o2.modifiers["Armature"].object = r2
    for fc in o2.data.shape_keys.animation_data.drivers:
        for v in fc.driver.variables: v.targets[0].id = r2
    face = r2.pose.bones["Face"]
    for k in rig.pose.bones["Face"].keys(): face[k] = 0.0
    r2.location.x = x
    for i, slot in enumerate(o2.material_slots):
        slot.link = 'OBJECT'
        slot.material = per[pname].get(MATS[i]) or shared.get(MATS[i])
    return r2, o2

def pose_from(r, clip, frame):
    """Freeze rig r in the pose of clip@frame (copies transforms + face props, no action left on r)."""
    rig.animation_data.action = bpy.data.actions["CC_" + clip]; sc.frame_set(frame)
    vals = {pb.name: (pb.location.copy(), pb.rotation_euler.copy(), pb.scale.copy()) for pb in rig.pose.bones}
    face = {k: rig.pose.bones["Face"][k] for k in rig.pose.bones["Face"].keys()}
    if r.animation_data: r.animation_data.action = None
    for pb in r.pose.bones:
        pb.rotation_mode = 'XYZ'; pb.location, pb.rotation_euler, pb.scale = vals[pb.name]
    for k, v in face.items(): r.pose.bones["Face"][k] = v
    if r is not rig: rig.animation_data.action = None

def main(what):
    os.makedirs(OUT, exist_ok=True)
    setup_look()
    shared, per = build_materials()
    apply_player(ob, "Tomato", shared, per)
    blob = bpy.data.objects.get("SpitBlob") or build_blob(body_mat=per["Tomato"]["Body"])
    for n in ("SpitBlob", "SpitDroplet.0", "SpitDroplet.1"):
        o = bpy.data.objects.get(n)
        if o: o.hide_render = True

    if "lineup" in what or "backs" in what:
        order = [("Tomato", "Spit", 3), ("Teal", "Victory", 30), ("Chartreuse", "Idle", 1), ("Violet", "Taunt", 13)]
        xs = [-2.2, -0.75, 0.72, 2.2]
        clones = []
        for (pn, clip, fr), x in zip(order, xs):
            if pn == "Tomato":
                pose_from(rig, clip, fr); rig.location.x = x; clones.append((rig, ob))
            else:
                r2, o2 = clone_player(pn, x, shared, per); pose_from(r2, clip, fr); clones.append((r2, o2))
        rig.pose.bones["Face"]["EyesHappy"] = 1.0
        bpy.context.view_layer.update()
        if "lineup" in what:
            cam("Cam_Lineup", (1.3, -7.0, 2.1), (0, 0, 0.5), lens=42); still(os.path.join(OUT, "hero_lineup.png"), (1920, 1080))
        if "backs" in what:
            for r, o in clones:
                for pb in r.pose.bones: pb.location, pb.rotation_euler, pb.scale = (0, 0, 0), (0, 0, 0), (1, 1, 1)
                for k in r.pose.bones["Face"].keys(): r.pose.bones["Face"][k] = 0.0
            cam("Cam_Backs", (-1.0, 6.2, 2.1), (0, 0, 0.55), lens=42); still(os.path.join(OUT, "back_emblems.png"), (1920, 1080))
        for r, o in clones[1:]:
            bpy.data.objects.remove(o); bpy.data.objects.remove(r)
        rig.location.x = 0

    if "turntable" in what:
        rig.animation_data.action = bpy.data.actions["CC_Idle"]
        piv = bpy.data.objects.new("CC_Pivot", None); sc.collection.objects.link(piv)
        c = cam("Cam_Turn", (0, -3.3, 1.25), (0, 0, 0.55), lens=50)
        c.parent = piv
        piv.rotation_euler = (0, 0, 0); piv.keyframe_insert("rotation_euler", frame=1)
        piv.rotation_euler = (0, 0, math.radians(360)); piv.keyframe_insert("rotation_euler", frame=121)
        for fc in piv.animation_data.action.layers[0].strips[0].channelbags[0].fcurves:
            for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
        sequence("turntable", (1080, 1080), 1, 120, label=None)
        c.parent = None; bpy.data.objects.remove(piv)

    if "clips" in what:
        c = cam("Cam_Clips", (2.35, -3.7, 1.25), (0, -0.15, 0.55), lens=48)
        for name, fn in CLIPS.items():
            n, poses, loop, _ = fn()
            act = bpy.data.actions["CC_" + name]
            rig.animation_data.action = act
            reps = max(1, round(60 / n)) if loop else 1
            f1 = n * reps + (0 if loop else 20)          # one-shots hold 20 f at the end
            if loop:
                act.use_cyclic = True
            show_blob = name == "Spit"
            for o in ("SpitBlob", "SpitDroplet.0", "SpitDroplet.1"):
                ob_ = bpy.data.objects.get(o)
                if ob_: ob_.hide_render = not show_blob
            if show_blob: preview_flight(rig, blob)
            sequence(f"clip_{name}", (1280, 720), 1, f1, label=f"{name}  ({n} f @ 30 fps{', loop' if loop else ''})")
        for o in ("SpitBlob", "SpitDroplet.0", "SpitDroplet.1"):
            if bpy.data.objects.get(o): bpy.data.objects[o].hide_render = True

    if "gameplay" in what:
        # over-the-shoulder view: what the player actually sees most of the match
        rig.animation_data.action = bpy.data.actions["CC_Run"]
        cam("Cam_OTS", (0.75, 3.1, 1.85), (0.15, -1.2, 0.75), lens=40)
        sequence("gameplay_cam_run", (1280, 720), 1, 48, label="Run, over-the-shoulder camera")
        rig.animation_data.action = bpy.data.actions["CC_Spit"]
        bpy.data.objects["SpitBlob"].hide_render = False
        preview_flight(rig, blob, speed=0.5, frames=22)
        sequence("gameplay_cam_spit", (1280, 720), 1, 30, label="Spit, over-the-shoulder camera")
        bpy.data.objects["SpitBlob"].hide_render = True

    if "faces" in what:
        kbs = ob.data.shape_keys.key_blocks
        rig.animation_data.action = None
        face = rig.pose.bones["Face"]
        def apply(st):
            for k in face.keys(): face[k] = st.get(k, 0.0)
            ob.data.shape_keys.update_tag(); bpy.context.view_layer.update()
        states = [("Neutral", {})] + [(k.name, {k.name: 1.0}) for k in kbs[1:]] + [
            ("Spit charge", {"CheekPuff": 1, "MouthSmallO": 1, "EyesHappy": 1}), ("Ptoo!", {"MouthO": 1, "EyesWide": 0.7}),
            ("Smug", {"EyesHappy": 1, "MouthW": 0.8}), ("Ouch", {"EyesHurt": 1, "MouthO": 0.6}), ("Waaa!", {"EyesWide": 1, "MouthOpen": 1})]
        c = face_cam("Cam_Face", loc=(0.0, -2.2, 0.55), target=(0, 0, 0.47), lens=85)
        res = render_states(states, os.path.join(OUT, "_faces"), c, res=(480, 380), cols=4, apply=apply, samples=24)
        shutil.copy(res, os.path.join(OUT, "expressions.png"))
        apply({})
    if "portraits" in what:
        # HUD portraits: 512 px, transparent, one per player x expression (written straight into the Unity project)
        pdir = "/Users/ezelbayraktar/Documents/Development/PartyGame/Assets/ShooterParty/MiniGames/KnockbackArena/Portraits"
        os.makedirs(pdir, exist_ok=True)
        bpy.data.objects["Stage_Ground"].hide_render = True
        sc.render.film_transparent = True
        exprs = [("Neutral", "Idle", 1, {}), ("Happy", "VictoryLoop", 8, {"EyesHappy": 1, "MouthOpen": 1}),
                 ("Hurt", "Hit", 2, {"EyesHurt": 1, "MouthO": 0.7}), ("Sad", "DefeatLoop", 20, {"EyesWide": 0.35, "MouthFrown": 1})]
        cam("Cam_Portrait", (1.35, -3.9, 1.35), (0.0, 0.0, 0.66), lens=88)
        for pn in PLAYERS:
            apply_player(ob, pn, shared, per)
            for ex, clip, fr, face_set in exprs:
                pose_from(rig, clip, fr)
                for k in rig.pose.bones["Face"].keys(): rig.pose.bones["Face"][k] = face_set.get(k, 0.0)
                bpy.context.view_layer.update()
                still(os.path.join(pdir, f"Portrait_CC_{pn}_{ex}.png"), (512, 512))
        sc.render.film_transparent = False
        bpy.data.objects["Stage_Ground"].hide_render = False
        apply_player(ob, "Tomato", shared, per)
    print("RENDERS DONE ->", OUT)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
main(set(argv) or {"lineup", "backs", "turntable", "clips", "gameplay", "faces", "portraits"})
