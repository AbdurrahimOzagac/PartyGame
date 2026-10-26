# Procedural environment textures for the ice arena (numpy; run with Blender's python like sprites.py):
#   T_Ice_Detail.png  1024 tileable, sRGB: soft cloudy value variation (0.88-1.0) multiplied into the ice colour
#   T_Ice_Mask.png    1024 tileable, LINEAR: R surface frost + fine cracks, G inner (parallaxed) cracks, B sparkle
#   T_Sky_Panorama.png 4096x2048 equirect, sRGB: deep-blue sky, horizon haze = fog colour, puffy cartoon clouds,
#                     a thin cloud bank under the horizon over the deep-blue drop. Unity imports it as a cubemap.
import os
import numpy as np
from img import save_png, tile_noise, tile_voronoi, smooth, hex_rgb

UNITY = "/Users/ezelbayraktar/Documents/Development/PartyGame/Assets/Ezel/KnockbackArena/Textures"


def ice_detail(n=1024):
    v = tile_noise(n, 3, seed=1, octaves=5, gain=0.55)
    v = 0.88 + 0.12 * smooth(0.25, 0.75, v)
    return np.repeat(v[..., None], 3, -1)


def ice_mask(n=1024):
    # R: thin surface cracks (Voronoi borders) + faint frost patches. Kept low so the floor never reads as snow.
    _, edge = tile_voronoi(n, 6, seed=2, warp=0.35)
    width = tile_noise(n, 8, seed=3, octaves=2)
    cracks = np.clip(1 - edge / (0.012 + 0.02 * width), 0, 1) ** 2 * smooth(0.42, 0.55, tile_noise(n, 2, seed=4, octaves=2))
    frost = smooth(0.62, 0.8, tile_noise(n, 4, seed=5, octaves=4)) * 0.14
    r = np.clip(cracks * 0.28 + frost, 0, 1)
    # G: big inner fractures + bubbles, seen "under" the surface through the parallax tap
    _, edge2 = tile_voronoi(n, 3, seed=6, warp=0.3)
    inner = np.clip(1 - edge2 / 0.025, 0, 1) ** 1.5 * smooth(0.35, 0.6, tile_noise(n, 2, seed=8, octaves=2))
    rng = np.random.default_rng(7)
    ys, xs = np.mgrid[0:n, 0:n] / n
    for _ in range(90):   # small round bubbles
        cx, cy, rr = rng.random(), rng.random(), rng.uniform(0.002, 0.006)
        dx = np.abs(xs - cx); dx = np.minimum(dx, 1 - dx); dy = np.abs(ys - cy); dy = np.minimum(dy, 1 - dy)
        inner = np.maximum(inner, np.clip(1 - np.hypot(dx, dy) / rr, 0, 1) * 0.8)
    g = inner
    # B: per-texel random, lightly blurred; the shader multiplies two taps and keeps the top ~0.5 %
    b = rng.random((n, n))
    b = (b + np.roll(b, 1, 0) + np.roll(b, 1, 1) + np.roll(np.roll(b, 1, 0), 1, 1)) / 4
    b = np.clip((b - 0.5) * 2.2 + 0.5, 0, 1)
    return np.stack([r, g, b], -1)


def sky(w=4096, h=2048):
    v = (np.arange(h) + 0.5) / h
    el = (0.5 - v) * 180.0                                   # elevation in degrees, +90 = zenith
    stops = [(-90, "#062E78"), (-25, "#0A47A0"), (-4, "#3F86D8"), (0, "#4A92E2"), (10, "#4A92E2"),
             (30, "#0F5CC4"), (90, "#0B4CB0")]
    els = np.array([s for s, _ in stops]); cols = np.array([hex_rgb(c) for _, c in stops])
    grad = np.stack([np.interp(el, els, cols[:, k]) for k in range(3)], -1)
    img = np.repeat(grad[:, None, :], w, 1)
    # clouds: periodic in azimuth (noise tiles), mapped by elevation bands
    nz = tile_noise(2048, 6, seed=11, octaves=5, gain=0.5)
    nz = np.kron(nz, np.ones((1, 2)))[:, :w]                  # 2048 x 4096
    nz = nz[np.clip(((v * 2048) % 2048).astype(int), 0, 2047)]
    big = tile_noise(2048, 2, seed=12, octaves=2)
    big = np.kron(big, np.ones((1, 2)))[:, :w][np.clip((v * 2048).astype(int), 0, 2047)]
    elc = el[:, None]
    # floating clouds 3-26 deg: soft-edged puffy shapes whose bellies sit at varying heights
    low = 1.0 + 14.0 * np.clip(big - 0.3, 0, 0.6) + 3.0 * (nz - 0.5)
    band = smooth(low, low + 4, elc) * (1 - smooth(16, 28, elc))
    puffs = smooth(0.56, 0.62, nz * 0.7 + big * 0.5) * band
    # thin cloud bank just below the horizon, thinning out over the deep blue drop (keeps falling critters readable)
    top_edge = -2.5 - 5.0 * (nz - 0.5) * 2
    bank = smooth(0, 1.5, top_edge - elc) * smooth(-16, -8, elc) * smooth(0.35, 0.6, nz * 0.6 + big * 0.6)
    cl = np.clip(np.maximum(puffs * 0.85, bank * 0.8), 0, 1)
    # cartoon shading: bright tops, bluish bellies
    shade = np.clip(0.5 + (nz - 0.5) * 2.4 + 0.25, 0, 1)[..., None]
    cloud_col = hex_rgb("#C2D8F2") * (1 - shade) + hex_rgb("#F4F9FF") * shade
    img = img * (1 - cl[..., None]) + cloud_col * cl[..., None]
    # dither to avoid banding in the smooth gradient (8-bit PNG)
    img += (np.random.default_rng(13).random(img.shape) - 0.5) / 255
    return img


if __name__ == "__main__":
    os.makedirs(UNITY, exist_ok=True)
    d = ice_detail(); m = ice_mask(); s = sky()
    # self-check: tileable edges, value ranges, horizon equals the fog colour
    assert abs(d[0] - d[-1]).mean() < 0.02 and 0.85 <= d.min() and d.max() <= 1.0
    assert m[..., 0].mean() < 0.12, "floor frost must stay subtle"
    horizon = s[1024 - 40, :, :].mean(0)                      # ~3.5 deg above the horizon, between clouds mostly
    assert np.abs(horizon - hex_rgb("#4A92E2")).max() < 0.25, horizon
    save_png(os.path.join(UNITY, "T_Ice_Detail.png"), d)
    save_png(os.path.join(UNITY, "T_Ice_Mask.png"), m)
    save_png(os.path.join(UNITY, "T_Sky_Panorama.png"), s)
    print("env textures written", d.shape, m.shape, s.shape)
