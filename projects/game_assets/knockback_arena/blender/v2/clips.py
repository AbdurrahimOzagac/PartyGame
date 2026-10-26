# Clip library for CubeCritter v2 (30 fps, in place). Run after anim.py. Frame 0 = first frame.
# Timing notes come from research_anim/ANIM_SPEC.md (spit releases on frame 4 = 133 ms, run = 12 f @ 2.6 m/s).

def Z(**kw):  # tiny helper: Z(L=..., R=...) not needed; kept tables literal for readability
    return kw

ARMS0 = {"Arm.L.rot": (0, 0, 0), "Arm.R.rot": (0, 0, 0)}

def idle():
    return 60, {
        0:  {"COG.loc": (0, 0, 0), "COG.rot": (0, 0, 0), "Body.sq": 1.0,
             "Arm.L.rot": (0, 0, 0), "Arm.R.rot": (0, 0, 0)},
        15: {"COG.rot": (0, 2, 0.6)},
        30: {"COG.loc": (0, 0.006, 0), "Body.sq": 1.028, "Arm.L.rot": (2, 0, 5), "Arm.R.rot": (2, 0, 5)},
        45: {"COG.rot": (0, -2, -0.6)},
    }, True, ()

def run():
    n = 12
    half = {
        0: {"COG.loc": (-0.020, -0.015, 0), "COG.rot": (10, -4, -3), "Body.sq": 0.97,
            "Foot.L.loc": (0, 0, 0.13), "Foot.L.rot": (0, 0, 0), "Foot.R.loc": (0, 0.06, -0.15), "Foot.R.rot": (-20, 0, 0),
            "Arm.L.rot": (-25, 0, 6), "Arm.R.rot": (25, 0, 6)},
        1: {"COG.loc": (-0.025, -0.035, 0), "COG.rot": (12, -3, -5), "Body.sq": 0.90,
            "Foot.L.loc": (0, 0, 0.043), "Foot.L.rot": (0, 0, 0), "Foot.R.loc": (0, 0.09, -0.06), "Foot.R.rot": (-10, 0, 0),
            "Arm.L.rot": (-30, 0, 16), "Arm.R.rot": (30, 0, 16)},
        2: {"COG.loc": (-0.015, -0.010, 0), "COG.rot": (11, -1, -3), "Body.sq": 0.99,
            "Foot.L.loc": (0, 0, -0.043), "Foot.L.rot": (0, 0, 0), "Foot.R.loc": (0, 0.09, 0.03), "Foot.R.rot": (5, 0, 0),
            "Arm.L.rot": (-15, 0, 8), "Arm.R.rot": (15, 0, 8)},
        3: {"COG.loc": (-0.005, 0.030, 0), "COG.rot": (9, 0, -1), "Body.sq": 1.06,
            "Foot.L.loc": (0, 0.01, -0.13), "Foot.L.rot": (-30, 0, 0), "Foot.R.loc": (0, 0.06, 0.10), "Foot.R.rot": (12, 0, 0),
            "Arm.L.rot": (-5, 0, 4), "Arm.R.rot": (5, 0, 4)},
        4: {"COG.loc": (0.005, 0.045, 0), "COG.rot": (8, 2, 1), "Body.sq": 1.03,
            "Foot.L.loc": (0, 0.03, -0.15), "Foot.L.rot": (-25, 0, 0), "Foot.R.loc": (0, 0.03, 0.14), "Foot.R.rot": (15, 0, 0),
            "Arm.L.rot": (10, 0, 4), "Arm.R.rot": (-10, 0, 4)},
        5: {"COG.loc": (0.015, 0.025, 0), "COG.rot": (9, 3, 2), "Body.sq": 1.00,
            "Foot.L.loc": (0, 0.05, -0.16), "Foot.L.rot": (-22, 0, 0), "Foot.R.loc": (0, 0.01, 0.14), "Foot.R.rot": (6, 0, 0),
            "Arm.L.rot": (20, 0, 5), "Arm.R.rot": (-20, 0, 5)},
    }
    full = cycle(half, n)
    # planted foot slides back at constant speed (no skating): linear keys F0..F3 (L) and F6..F9 (R)
    linear = [("Foot.L.loc", f) for f in (0, 1, 2)] + [("Foot.R.loc", f) for f in (6, 7, 8)]
    return n, full, True, linear

