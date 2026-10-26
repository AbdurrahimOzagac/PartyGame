# Procedural SFX for Knockback Arena: bubbly, squishy, sparkly, never harsh. Recipes: UI/audio brief, section B.
#   /Applications/Blender.app/Contents/Resources/5.2/python/bin/python3.13 sfx.py
# Writes 44.1 kHz mono 16-bit WAVs into the Unity project (Audio/SFX/, names = sound bank ids) and a review reel
# (wav + mp3) into renders/v2/. Deterministic: every noise source has a fixed seed. Ends with an assert self-check.
import os, wave, subprocess
import numpy as np
from numpy.lib.stride_tricks import sliding_window_view

UNITY = "/Users/ezelbayraktar/Documents/Development/PartyGame/Assets/Ezel/KnockbackArena"
OUT = os.path.join(UNITY, "Audio", "SFX")
RENDERS = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "renders", "v2"))
FFMPEG = "/opt/homebrew/bin/ffmpeg"
SR = 44100
LEAD, TAIL = int(0.0025 * SR), int(0.010 * SR)   # silent head (no onset click) and the brief's 10 ms silent tail
CEIL = 10 ** (-1.2 / 20)                          # limiter ceiling: stays under the -1 dBFS spec after 16-bit rounding
FRAME = int(0.005 * SR)                           # 5 ms frames for gated RMS: short enough for the 30 ms hover


# ---------------------------------------------------------------- building blocks
def db(g): return 10 ** (g / 20)
def tt(n): return np.arange(n) / SR
def phase(f): return 2 * np.pi * (np.cumsum(f) - f) / SR     # exclusive sum: phase 0 on the first sample


def env(n, att, tau, t0=0.0):
    """Linear attack, then exp(-t/tau); silent before t0."""
    t = tt(n) - t0
    return np.where(t < 0, 0.0, np.minimum(np.maximum(t, 0) / att, 1) * np.exp(-np.maximum(t - att, 0) / tau))


def glide(n, f0, f1, dur, t0=0.0):
    """Exponential glide f0 -> f1 over dur (starting at t0), holding the end values."""
    return f0 * (f1 / f0) ** np.clip((tt(n) - t0) / dur, 0, 1)


def saw(f):
    """Band-limited saw (harmonics up to 18 kHz at the top pitch); +pi so it starts at 0 instead of on its jump."""
    ph = phase(f) + np.pi
    return sum(np.sin(k * ph) / k for k in range(1, int(18000 / f.max()) + 1))


def noise(n, seed, color=0):
    """Unit-RMS white (0), pink (1) or brown (2) noise: irfft(rfft(white) / f^(color/2))."""
    w = np.random.default_rng(seed).standard_normal(n)
    if color:
        F = np.fft.rfft(w); f = np.fft.rfftfreq(n, 1 / SR); F[0] = 0; f[0] = 1
        w = np.fft.irfft(F / f ** (color / 2), n)
    return w / np.sqrt(np.mean(w ** 2))


def lp1(x, fc):
    """One-pole low-pass y += a(x - y), a = 1 - e^(-2 pi fc / sr). High-pass = x - lp1(x)."""
    a = 1 - np.exp(-2 * np.pi * fc / SR); y = 0.0; out = []
    for v in x.tolist():
        y += a * (v - y); out.append(y)
    return np.array(out)


def svf(x, fc, q=0.707):
    """Simper/Cytomic SVF, cutoff per sample. Returns (low, band, high); band is k*v1, i.e. 0 dB at the peak."""
    g = np.tan(np.pi * np.minimum(np.broadcast_to(fc, x.shape), 0.45 * SR) / SR)
    k = 1 / q
    a1 = 1 / (1 + g * (g + k)); a2 = g * a1; a3 = g * a2
    ic1 = ic2 = 0.0; lo = []; bp = []
    for v0, b1, b2, b3 in zip(x.tolist(), a1.tolist(), a2.tolist(), a3.tolist()):
        v3 = v0 - ic2; v1 = b1 * ic1 + b2 * v3; v2 = ic2 + b2 * ic1 + b3 * v3
        ic1 = 2 * v1 - ic1; ic2 = 2 * v2 - ic2
        lo.append(v2); bp.append(v1)
    lo = np.array(lo); bp = k * np.array(bp)
    return lo, bp, x - bp - lo


