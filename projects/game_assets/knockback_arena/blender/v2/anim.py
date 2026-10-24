# Animation authoring for CubeCritter v2: key-pose tables -> one armature action per clip (30 fps).
# Spec convention (character space): loc = (right, up, fwd) m; rot = (pitch+ forward, yaw+ right, roll+ right) deg.
# Arms: rot = (swing+ tip forward, twist, raise+ outward). Feet: rot = (toe+ up, yaw, roll).
# Body "sq" = vertical squash/stretch keeping volume; "st" = stretch along forward. "sk.Name" = face shape key 0..1
# (keyed as a custom prop on the Face bone; drivers feed the shape keys, so FBX bakes per-clip blendShape curves).
# Table frames are 0-based; Blender keys land on frame+1.
import bpy, math
from mathutils import Vector
from bpy_extras import anim_utils

FPS = 30
CHANNELS = ("location", "rotation_euler", "scale")

def to_local(bone, ch, val):
    side = -1 if bone.endswith(".R") else 1
    if ch == "loc":
        r, u, f = val; return "location", (-r, u, f)
    if ch == "rot":
        a, b, c = (math.radians(x) for x in val)
        if bone.startswith("Arm."): return "rotation_euler", (-a, b * side, c * side)
        if bone.startswith("Foot."): return "rotation_euler", (-a, -b, c)
        return "rotation_euler", (a, -b, c)
    if ch == "sq":
        k = 1 / math.sqrt(val); return "scale", (k, val, k)
    if ch == "st":
        k = 1 / math.sqrt(val); return "scale", (k, k, val)
    if ch == "scale": return "scale", tuple(val)
    raise KeyError(ch)

SWAP = {"Arm.L": "Arm.R", "Arm.R": "Arm.L", "Foot.L": "Foot.R", "Foot.R": "Foot.L"}

def mirror_pose(pose):
    """Left/right mirror of a pose dict (used for the second half of cycles)."""
    out = {}
    for k, v in pose.items():
        if k.startswith("sk."): out[k] = v; continue
        bone, ch = k.rsplit(".", 1)
        limb = bone.startswith(("Arm.", "Foot."))
        if ch == "loc": v = (v[0] if limb else -v[0], v[1], v[2])
        elif ch == "rot" and not bone.startswith("Arm."): v = (v[0], -v[1], -v[2])
        out[f"{SWAP.get(bone, bone)}.{ch}"] = v
    return out