def spit():
    # press -> visible on F0 (cheeks start, ^^ eyes), blob leaves on F4 (133 ms), cancel window from F8
    return 22, {
        0:  {"Body.rot": (-6, 0, 0), "Body.scale": (1.0, 1.04, 1.0), "Arm.L.rot": (-20, 0, 6), "Arm.R.rot": (-20, 0, 6),
             "sk.CheekPuff": 0.55, "sk.MouthSmallO": 0.4, "sk.EyesHappy": 1.0},
        2:  {"Body.rot": (-10, 0, 0), "Body.scale": (1.05, 1.06, 1.05), "Arm.L.rot": (-30, 0, 10), "Arm.R.rot": (-30, 0, 10),
             "sk.CheekPuff": 1.0, "sk.MouthSmallO": 1.0, "sk.EyesHappy": 1.0, "sk.EyesWide": 0.0, "sk.MouthO": 0.0},
        3:  {"Body.rot": (-11, 0, 0), "Body.scale": (1.055, 1.065, 1.055), "sk.EyesHappy": 1.0, "sk.MouthSmallO": 1.0,
             "sk.EyesWide": 0.0, "sk.MouthO": 0.0},
        4:  {"Body.rot": (12, 0, 0), "Body.scale": (0.945, 0.945, 1.12), "Arm.L.rot": (35, 0, 14), "Arm.R.rot": (35, 0, 14),
             "sk.CheekPuff": 0.25, "sk.MouthSmallO": 0.0, "sk.MouthO": 1.0, "sk.EyesHappy": 0.0, "sk.EyesWide": 0.7},
        5:  {"Body.rot": (15, 0, 0), "Body.scale": (0.933, 0.933, 1.15), "sk.CheekPuff": 0.0, "sk.MouthO": 0.75, "sk.EyesWide": 0.5,
             "sk.EyesHappy": 0.0, "sk.MouthW": 0.0},
        7:  {"Body.rot": (-4, 0, 0), "Body.scale": (1.043, 0.92, 1.043), "Arm.L.rot": (10, 0, 6), "Arm.R.rot": (10, 0, 6),
             "sk.MouthO": 0.0, "sk.MouthSmallO": 0.3, "sk.EyesWide": 0.1},
        9:  {"Body.rot": (3, 0, 0), "Body.scale": (0.985, 1.03, 0.985), "Arm.L.rot": (-3, 0, 3), "Arm.R.rot": (-3, 0, 3),
             "sk.MouthSmallO": 0.0, "sk.EyesWide": 0.0, "sk.EyesHappy": 0.0, "sk.MouthW": 0.0, "sk.CheekPuff": 0.0},
        10: {"sk.EyesHappy": 1.0, "sk.MouthW": 0.8, "sk.CheekPuff": 0.15},
        11: {"Body.rot": (-1.5, 0, 0), "Body.scale": (1.005, 0.99, 1.005)},
        16: {"Body.rot": (0.5, 0, 0), "Body.scale": (0.995, 1.01, 0.995), "sk.EyesHappy": 1.0, "sk.MouthW": 0.8, "sk.CheekPuff": 0.15,
             "sk.EyesWide": 0.0},
        18: {"sk.EyesHappy": 0.0, "sk.EyesWide": 0.15},
        22: {"Body.rot": (0, 0, 0), "Body.scale": (1, 1, 1), "Arm.L.rot": (0, 0, 0), "Arm.R.rot": (0, 0, 0),
             "sk.EyesWide": 0.0, "sk.MouthW": 0.0, "sk.CheekPuff": 0.0, "sk.EyesHappy": 0.0},
    }, False, ()

