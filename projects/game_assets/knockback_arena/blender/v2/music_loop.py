# Turns a generated music clip (Higgsfield Seed Audio, ~16 s) into a seamless loop for Unity:
# estimates the tempo from the onset envelope, cuts to a whole number of bars, folds the tail that rings past
# the cut back over the start (so the loop point has no gap or click), and sets the level.
#   python3.13 music_loop.py <in.wav> <out.wav> [bpm_min bpm_max]   (Blender's python, numpy only)
import sys, wave
import numpy as np


def read(path):
    with wave.open(path) as w:
        sr, ch, n = w.getframerate(), w.getnchannels(), w.getnframes()
        a = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float64) / 32768
    return a.reshape(-1, ch), sr


def write(path, x, sr):
    y = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(y.shape[1]); w.setsampwidth(2); w.setframerate(sr); w.writeframes(y.tobytes())


def tempo(x, sr, lo=80, hi=140):
    """BPM from the autocorrelation of the spectral-flux onset envelope (10 ms hops)."""
    m = x.mean(1); hop = sr // 100; win = 2048
    frames = np.lib.stride_tricks.sliding_window_view(m, win)[::hop] * np.hanning(win)
    mag = np.abs(np.fft.rfft(frames, axis=1))
    flux = np.maximum(np.diff(mag, axis=0), 0).sum(1)
    flux -= flux.mean()
    ac = np.correlate(flux, flux, "full")[len(flux) - 1:]
    lags = np.arange(len(ac)) / 100.0                       # seconds
    ok = np.where((lags > 60 / hi) & (lags < 60 / lo))[0]
    i = ok[np.argmax(ac[ok])]
    d = (ac[i - 1] - ac[i + 1]) / (2 * (ac[i - 1] - 2 * ac[i] + ac[i + 1]))   # parabolic peak: sub-hop precision
    return 60 / ((i + d) / 100.0)


def make_loop(x, sr, bpm, fold=0.35, rms_db=-20.0, min_fold=0.08):
    bar = 4 * 60 / bpm
    fit = int((len(x) / sr - min_fold) // bar)
    bars = fit - fit % 4 if fit >= 4 else fit                  # keep whole 4-bar phrases so harmony lines up
    L = int(round(bars * bar * sr))
    F = min(int(fold * sr), len(x) - L)
    out = x[:L].copy()
    fade = np.linspace(1, 0, F)[:, None] ** 2
    out[:F] = out[:F] * (1 - fade) + x[L:L + F] * fade           # the ring-out over the cut plays at the loop start
    rms = np.sqrt((out ** 2).mean())
    out *= 10 ** (rms_db / 20) / max(rms, 1e-9)
    peak = np.abs(out).max()
    if peak > 10 ** (-1 / 20): out *= 10 ** (-1 / 20) / peak   # peak <= -1 dBFS
    return out, bars


if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    lo, hi = (float(sys.argv[3]), float(sys.argv[4])) if len(sys.argv) > 4 else (80, 140)
    x, sr = read(src)
    bpm = tempo(x, sr, lo, hi)
    y, bars = make_loop(x, sr, bpm)
    # self-check: the seam (end -> start) is no louder a jump than a typical sample step
    seam = np.abs(y[0] - y[-1]).max(); typical = np.abs(np.diff(y, axis=0)).mean(0).max() * 8
    assert seam < max(typical, 0.02), (seam, typical)
    write(dst, y, sr)
    print(f"{src} -> {dst}: {bpm:.1f} BPM, {bars} bars, {len(y) / sr:.2f} s")