def bubble(n, t0, f0, xi, delta):
    """van den Doel bubble: sin(2 pi int f) e^(-beta t), f = f0 (1 + xi beta t), beta = pi f0 delta."""
    t = np.maximum(tt(n) - t0, 0); beta = np.pi * f0 * delta
    f = np.minimum(f0 * (1 + xi * beta * t), 16000) * (tt(n) >= t0)   # 0 Hz before t0 keeps the phase at 0
    return np.sin(phase(f)) * env(n, 0.0006, 1 / beta, t0)              # 0.6 ms attack: a bare onset clicks


def marimba(n, t0, f, dur=9.0, seed=0):
    """Bar partials x1/x4/x10 (amps 1/.35/.12, tau 350/90/30 ms) + a 2 ms soft noise mallet; damped after dur."""
    t = np.maximum(tt(n) - t0, 0)
    y = sum(a * np.sin(2 * np.pi * m * f * t) * env(n, 0.0005, tau, t0)   # 0.5 ms attack: no full-band onset click
            for m, a, tau in ((1, 1, .35), (4, .35, .09), (10, .12, .03)) if m * f < 18000)
    i, m = int(round(t0 * SR)), int(0.006 * SR)                    # 6 ms buffer so the filter rings out, no cut-off click
    y[i:i + m] += 0.3 * svf(noise(m, seed) * env(m, 0.0002, 0.0005), 4000)[0]   # 2 ms burst, 4 kHz LP: yarn mallet
    return y * np.exp(-np.maximum(t - dur, 0) / 0.08)


def fm(n, t0, f, ratio, i0, tau_i, tau_a, att=0.001):
    """Phase-modulation bell/sparkle: sin(2 pi f t + I e^(-t/tau_i) sin(2 pi ratio f t)) with an attack/decay env."""
    t = np.maximum(tt(n) - t0, 0)
    return np.sin(2 * np.pi * f * t + i0 * np.exp(-t / tau_i) * np.sin(2 * np.pi * ratio * f * t)) * env(n, att, tau_a, t0)


def mix(*layers):
    """Sum (gain dB, signal) layers; each is peak-normalised first so the dB values are layer-to-layer ratios."""
    return sum(db(g) * x / np.max(np.abs(x)) for g, x in layers)


# ---------------------------------------------------------------- loudness + file edges
def gated_rms_db(y):
    """RMS of the 5 ms frames within 40 dB of the loudest one (the brief's 'part above -40 dB')."""
    fr = np.mean(np.pad(y, (0, -len(y) % FRAME)).reshape(-1, FRAME) ** 2, axis=1)
    return 10 * np.log10(np.mean(fr[fr > fr.max() * 1e-4]))


def limit(y):
    """Look-ahead peak limiter: gain = box-average of a windowed min of ceil/|y|, so it never exceeds ceil/|y|."""
    L = int(0.0015 * SR)
    g = np.minimum(1, CEIL / np.maximum(np.abs(y), 1e-9))
    g = sliding_window_view(np.pad(g, L, mode="edge"), 2 * L + 1).min(axis=1)
    g = sliding_window_view(np.pad(g, L, mode="edge"), 2 * L + 1).mean(axis=1)
    return y * g


def finish(x, rms_db, fade):
    x = x - lp1(x, 25)                                                 # 25 Hz high-pass: no DC offset
    a, b = int(0.001 * SR), int(fade * SR)
    x[:a] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(a) / a)              # 1 ms raised-cosine fade-in
    x[-b:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(1, b + 1) / b)      # raised-cosine fade-out, ends at exactly 0
    y = np.concatenate([np.zeros(LEAD), x, np.zeros(TAIL)])
    over = 20 * np.log10(np.max(np.abs(y)) / CEIL) + rms_db - gated_rms_db(y)   # peak overshoot before limiting
    for _ in range(10):                                                # limiting lowers the RMS a bit, so repeat
        y = limit(y * db(rms_db - gated_rms_db(y)))
    return y, max(0.0, over)


def write(path, y):
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(np.round(y * 32767).astype("<i2").tobytes())


def read(path):
    with wave.open(path) as w:
        assert (w.getnchannels(), w.getsampwidth(), w.getframerate()) == (1, 2, SR), path
        return np.frombuffer(w.readframes(w.getnframes()), "<i2") / 32768