def hit():
    return 20, {
        0:  {"Body.scale": (1.118, 1.118, 0.80), "COG.loc": (0.02, 0, 0), "Arm.L.rot": (0, 0, 40), "Arm.R.rot": (0, 0, 40),
             "sk.EyesHurt": 1.0, "sk.MouthO": 0.6, "COG.rot": (0, 0, 0), "sk.EyesHappy": 0.0, "sk.CheekPuff": 0.0},
        1:  {"COG.loc": (-0.015, 0, 0)},
        2:  {"COG.loc": (0.008, 0, 0), "Body.scale": (1.11, 1.11, 0.81), "sk.EyesHurt": 1.0, "sk.EyesWide": 0.0},
        3:  {"COG.loc": (0, 0, 0), "Body.rot": (-18, 0, 0), "Body.scale": (0.953, 0.953, 1.10), "Arm.L.rot": (-10, 0, 70), "Arm.R.rot": (-10, 0, 70),
             "sk.EyesHurt": 0.0, "sk.EyesWide": 1.0, "sk.MouthO": 0.6},
        5:  {"Body.rot": (-22, 0, 0), "Body.scale": (0.98, 1.04, 0.98)},
        7:  {"Body.rot": (6, 0, 0), "Body.scale": (1.037, 0.93, 1.037), "Arm.L.rot": (0, 0, 30), "Arm.R.rot": (0, 0, 30), "sk.EyesWide": 0.5,
             "sk.EyesHappy": 0.0, "sk.CheekPuff": 0.0},
        9:  {"Body.rot": (-3, 0, 0), "Body.scale": (0.99, 1.02, 0.99), "Arm.L.rot": (0, 0, 20), "Arm.R.rot": (0, 0, 20),
             "sk.EyesWide": 0.0, "sk.MouthO": 0.0, "sk.EyesHappy": 0.8, "sk.CheekPuff": 0.3, "COG.rot": (0, 0, 0)},
        11: {"COG.rot": (0, 12, 2)},
        13: {"COG.rot": (0, -9, -2)},
        15: {"COG.rot": (0, 5, 1), "sk.EyesHappy": 0.8},
        17: {"COG.rot": (0, 0, 0), "Body.rot": (0, 0, 0), "Body.scale": (1, 1, 1)},
        20: {"sk.EyesHappy": 0.0, "sk.CheekPuff": 0.0, "Arm.L.rot": (0, 0, 0), "Arm.R.rot": (0, 0, 0)},
    }, False, [("COG.loc", 0), ("COG.loc", 1), ("COG.loc", 2)]

