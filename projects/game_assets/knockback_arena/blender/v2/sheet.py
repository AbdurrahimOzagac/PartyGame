# Contact-sheet helper: render a list of states from a camera, tile them with ffmpeg.
import bpy, os, subprocess
from mathutils import Vector

def face_cam(name="Cam_Face", loc=(0.0, -2.2, 0.62), target=(0, 0, 0.52), lens=85):
    cam = bpy.data.objects.get(name)
    if not cam:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        bpy.data.collections["Stage"].objects.link(cam)
    cam.data.lens = lens
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return cam

def render_states(states, out_dir, cam, res=(420, 340), cols=4, apply=None, samples=16):
    """states: list of (label, dict) ; apply(dict) sets the scene state."""
    sc = bpy.context.scene
    os.makedirs(out_dir, exist_ok=True)
    old = (sc.camera, sc.render.resolution_x, sc.render.resolution_y, sc.render.filepath, sc.eevee.taa_render_samples)
    sc.camera = cam; sc.render.resolution_x, sc.render.resolution_y = res
    sc.eevee.taa_render_samples = samples
    paths = []
    r = sc.render
    stamp_flags = [a for a in dir(r) if a.startswith("use_stamp_") and isinstance(getattr(r, a), bool)]
    saved = {a: getattr(r, a) for a in stamp_flags + ["use_stamp"]}
    for a in stamp_flags: setattr(r, a, False)
    r.use_stamp = True; r.use_stamp_note = True; r.stamp_font_size = 18
    for i, (label, st) in enumerate(states):
        apply(st)
        r.stamp_note_text = label
        p = os.path.join(out_dir, f"s_{i:02d}.png"); sc.render.filepath = p
        bpy.ops.render.render(write_still=True); paths.append((p, label))
    for a, v in saved.items(): setattr(r, a, v)
    sc.camera, sc.render.resolution_x, sc.render.resolution_y, sc.render.filepath, sc.eevee.taa_render_samples = old
    # tile with labels
    n = len(paths); rows = (n + cols - 1) // cols
    args = ["/opt/homebrew/bin/ffmpeg", "-y", "-loglevel", "error"]
    for p, _ in paths: args += ["-i", p]
    flt = ""
    for i in range(n):
        flt += f"[{i}:v]null[v{i}];"
    for i in range(n, rows * cols):  # pad
        flt += f"color=c=black:s={res[0]}x{res[1]}:d=1[v{i}];"
    flt += "".join(f"[v{i}]" for i in range(rows * cols)) + f"xstack=inputs={rows*cols}:layout=" + "|".join(
        f"{(i % cols) * res[0]}_{(i // cols) * res[1]}" for i in range(rows * cols)) + ",format=rgb24[out]"
    sheet = os.path.join(out_dir, "sheet.png")
    r = subprocess.run(args + ["-filter_complex", flt, "-map", "[out]", "-frames:v", "1", sheet], capture_output=True, text=True)
    print("sheet", sheet, r.returncode, r.stderr[-400:])
    return sheet