# ---------------------------------------------------------------- sounds (each fills a body of n samples)
def spit(n, semi, tm, seed):
    # "ptoo": rising FM-bright bubble + a 3 ms "p" tick at 3.5 kHz + a falling breath of band-passed noise
    t = tt(n); p = 2 ** (semi / 12); w = svf(noise(n, seed), 6000)[0]        # 6 kHz pre-cut: no hiss above the "p"
    ph = phase(glide(n, 520 * p, 1100 * p, 0.06 * tm))
    pop = np.sin(ph + 1.5 * np.exp(-t / (0.015 * tm)) * np.sin(2 * ph)) * env(n, 0.001, 0.035 * tm)
    tick = svf(w * env(n, 0.0003, 0.001), 3500 * p, 2)[1]
    air = svf(w * env(n, 0.003, 0.03 * tm), glide(n, 1800 * p, 600 * p, 0.08 * tm), 3)[1]
    return mix((0, pop), (-14, tick), (-16, air))


def hit(n, semi, cut, seed):
    # squishy jelly thwack: saturated falling thump + closing low-passed splat + wobbling jelly tone + bloops
    t = tt(n); p = 2 ** (semi / 12); rng = np.random.default_rng(seed + 100)
    thump = np.tanh(2 * np.sin(phase(glide(n, 180 * p, 65 * p, 0.08))) * env(n, 0.001, 0.06))
    w = svf(noise(n, seed), 6000)[0]                                          # 6 kHz pre-cut: squishy, not crackly
    splat_ = svf(w * env(n, 0.001, 0.05), glide(n, 4000 * cut, 400 * cut, 0.12), 2)[0]
    wobble = np.sin(phase(230 * p * (1 + 0.25 * np.sin(2 * np.pi * 18 * t)))) * env(n, 0.003, 0.12)
    bloops = sum(bubble(n, rng.uniform(0.02, 0.12), rng.uniform(500, 1200) * p, 1.0, 0.04)
                 for _ in range(rng.integers(3, 6)))
    return mix((0, thump), (-3, splat_), (-8, wobble), (-14, bloops))


def splat(n, semi, seed):
    # wet "plip" on the ice: a quick rising droplet, a smaller second droplet and a soft low-passed smack
    p = 2 ** (semi / 12)
    smack = svf(noise(n, seed) * env(n, 0.0005, 0.008), glide(n, 2500, 700, 0.03), 0.9)[0]
    return mix((0, bubble(n, 0.003, 700 * p, 1.0, 0.03)), (-9, bubble(n, 0.032, 1250 * p, 1.0, 0.03)), (-12, smack))


def blob_pop(n):
    # tiny sparkly pop: a fast high bubble, a pin-prick tick and two glassy FM glints
    tick = svf(noise(n, 81) * env(n, 0.0002, 0.0006), 5000, 1.5)[1]
    glints = fm(n, 0.012, 2637, 2, 1.0, 0.015, 0.03) + 0.7 * fm(n, 0.034, 3136, 2, 1.0, 0.015, 0.025)
    return mix((0, bubble(n, 0.002, 1400, 1.0, 0.05)), (-10, tick), (-9, glints))   # high damping: a pip, not a zap


def whoosh(n):
    # pink noise through a band-pass whose centre rides the amplitude (300 -> 2500 -> 600 Hz), plus an octave band
    t = tt(n); w = noise(n, 91, 1)
    a = np.where(t < 0.12, np.sin(np.pi / 2 * t / 0.12) ** 2, np.exp(-(t - 0.12) / 0.12))
    fc = np.where(t < 0.12, 300 * (2500 / 300) ** a, 600 * (2500 / 600) ** a)
    return (svf(w, fc, 1.5)[1] + db(-9) * svf(w, 2 * fc, 1.5)[1]) * a


def wheee(n):
    # cartoon "wheee": saw voice through /u/ -> /i/ formants, pitch up then falling away, then distance (dark + quiet)
    t = tt(n)
    f = np.where(t < 0.15, 520 * (700 / 520) ** (t / 0.15), 700 * (240 / 700) ** np.clip((t - 0.15) / 0.9, 0, 1))
    src = saw(f * (1 + 0.03 * np.minimum(t / 0.8, 1) * np.sin(2 * np.pi * 6 * t)))
    u = np.minimum(t / 0.12, 1)
    # Q 2/8/10, not 5/12/15: f0 (240-700 Hz) sits above F1, so a narrow F1 passes nothing and narrow F2/F3 whistle
    x = sum(db(g) * svf(src, a * (b / a) ** u, q)[1]
            for a, b, q, g in ((375, 370, 2, 0), (1090, 3200, 8, -6), (2800, 3730, 10, -12)))
    d = np.clip((t - (t[-1] - 0.6)) / 0.6, 0, 1)
    return svf(x, 8000 * (1500 / 8000) ** d, 0.707)[0] * db(-18 * d) * env(n, 0.03, 1e9)