def cycle(half, n):
    """half: poses for frames [0, n/2); returns full cycle with the mirrored second half."""
    full = dict(half)
    for f, p in half.items(): full[f + n // 2] = mirror_pose(p)
    return full

FOOT_PIVOT_H, FOOT_REACH = 0.064, 0.16   # foot bone height above the sole; centre -> rounded toe/heel contact

def foot_pivot(pose):
    """Feet rotate about their centre; lift them so the toe (toe-off) or heel (heel strike) stays on the ground."""
    for side in ("L", "R"):
        rot = pose.get(f"Foot.{side}.rot")
        if rot is None: continue
        a = abs(math.radians(rot[0]))
        lift = FOOT_PIVOT_H * (math.cos(a) - 1) + FOOT_REACH * math.sin(a)
        r, u, f = pose.get(f"Foot.{side}.loc", (0, 0, 0))
        pose[f"Foot.{side}.loc"] = (r, u + max(0.0, lift), f)
    return pose

def cbag(act, rig):
    return anim_utils.action_get_channelbag_for_slot(act, rig.animation_data.action_slot)

def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = 'XYZ'
        pb.location = (0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    face = rig.pose.bones["Face"]
    for k in face.keys(): face[k] = 0.0

def author_clip(rig, name, frames, poses, loop=False, linear=(), springs=True):
    """Create action CC_<name>. poses: {frame: {"Bone.ch": value, "sk.Key": weight}}.
    linear: (channel_key, frame) pairs whose key gets LINEAR interpolation (foot plants)."""
    act = bpy.data.actions.get("CC_" + name)
    if act: bpy.data.actions.remove(act)
    act = bpy.data.actions.new("CC_" + name); act.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = act
    reset_pose(rig)
    face = rig.pose.bones["Face"]
    poses = {f: foot_pivot(dict(p)) for f, p in poses.items()}
    # normalise the table to (bone, data path) -> {frame: local value}; "sk.X" -> ("Face", '["X"]')
    tracks = {}
    for f, p in poses.items():
        for k, v in p.items():
            if k.startswith("sk."): tracks.setdefault(("Face", f'["{k[3:]}"]'), {})[f] = float(v)
            else:
                bone, ch = k.rsplit(".", 1)
                path, val = to_local(bone, ch, v)
                tracks.setdefault((bone, path), {})[f] = val
    first, last = 0, frames
    rest = lambda path: 0.0 if path.startswith("[") else ((1, 1, 1) if path == "scale" else (0, 0, 0))
    for (bone, path), keys in tracks.items():
        if first not in keys: keys[first] = rest(path)       # otherwise the first key would hold backwards
        if loop: keys[last] = keys[first]                     # seamless loop
        elif last not in keys: keys[last] = rest(path)        # one-shots end at rest unless the table says otherwise
    # complete every clip: untouched transforms + face props also get rest keys, so nothing from a previously
    # played action leaks into this one when the exporter bakes it
    for pb in rig.pose.bones:
        for path in CHANNELS:
            tracks.setdefault((pb.name, path), {first: rest(path), last: rest(path)})
    for prop in face.keys():
        tracks.setdefault(("Face", f'["{prop}"]'), {first: 0.0, last: 0.0})
    for (bone, path), keys in tracks.items():
        pb = rig.pose.bones[bone]
        for f in sorted(keys):
            if path.startswith("["): pb[path[2:-2]] = keys[f]
            else: setattr(pb, path, keys[f])
            pb.keyframe_insert(path, frame=f + 1, group=bone)
    cb = cbag(act, rig)
    lin = {(k, f) for k, f in linear}
    for fc in cb.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'BEZIER'; kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
        if loop: fc.modifiers.new('CYCLES')
    for k, f in lin:
        bone, ch = k.rsplit(".", 1)
        dp = f'pose.bones["{bone}"].' + to_local(bone, ch, (1, 1, 1))[0]
        for fc in cb.fcurves:
            if fc.data_path == dp:
                for kp in fc.keyframe_points:
                    if abs(kp.co.x - (f + 1)) < 0.01: kp.interpolation = 'LINEAR'
    for fc in cb.fcurves: fc.update()
    act.use_frame_range = True
    act.frame_start, act.frame_end = 1, frames + 1
    act["cc_loop"] = loop; act["cc_frames"] = frames
    if springs:
        bake_spring(rig, act, frames, loop, driver_bone="Body", targets=(("Top", 1.0),), k=420.0, c=9.0, g=0.010, clamp=0.16)
        bake_spring(rig, act, frames, loop, driver_bone="Top", targets=(("Curl.1", 0.55), ("Curl.2", 1.0)), k=230.0, c=8.0, g=0.020, clamp=0.45, vert=0.35)
    return act

# ------------------------------------------------------------------ secondary motion
def bake_spring(rig, act, frames, loop, driver_bone, targets, k, c, g, clamp, vert=0.0):
    """Damped angular spring driven by the acceleration of driver_bone's tail (in its own frame).
    The result is ADDED to the targets' authored pitch/roll and keyed on every frame."""
    sc = bpy.context.scene; dt = 1 / FPS
    rig.animation_data.action = act
    n = frames + 1
    reps = 3 if loop else 1
    seq = list(range(1, n)) * reps if loop else list(range(1, n + 1))
    pos, rot = [], []
    for f in seq:
        sc.frame_set(f)
        m = rig.matrix_world @ rig.pose.bones[driver_bone].matrix
        pos.append((rig.matrix_world @ rig.pose.bones[driver_bone].tail).copy()); rot.append(m.to_3x3().normalized())
    ang = Vector((0.0, 0.0)); vel = Vector((0.0, 0.0)); out = []
    for i in range(len(pos)):
        if loop: a = (pos[(i + 1) % len(pos)] - 2 * pos[i] + pos[i - 1]) / (dt * dt)
        else: a = Vector((0, 0, 0)) if i in (0, len(pos) - 1) else (pos[i + 1] - 2 * pos[i] + pos[i - 1]) / (dt * dt)
        la = rot[i].transposed() @ a              # local X = side (+left), local Z = forward
        drive = Vector((la.z - vert * la.y, -la.x)) * g * k   # lag opposite to the acceleration; vertical accel flops the hook
        for _ in range(4):
            h = dt / 4
            acc = -k * ang - c * vel - drive
            vel += acc * h; ang += vel * h
        out.append(ang.copy())
    take = out[-(n - 1):] if loop else out
    # read authored values, add spring, re-key every frame
    for bn, w in targets:
        pb = rig.pose.bones[bn]
        base = []
        for f in range(1, n + 1):
            sc.frame_set(f); base.append(tuple(pb.rotation_euler))
        cb = cbag(act, rig)
        for fc in [fc for fc in cb.fcurves if fc.data_path == f'pose.bones["{bn}"].rotation_euler']:
            cb.fcurves.remove(fc)
        for f in range(1, n + 1):
            sp = take[(f - 1) % len(take)] if loop else take[min(f - 1, len(take) - 1)]
            if not loop and f == n: sp = Vector((0, 0))   # one-shots end at rest
            bx, by, bz = base[f - 1]
            pb.rotation_euler = (bx + max(-clamp, min(clamp, sp.x * w)), by, bz + max(-clamp, min(clamp, sp.y * w)))
            pb.keyframe_insert("rotation_euler", frame=f, group=bn)
        cb = cbag(act, rig)
        for fc in cb.fcurves:
            if fc.data_path == f'pose.bones["{bn}"].rotation_euler':
                if loop:
                    fc.keyframe_points[-1].co.y = fc.keyframe_points[0].co.y
                    fc.modifiers.new('CYCLES')
                fc.update()
