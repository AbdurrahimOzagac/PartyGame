# Checks every clip frame-by-frame: mutually exclusive face groups stay <= 1, nothing sinks below the ground,
# loops close. Run after the clips are authored. Prints a report; returns the list of problems.
import bpy
import numpy as np

EYES = ("Blink", "EyesHappy", "EyesAngry", "EyesWide", "EyesHurt")
MOUTH = ("MouthOpen", "MouthO", "MouthSmallO", "MouthW", "MouthFrown")

def validate(rig, ob, tol=1.05, ground=-0.01):
    sc = bpy.context.scene; dg = bpy.context.evaluated_depsgraph_get()
    face = rig.pose.bones["Face"]; problems = []
    for act in sorted((a for a in bpy.data.actions if a.name.startswith("CC_")), key=lambda a: a.name):
        rig.animation_data.action = act
        f0, f1 = int(act.frame_start), int(act.frame_end)
        worst = {"eyes": 0.0, "mouth": 0.0, "minz": 9.0}
        first_pose = last_pose = None
        for f in range(f0, f1 + 1):
            sc.frame_set(f)
            e = sum(face[k] for k in EYES); m = sum(face[k] for k in MOUTH)
            worst["eyes"] = max(worst["eyes"], e); worst["mouth"] = max(worst["mouth"], m)
            if e > tol: problems.append(f"{act.name} f{f}: eye keys sum {e:.2f}")
            if m > tol: problems.append(f"{act.name} f{f}: mouth keys sum {m:.2f}")
            me = ob.evaluated_get(dg).to_mesh()
            co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co)
            z = (np.array(ob.matrix_world) @ np.c_[co.reshape(-1, 3), np.ones(len(me.vertices))].T)[2]
            ob.evaluated_get(dg).to_mesh_clear()
            worst["minz"] = min(worst["minz"], float(z.min()))
            pose = [tuple(round(x, 4) for x in pb.matrix_basis.col[c]) for pb in rig.pose.bones for c in range(4)]
            if f == f0: first_pose = pose
            last_pose = pose
        if worst["minz"] < ground: problems.append(f"{act.name}: mesh dips to z={worst['minz']:.3f}")
        if act.get("cc_loop") and first_pose != last_pose: problems.append(f"{act.name}: loop does not close")
        print(f"{act.name:16s} frames {f0}-{f1} eyes<= {worst['eyes']:.2f} mouth<= {worst['mouth']:.2f} minz {worst['minz']:.3f}")
    rig.animation_data.action = None
    print("PROBLEMS:" if problems else "ALL CLIPS OK", *problems[:40], sep="\n  ")
    return problems