def splash(n):
    # distant splash: dull brown-noise burst + a scatter of real-ish bubbles, low-passed, with one 90 ms echo
    rng = np.random.default_rng(102)
    b = noise(n, 101, 2); b = b - lp1(b, 80)                 # 80 Hz HP: brown noise is mostly sub-bass otherwise
    body = svf(b, 1600)[0] * env(n, 0.008, 0.18)
    bubbles = sum(rng.uniform(0.3, 1) * bubble(n, rng.uniform(0.01, 0.3), rng.uniform(400, 1500), 0.1, 0.02)
                  for _ in range(13))
    x = svf(mix((0, body), (-3, bubbles)), 2000)[0]
    d = int(0.09 * SR)
    return x + 0.3 * np.concatenate([np.zeros(d), x[:-d]])


def land(n):
    # landing squish: saturated low thud, a closing band of noise, and a little suction bloop at +60 ms
    thud = np.tanh(1.5 * np.sin(phase(glide(n, 120, 55, 0.06))) * env(n, 0.002, 0.05))
    squish = svf(svf(noise(n, 111) * env(n, 0.002, 0.07), glide(n, 900, 300, 0.12), 4)[1], 3000)[0]  # LP: BP skirts hiss
    suck = np.sin(phase(glide(n, 300, 600, 0.06, 0.06))) * env(n, 0.003, 0.03, 0.06)
    return mix((0, thud), (-2, squish), (-10, suck))


def step(n, seed, hp):
    # ice/snow crunch: 6-14 tiny high-passed noise grains bunched at the start, 1-2 ice tinks and a soft thump
    rng = np.random.default_rng(seed); t = tt(n); g = np.zeros(n)
    for _ in range(rng.integers(6, 15)):
        i = int(-0.015 * np.log(1 - rng.uniform() * (1 - np.exp(-2))) * SR)   # start ~ e^(-t/15 ms), cut at 30 ms
        m = int(rng.uniform(0.001, 0.003) * SR)
        g[i:i + m] += rng.uniform(0.3, 1) * rng.standard_normal(m) * np.exp(-np.arange(m) / (0.0008 * SR))
    grains = svf(svf(svf(g, hp)[2], 6000)[0], 6000)[0]                        # 6 kHz 4-pole top cut: crunch, not fizz
    tinks = sum(np.sin(2 * np.pi * rng.uniform(3200, 5500) * t) * env(n, 0.0005, 0.008, rng.uniform(0.002, 0.025))
                for _ in range(rng.integers(1, 3)))
    thump = np.sin(2 * np.pi * 150 * t) * env(n, 0.002, 0.012)
    return mix((0, grains), (-16, tinks), (-12, thump))


def beep(n):
    # countdown beep: 880 Hz + soft 2nd/3rd harmonics; hold 120 ms (brief: 140) so a 30 ms release fits in 200 ms
    t = tt(n)
    tone = sum(a * np.sin(2 * np.pi * f * t) for f, a in ((880, 1), (1760, 0.3), (2640, 0.12)))
    return tone * np.minimum(t / 0.003, 1) * np.exp(-np.maximum(t - 0.123, 0) / 0.03)


def go(n):
    # "GO!" horn: A-major chord of detuned saws scooping up a semitone, through a low-pass that blooms then settles
    t = tt(n); scoop = 2 ** ((np.minimum(t / 0.04, 1) - 1) / 12)
    x = sum(saw(f * 2 ** (c / 1200) * scoop) for f in (440, 554.37, 659.25, 880) for c in (-6, 6))
    fc = np.where(t < 0.06, 800 * 5 ** (t / 0.06), 2500 + 1500 * np.exp(-(t - 0.06) / 0.05))
    return svf(x, fc, 0.8)[0] * np.minimum(t / 0.005, 1) * np.exp(-np.maximum(t - 0.455, 0) / 0.06)