def fall():
    # airborne / knocked off the edge: flailing arms (4 f period), pedalling feet (12 f), wobble, "waaa!"
    n = 12; p = {}
    for f in range(0, n, 2):
        a = (f // 2) % 2 == 0
        p[f] = {"Arm.L.rot": (15 if a else -10, 0, 118 if a else 62), "Arm.R.rot": (-10 if a else 15, 0, 62 if a else 118),
                "sk.MouthOpen": 1.0 if a else 0.8}
    for f, (lf, lu) in {0: (0.10, 0.06), 3: (0.0, 0.10), 6: (-0.10, 0.06), 9: (0.0, 0.03)}.items():
        p.setdefault(f, {}).update({"Foot.L.loc": (0, lu, lf), "Foot.R.loc": (0, 0.13 - lu, -lf)})
    p[0].update({"COG.rot": (-8, 0, -7), "Body.sq": 1.07, "sk.EyesWide": 1.0, "COG.loc": (0, 0.02, 0)})
    p[3].update({"COG.loc": (0, 0.035, 0), "COG.rot": (-11, 0, 0)})
    p[6].update({"COG.rot": (-8, 0, 7), "Body.sq": 1.04, "COG.loc": (0, 0.02, 0)})
    p[9].update({"COG.loc": (0, 0.005, 0), "COG.rot": (-11, 0, 0)})
    return n, p, True, ()

def land():
    return 10, {
        0:  {"Body.sq": 0.78, "Arm.L.rot": (0, 0, 45), "Arm.R.rot": (0, 0, 45), "COG.loc": (0, -0.03, 0), "sk.EyesHappy": 0.5},
        1:  {"Body.sq": 0.76, "COG.loc": (0, -0.035, 0), "sk.EyesHappy": 0.7, "sk.CheekPuff": 0.4},
        3:  {"Body.sq": 1.08, "COG.loc": (0, 0.01, 0), "Arm.L.rot": (0, 0, 10), "Arm.R.rot": (0, 0, 10), "sk.CheekPuff": 0.0},
        5:  {"Body.sq": 0.97, "COG.loc": (0, 0, 0), "sk.EyesHappy": 0.0},
        7:  {"Body.sq": 1.01, "Arm.L.rot": (0, 0, -3), "Arm.R.rot": (0, 0, -3)},
        10: {"Body.sq": 1.0},
    }, False, ()

def jump():
    return 10, {
        0:  {"Body.sq": 1.0, "COG.loc": (0, 0, 0)},
        3:  {"Body.sq": 0.80, "COG.loc": (0, -0.03, 0), "Arm.L.rot": (-15, 0, -5), "Arm.R.rot": (-15, 0, -5),
             "sk.EyesAngry": 0.6, "sk.CheekPuff": 0.3, "Foot.L.rot": (0, 0, 0), "Foot.R.rot": (0, 0, 0)},
        5:  {"Body.sq": 1.16, "COG.loc": (0, 0.03, 0), "Arm.L.rot": (10, 0, 70), "Arm.R.rot": (10, 0, 70),
             "sk.EyesAngry": 0.0, "sk.EyesWide": 0.6, "sk.CheekPuff": 0.0, "sk.MouthO": 0.5, "sk.MouthOpen": 0.0,
             "Foot.L.rot": (-25, 0, 0), "Foot.R.rot": (-25, 0, 0)},
        10: {"Body.sq": 1.05, "COG.loc": (0, 0.02, 0), "Arm.L.rot": (5, 0, 60), "Arm.R.rot": (5, 0, 60),
             "sk.EyesWide": 0.8, "sk.MouthO": 0.0, "sk.MouthOpen": 0.6,
             "Foot.L.rot": (-10, 0, 0), "Foot.R.rot": (-10, 0, 0), "Foot.L.loc": (0, 0.05, 0.03), "Foot.R.loc": (0, 0.03, -0.03)},
    }, False, ()

def victory():
    up = {"Arm.L.rot": (8, 0, 122), "Arm.R.rot": (8, 0, 122), "sk.EyesHappy": 1.0, "sk.MouthOpen": 1.0}
    return 45, {
        0:  {"Body.sq": 1.0, "COG.loc": (0, 0, 0), "COG.rot": (0, 0, 0)},
        4:  {"Body.sq": 0.85, "COG.loc": (0, -0.02, 0), "Arm.L.rot": (-10, 0, -5), "Arm.R.rot": (-10, 0, -5), "sk.EyesHappy": 0.7},
        5:  {"Body.sq": 1.15, "COG.loc": (0, 0.0, 0), "COG.rot": (0, 0, 0), "Arm.L.rot": (0, 0, 90), "Arm.R.rot": (0, 0, 90), "sk.MouthOpen": 0.8},
        11: {"COG.loc": (0, 0.30, 0), "Body.sq": 1.05, "Foot.L.loc": (0, 0.30, 0), "Foot.R.loc": (0, 0.30, 0)},
        17: {"COG.loc": (0, 0.0, 0), "COG.rot": (0, 360, 0), "Body.sq": 1.08, "Foot.L.loc": (0, 0.0, 0), "Foot.R.loc": (0, 0.0, 0)},
        18: {"Body.sq": 0.82, "Arm.L.rot": (0, 0, 60), "Arm.R.rot": (0, 0, 60)},
        22: {"Body.sq": 1.06},
        26: {"Body.sq": 0.99, **up},
        45: {"Body.sq": 1.0, "COG.rot": (0, 360, 0), "COG.loc": (0, 0, 0), **up},
    }, False, [("COG.loc", 17)]

def victory_loop():
    up = lambda a: {"Arm.L.rot": (8, 0, 122 + a), "Arm.R.rot": (8, 0, 122 - a)}
    return 30, {
        0:  {"COG.loc": (0, 0, 0), "Body.sq": 0.95, **up(0), "sk.EyesHappy": 1.0, "sk.MouthOpen": 1.0, "COG.rot": (0, 0, 0)},
        7:  {"COG.loc": (0, 0.05, 0), "Body.sq": 1.05, **up(15), "COG.rot": (0, 0, 4)},
        15: {"COG.loc": (0, 0, 0), "Body.sq": 0.95, **up(0), "COG.rot": (0, 0, 0)},
        22: {"COG.loc": (0, 0.05, 0), "Body.sq": 1.05, **up(-15), "COG.rot": (0, 0, -4)},
    }, True, [("COG.loc", 0), ("COG.loc", 15)]

def defeat():
    slump = {"Body.scale": (1.03, 0.86, 1.03), "Body.rot": (12, 0, 0), "Arm.L.rot": (5, 0, -10), "Arm.R.rot": (5, 0, -10),
             "sk.Blink": 0.5, "sk.MouthFrown": 1.0}
    return 40, {
        0:  {"sk.EyesWide": 1.0, "Body.sq": 1.04},
        3:  {"sk.EyesWide": 1.0, "sk.Blink": 0.0},
        8:  {**slump, "sk.EyesWide": 0.0, "sk.CheekPuff": 0.0},
        14: {"sk.CheekPuff": 0.0},
        20: {"sk.CheekPuff": 0.6, "Body.scale": (1.04, 0.87, 1.04)},
        28: {"sk.CheekPuff": 0.0, "Body.scale": (1.03, 0.84, 1.03)},
        40: {**slump, "Body.scale": (1.03, 0.85, 1.03)},
    }, False, ()

def defeat_loop():
    base = {"Body.rot": (12, 0, 0), "Arm.L.rot": (5, 0, -10), "Arm.R.rot": (5, 0, -10), "sk.Blink": 0.5, "sk.MouthFrown": 1.0}
    return 60, {
        0:  {**base, "Body.scale": (1.03, 0.85, 1.03), "COG.rot": (0, 0, 0)},
        30: {"Body.scale": (1.025, 0.865, 1.025), "Body.rot": (11, 0, 0), "COG.rot": (0, 0, 1.5)},
    }, True, ()

def taunt():
    # "Puff & Wiggle": puff up, hop-wiggle side to side, then a cheeky "pfft" and a smug blink
    p = {0: {"Body.scale": (1, 1, 1), "COG.rot": (0, 0, 0), "COG.loc": (0, 0, 0),
             "sk.CheekPuff": 0.0, "sk.MouthSmallO": 0.0, "sk.EyesHappy": 0.0},
         6: {"Body.scale": (1.06, 1.06, 1.06), "sk.CheekPuff": 1.0, "sk.MouthSmallO": 0.8, "sk.EyesHappy": 1.0}}
    for i, f in enumerate(range(6, 25, 3)):
        r = 10 if i % 2 == 0 else -10
        p.setdefault(f, {}).update({"COG.rot": (0, 0, r), "COG.loc": (0, 0.03 if i % 2 == 0 else 0.0, 0),
                                    "Arm.L.rot": (0, 0, 60 if i % 2 == 0 else 20), "Arm.R.rot": (0, 0, 20 if i % 2 == 0 else 60)})
    p[24].update({"sk.CheekPuff": 1.0, "sk.MouthSmallO": 0.8, "sk.EyesHappy": 1.0, "sk.MouthOpen": 0.0, "Body.scale": (1.06, 1.06, 1.06)})
    p[26] = {"COG.rot": (0, 0, 0), "COG.loc": (0, 0, 0), "Body.scale": (0.95, 0.95, 1.08), "sk.CheekPuff": 0.0, "sk.MouthSmallO": 0.0,
             "sk.MouthOpen": 0.75, "sk.EyesHappy": 1.0, "Arm.L.rot": (0, 0, 10), "Arm.R.rot": (0, 0, 10), "sk.MouthW": 0.0}
    p[29] = {"sk.MouthOpen": 0.0, "sk.MouthW": 0.8, "Body.scale": (1, 1, 1), "sk.EyesHappy": 1.0, "sk.Blink": 0.0}
    p[31] = {"sk.EyesHappy": 0.0, "sk.Blink": 0.0}
    p[33] = {"sk.Blink": 1.0}
    p[36] = {"sk.Blink": 0.0, "sk.MouthW": 0.0, "Arm.L.rot": (0, 0, 0), "Arm.R.rot": (0, 0, 0)}
    return 36, p, False, ()

def blink():
    return 6, {0: {"sk.Blink": 0.0}, 1: {"sk.Blink": 0.55}, 2: {"sk.Blink": 1.0},
               3: {"sk.Blink": 1.0, "sk.EyesWide": 0.0}, 4: {"sk.Blink": 0.35, "sk.EyesWide": 0.0},
               5: {"sk.Blink": 0.0, "sk.EyesWide": 0.08}, 6: {"sk.EyesWide": 0.0}}, False, ()

CLIPS = {"Idle": idle, "Run": run, "Spit": spit, "Hit": hit, "Fall": fall, "Land": land, "Jump": jump,
         "Victory": victory, "VictoryLoop": victory_loop, "Defeat": defeat, "DefeatLoop": defeat_loop,
         "Taunt": taunt, "Blink": blink}

def author_all(rig, only=None):
    out = {}
    for name, fn in CLIPS.items():
        if only and name not in only: continue
        n, poses, loop, linear = fn()
        out[name] = author_clip(rig, name, n, poses, loop=loop, linear=linear, springs=name not in ("Blink",))
    return out