def ko(n):
    # light sad trombone: Bb4-A4-Ab4 then a long wobbly G4; each note slides in and gets its own "wah" sweep
    t = tt(n); f = np.zeros(n); fc = np.zeros(n)
    for t0, fn, d in ((0.0, 466.16, 0.18), (0.18, 440.0, 0.18), (0.36, 415.30, 0.18), (0.54, 392.0, 0.6)):
        m = t >= t0; u = t[m] - t0; w = 0.05 if d < 0.3 else 0.08
        f[m] = fn * 2 ** (-0.3 * np.exp(-u / 0.02) / 12)                    # lip slide-in from 30 cents flat
        fc[m] = np.where(u < w, 600 * 4 ** (u / w), 2400 * 0.375 ** np.clip((u - w) / (d - w), 0, 1))
    u = np.maximum(t - 0.54, 0)
    f *= 2 ** (40 / 1200 * np.minimum(u / 0.15, 1) * np.sin(2 * np.pi * 5.5 * u))   # +-40 cent vibrato on the G4
    amp = np.minimum(t / 0.015, 1) * (0.5 + 0.5 * np.cos(np.pi * np.clip((t - 0.94) / 0.2, 0, 1)))
    for tb in (0.18, 0.36, 0.54):
        amp *= 1 - 0.65 * np.exp(-((t - tb) / 0.015) ** 2)                  # smooth dip between notes, no clicks
    return svf(saw(f), fc, 1.8)[0] * amp


def fanfare(n):
    # round-win fanfare: marimba run up to C6, then a warm saw pad and three FM sparkle taps on the final note
    t = tt(n)
    seq = ((0, 392, .1), (.11, 523.25, .1), (.22, 659.25, .1), (.33, 783.99, .24), (.6, 659.25, .12),
           (.74, 783.99, .16), (.92, 1046.5, .9))
    mar = sum(marimba(n, t0, f, d, 120 + i) for i, (t0, f, d) in enumerate(seq))
    pad = sum(saw(np.full(n, f * 2 ** (c / 1200))) for f in (261.63, 329.63, 392.0, 523.25) for c in (-5, 5))
    pad = svf(pad, 2500)[0] * env(n, 0.08, 1.2, 0.92)
    sparkle = sum(fm(n, t0, 2093, 3.5, 2, 0.03, 0.12) for t0 in (0.92, 0.96, 1.0))
    x = mix((0, mar), (-12, pad), (-8, sparkle))
    return x * (0.5 - 0.5 * np.cos(np.pi * np.clip((t[-1] - t) / 0.3, 0, 1)))   # fade the last 300 ms


def hover(n):
    # hover: tiny rising sine blip; glide over 15 ms (brief: 25) as the 30 ms file leaves ~18 ms of sound
    return np.sin(phase(glide(n, 1800, 2200, 0.015))) * env(n, 0.001, 0.006)


def click(n, semi=0):
    # click: struck modes at 700 Hz (body) and 2800 Hz (tick) + a 1 ms 4 kHz-high-passed noise tick
    t = tt(n); p = 2 ** (semi / 12)
    tick = svf(noise(n, 131) * env(n, 0.0002, 0.0004), 4000)[2]
    return mix((0, np.sin(2 * np.pi * 700 * p * t) * env(n, 0.0005, 0.025)),
               (-6, np.sin(2 * np.pi * 2800 * p * t) * env(n, 0.0005, 0.008)), (-10, tick))


def jump(n):
    # springy "boing": a sine bending up 180 -> 560 Hz with a fast 15 Hz wobble that settles, plus a little twang
    t = tt(n)
    ph = phase(glide(n, 180, 560, 0.16) * (1 + 0.15 * np.exp(-t / 0.12) * np.sin(2 * np.pi * 15 * t)))
    return (np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.08 * np.sin(3 * ph)) * env(n, 0.004, 0.11)


def pickup(n):
    # collect chime: quick C6-E6-G6-C7 arpeggio, each note marimba body + glassy FM shimmer; the top note rings
    notes = ((0, 1046.5), (0.055, 1318.51), (0.11, 1567.98), (0.165, 2093.0))
    return sum(marimba(n, t0, f, 9, 140 + i) + 0.6 * fm(n, t0, f, 3.5, 1.5, 0.04, 0.3 if i == 3 else 0.12)
               for i, (t0, f) in enumerate(notes))


def dash_ready(n):
    # dash-charge "ding": a small struck bar at A6 (modes 1 : 2.76 : 5.4), quick and quiet
    t = tt(n)
    return sum(a * np.sin(2 * np.pi * 1760 * m * t) * env(n, 0.001, tau)
               for m, a, tau in ((1, 1, .045), (2.76, .3, .02), (5.4, .1, .008)))


JOBS = [  # (sound bank id, seconds, RMS dBFS target, fade-out s, body generator) in the brief's table order
    ("SFX_Spit_01", .12, -15, .005, lambda n: spit(n, 0, 1.0, 11)),
    ("SFX_Spit_02", .12, -15, .005, lambda n: spit(n, 2, 0.94, 12)),
    ("SFX_Spit_03", .12, -15, .005, lambda n: spit(n, -2, 1.06, 13)),
    ("SFX_Hit_01", .25, -12, .005, lambda n: hit(n, 0, 1.0, 21)),
    ("SFX_Hit_02", .25, -12, .005, lambda n: hit(n, 2, 1.15, 22)),
    ("SFX_Hit_03", .25, -12, .005, lambda n: hit(n, -2, 0.85, 23)),
    ("SFX_Splat_01", .15, -18, .005, lambda n: splat(n, 0, 31)),
    ("SFX_Splat_02", .15, -18, .005, lambda n: splat(n, 1.5, 32)),
    ("SFX_BlobPop_01", .12, -20, .005, blob_pop),
    ("SFX_Whoosh_01", .40, -17, .020, whoosh),
    ("SFX_Wheee_01", 1.2, -17, .020, wheee),
    ("SFX_Splash_01", .70, -22, .020, splash),
    ("SFX_Land_01", .20, -17, .005, land),
    ("SFX_Step_01", .09, -24, .005, lambda n: step(n, 61, 2200)),
    ("SFX_Step_02", .09, -24, .005, lambda n: step(n, 62, 2500)),
    ("SFX_Step_03", .09, -24, .005, lambda n: step(n, 63, 2800)),
    ("SFX_Step_04", .09, -24, .005, lambda n: step(n, 64, 3000)),
    ("SFX_Beep_01", .20, -13, .005, beep),
    ("SFX_Go_01", .75, -13, .020, go),
    ("SFX_KO_01", 1.2, -12, .020, ko),
    ("SFX_Fanfare_01", 2.1, -13, .020, fanfare),
    ("SFX_UiClick_01", .06, -20, .005, click),
    ("SFX_UiHover_01", .03, -28, .005, hover),
    ("SFX_UiBack_01", .06, -20, .005, lambda n: click(n, -4)),
    ("SFX_Jump_01", .35, -15, .005, jump),
    ("SFX_Pickup_01", .50, -14, .020, pickup),
    ("SFX_DashReady_01", .15, -26, .005, dash_ready),
]

if __name__ == "__main__":
    os.makedirs(RENDERS, exist_ok=True)
    reel = []
    for name, dur, rms, fade, fn in JOBS:
        y, shaved = finish(fn(int(round(dur * SR)) - LEAD - TAIL), rms, fade)
        write(os.path.join(OUT, name + ".wav"), y)
        reel += [y, np.zeros(int(0.35 * SR))]
        print(f"wrote {name:18s} limiter -{shaved:.1f} dB")
    write(os.path.join(RENDERS, "sfx_reel.wav"), np.concatenate(reel))
    if os.path.exists(FFMPEG):
        subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-i", os.path.join(RENDERS, "sfx_reel.wav"),
                        "-codec:a", "libmp3lame", "-q:a", "2", os.path.join(RENDERS, "sfx_reel.mp3")], check=True)

    # self-check, on the files as written: peak, loudness, length, silent edges, no DC
    edge = int(0.002 * SR)
    for name, dur, rms, fade, fn in JOBS:
        y = read(os.path.join(OUT, name + ".wav"))
        peak, r = 20 * np.log10(np.max(np.abs(y))), gated_rms_db(y)
        assert peak <= -1.0, (name, peak)
        assert abs(r - rms) <= 1.5, (name, r, rms)
        assert len(y) == int(round(dur * SR)), (name, len(y))
        assert max(np.max(np.abs(y[:edge])), np.max(np.abs(y[-edge:]))) < db(-50), name
        assert abs(np.mean(y)) < db(-60), (name, np.mean(y))
        print(f"ok {name:18s} {dur * 1000:5.0f} ms  RMS {r:6.1f} (target {rms})  peak {peak:5.1f} dBFS")
