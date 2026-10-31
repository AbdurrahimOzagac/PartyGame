# Procedural 2D textures for VFX and UI (white / greyscale so Unity tints them; straight alpha).
#   /Applications/Blender.app/Contents/Resources/5.2/python/bin/python3.13 sprites.py
# Writes into the Unity project: Sprites/ (particle + decal textures) and UI/Sprites/ (uGUI).
import os, math
import numpy as np
from img import save_png, uv, smooth, sd_circle, sd_box, sd_star, sd_polygon

UNITY = "/Users/ezelbayraktar/Documents/Development/PartyGame/Assets/ShooterParty/MiniGames/KnockbackArena"
FX = os.path.join(UNITY, "Sprites")
UI = os.path.join(UNITY, "UI", "Sprites")
SS = 4   # supersampling


def render(n, fn, m=None):
    """fn(x, y, px) -> rgba at SS x resolution; box-downsampled for anti-aliasing. x, y in [-1, 1] (aspect kept)."""
    m = m or n
    x, y = uv(n * SS, m * SS)
    if m != n: y = y * (m / n)
    img = fn(x, y, 2.0 / (n * SS))
    h, w = m, n
    img = img.reshape(h, SS, w, SS, -1).mean(axis=(1, 3))
    # un-premultiply the box filter on colour so edges don't darken
    a = img[..., 3:4]
    rgb = np.where(a > 1e-4, img[..., :3] / np.maximum(a, 1e-4), 1.0)
    return np.concatenate([rgb * 1.0, a], -1)


def premul(rgb, a):
    """Premultiplied rgba for the supersampled buffer; render() divides alpha back out (output is straight alpha)."""
    rgb = np.asarray(rgb, dtype=np.float64)
    if rgb.ndim == a.ndim: rgb = rgb[..., None]           # greyscale shade -> rgb
    rgb = np.broadcast_to(rgb, a.shape + (3,))
    return np.concatenate([rgb * a[..., None], a[..., None]], -1)


def hard(d): return (d < 0).astype(np.float64)


def soft(d, w): return np.clip(0.5 - d / w, 0, 1)


# ---------------------------------------------------------------- particle sprites
def puff(x, y, px):
    # cloud puff: union of lobes, soft rim, greyscale shading (lighter top-left) so a tint still reads as volume
    lobes = [(0.0, -0.05, 0.55), (-0.38, -0.12, 0.38), (0.38, -0.1, 0.4), (-0.12, 0.3, 0.38), (0.2, 0.25, 0.36)]
    d = np.min([sd_circle(x - cx, y - cy, r) for cx, cy, r in lobes], axis=0)
    a = soft(d + 0.03, 0.16)
    shade = 0.78 + 0.22 * smooth(-0.8, 0.6, y - 0.4 * x)
    return premul(shade, a)


def star4(x, y, px):
    # 4-point twinkle: thin long rays + soft core glow
    rays = np.minimum(np.abs(x) * (1 + 6 * np.abs(y)), np.abs(y) * (1 + 6 * np.abs(x)))
    ray = np.clip(1 - rays / 0.06, 0, 1) * np.clip(1 - np.hypot(x, y), 0, 1) ** 1.4
    glow = np.exp(-(x * x + y * y) / 0.025)
    return premul(1.0, np.clip(ray + glow, 0, 1))


def sparkle(x, y, px):
    st = sd_star(x, y, 0.95, n=4, inner=0.16)
    a = soft(st, 0.05) + 0.6 * np.exp(-(x * x + y * y) / 0.05)
    return premul(1.0, np.clip(a, 0, 1))


def droplet(x, y, px):
    r = np.hypot(x, y)
    a = soft(r - 0.86, px * 3)
    hl = np.exp(-((x + 0.3) ** 2 + (y - 0.32) ** 2) / 0.02)
    shade = 0.8 + 0.2 * smooth(-0.9, 0.9, y) + hl * 0.5
    return premul(np.clip(shade, 0, 1), a)


def ring(x, y, px):
    r = np.hypot(x, y)
    a = np.exp(-((r - 0.78) / 0.09) ** 2) * soft(r - 0.97, 0.04)
    return premul(1.0, a)


def speedline(x, y, px):
    # horizontal streak, bright head on the right, tapering tail on the left
    t = (x + 1) / 2
    width = 0.08 + 0.55 * t ** 1.5
    a = np.clip(1 - np.abs(y) / width, 0, 1) ** 1.5 * smooth(0.0, 0.25, t) * smooth(1.0, 0.93, t)
    return premul(1.0, a)


def starburst(x, y, px):
    # impact splash: chunky rounded 8-point star with a hole-free core + inner shade
    d = sd_star(x, y, 0.95, n=8, inner=0.55, round_=0.02)
    a = hard(d)
    shade = 0.86 + 0.14 * smooth(0.0, 0.8, np.hypot(x, y))
    return premul(shade, a)


def shadow(x, y, px):
    r = np.hypot(x, y)
    return premul(0.0, np.clip(1 - r, 0, 1) ** 1.6)


def splat(x, y, px):
    # goo splat decal: blobby centre, rounded lobes, a few satellite droplets, glossy highlight. Tinted per player.
    ang = np.arctan2(y, x); r = np.hypot(x, y)
    lob = 0.52 + 0.07 * np.sin(ang * 7 + 0.4) + 0.05 * np.sin(ang * 3 + 1.3) + 0.03 * np.sin(ang * 11)
    d = r - lob
    rng = np.random.default_rng(5)
    for k in range(7):
        a0 = k / 7 * math.tau + rng.uniform(-0.3, 0.3); rr = rng.uniform(0.68, 0.86); s = rng.uniform(0.04, 0.09)
        d = np.minimum(d, sd_circle(x - math.cos(a0) * rr, y - math.sin(a0) * rr, s))
    a = hard(d)
    hl = np.exp(-((x + 0.18) ** 2 + (y - 0.2) ** 2) / 0.012) + 0.5 * np.exp(-((x - 0.1) ** 2 + (y - 0.32) ** 2) / 0.003)
    rim = smooth(-0.08, 0.0, d)
    shade = 0.84 + 0.12 * smooth(-0.6, 0.6, y) - 0.12 * rim + 0.6 * hl
    return premul(np.clip(shade, 0, 1), a)


def glow(x, y, px):
    return premul(1.0, np.exp(-(x * x + y * y) / 0.18) * smooth(1.0, 0.85, np.hypot(x, y)))


def star5(x, y, px):
    # chunky rounded 5-point star (fall-out poof, pickup collect), point up, soft inner shading
    d = sd_star(y + 0.04, -x, 0.95, n=5, inner=0.64, round_=0.05)   # swapped axes: first point straight up
    a = soft(d, px * 1.5)
    shade = 0.88 + 0.12 * smooth(-0.6, 0.7, y)
    return premul(shade, a)


def heart(x, y, px):
    yy = -y * 1.1 + 0.25; xx = np.abs(x) * 1.15
    d = np.hypot(xx - 0.38, yy + 0.18) - 0.42
    d = np.minimum(d, np.maximum(yy - 0.75 + xx * 1.1, -yy - 0.15 + xx * 0.2) * 0.7)
    return premul(1.0, hard(d))


def confetti_cell(k, x, y):
    # 8 shapes for a 4x2 sheet; mild shading so flips read in 3D
    if k == 0: d = sd_box(x, y, 0.62, 0.32, 0.06)
    elif k == 1: d = sd_circle(x, y, 0.55)
    elif k == 2: d = sd_star(x, y, 0.85, n=5, inner=0.45, round_=0.02)
    elif k == 3:
        yy = -y * 1.1 + 0.2; xx = np.abs(x) * 1.2
        d = np.minimum(np.hypot(xx - 0.33, yy + 0.15) - 0.36, np.maximum(yy - 0.65 + xx * 1.1, -yy - 0.1 + xx * 0.2) * 0.7)
    elif k == 4: d = np.maximum(np.abs(x) * 0.87 + y * 0.5, -y) - 0.45
    elif k == 5: d = np.abs(y - 0.22 * np.sin(x * 6)) - 0.13 + 0.0 * x; d = np.maximum(d, np.abs(x) - 0.75)
    elif k == 6: d = np.abs(np.hypot(x, y) - 0.48) - 0.14
    else: d = (np.abs(x) + np.abs(y) * 0.7) - 0.6
    return d


def confetti(x, y, px):
    # x, y cover a 4x2 atlas in [-1,1] x [-0.5,0.5]: map to cells
    cx = np.floor((x + 1) * 2).clip(0, 3); cy = np.floor((0.5 - y) * 2).clip(0, 1)
    lx = ((x + 1) * 2 - cx) * 2 - 1; ly = 1 - ((0.5 - y) * 2 - cy) * 2
    out = np.zeros(x.shape + (4,))
    for k in range(8):
        m = (cx == k % 4) & (cy == k // 4)
        d = confetti_cell(k, lx * 1.1, ly * 1.1)
        a = hard(d) * m
        shade = 0.85 + 0.15 * smooth(-0.8, 0.8, ly)
        out += premul(shade, a)
    return out


def ice_chips(x, y, px):
    # 4x1 atlas of angular ice shards: convex, elongated, two-tone facets, white edge highlight
    cx = np.floor((x + 1) * 2).clip(0, 3)
    lx = ((x + 1) * 2 - cx) * 2 - 1; ly = y * 4
    rng = np.random.default_rng(9)
    out = np.zeros(x.shape + (4,))
    for k in range(4):
        n = 5 + k % 2; rot = rng.uniform(0, math.pi); el = rng.uniform(0.45, 0.65)
        angs = np.arange(n) / n * math.tau + rng.uniform(-0.25, 0.25, n)
        pts = [(math.cos(a) * r, math.sin(a) * r * el) for a, r in zip(angs, rng.uniform(0.72, 0.9, n))]
        pts = [(px_ * math.cos(rot) - py_ * math.sin(rot), px_ * math.sin(rot) + py_ * math.cos(rot)) for px_, py_ in pts]
        d = sd_polygon(lx, ly, pts)
        m = ((cx == k) & (d < 0)).astype(np.float64)
        facet = np.where(lx * math.cos(rot + 1.2) + ly * math.sin(rot + 1.2) > 0, 1.0, 0.76)
        shade = np.where(d > -0.08, 1.0, facet)
        out += premul(shade, m)
    return out


# ---------------------------------------------------------------- UI sprites (uGUI, 9-slice where noted)
def ui_panel(x, y, px, r=0.5):
    # 128 px, corner radius 32 px (r = 0.5 in [-1,1] units) -> 9-slice border 40 px
    return premul(1.0, soft(sd_box(x, y, 1 - px * 2, 1 - px * 2, r), px * 1.5))


def ui_panel_shadow(x, y, px):
    return premul(0.0, 0.55 * smooth(0.18, -0.25, sd_box(x, y, 0.62, 0.62, 0.36)))


def ui_outline(x, y, px):
    d = sd_box(x, y, 1 - px * 2, 1 - px * 2, 0.5)
    return premul(1.0, soft(np.abs(d + 0.06) - 0.06, px * 1.5))


def ui_pill(x, y, px):
    # 128 x 64 pill (meter frame / fill), 9-slice border 32 px left/right
    return premul(1.0, soft(sd_box(x, y, 1 - px * 2, 0.5 - px * 2, 0.5 - px * 2), px * 1.5))


def ui_circle(x, y, px):
    return premul(1.0, soft(sd_circle(x, y, 1 - px * 2), px * 1.5))


def ui_ring(x, y, px, w=0.22):
    return premul(1.0, soft(np.abs(sd_circle(x, y, 1 - w / 2 - px * 2)) - w / 2, px * 1.5))


def ui_crosshair(x, y, px):
    # cute crosshair: centre dot + four rounded ticks, white with a dark outline baked in for any background
    dot = sd_circle(x, y, 0.09)
    ticks = np.min([sd_box(x - dx, y - dy, hx, hy, 0.06) for dx, dy, hx, hy in
                    [(0, 0.55, 0.06, 0.16), (0, -0.55, 0.06, 0.16), (0.55, 0, 0.16, 0.06), (-0.55, 0, 0.16, 0.06)]], axis=0)
    d = np.minimum(dot, ticks)
    fill = soft(d, px * 1.5); line = soft(d - 0.06, px * 1.5)
    a = np.maximum(fill, line * 0.75)
    col = np.clip(fill / np.maximum(a, 1e-4), 0, 1)
    shade = 0.1 + 0.9 * col
    return premul(shade, a)


def icon_weight(x, y, px):
    # kettlebell: mass icon
    body = sd_circle(x, y + 0.18, 0.62)
    handle = np.abs(sd_box(x, y - 0.5, 0.36, 0.3, 0.3)) - 0.11
    d = np.minimum(body, np.maximum(handle, -(y - 0.35)))
    return premul(1.0, soft(d, px * 1.5))


def icon_spring(x, y, px):
    # jump power-up: cartoon spring (three stacked flat rings) under an up arrow, on a base plate
    d = np.full(x.shape, 9.0)
    for k in range(3):
        cy = -0.62 + k * 0.3
        e = np.hypot(x / 0.55, (y - cy) / 0.16) - 1
        d = np.minimum(d, np.abs(e * 0.16) - 0.055)
    d = np.minimum(d, sd_box(x, y + 0.86, 0.62, 0.08, 0.06))
    d = np.minimum(d, sd_polygon(x, y, [(-0.5, 0.25), (0.5, 0.25), (0, 0.95)]) - 0.03)
    return premul(1.0, soft(d, px * 1.5))


def icon_dash(x, y, px):
    # three speed streaks + chevron
    d = np.min([sd_box(x + 0.25 + 0.15 * abs(k), y - k * 0.42, 0.42 - 0.08 * abs(k), 0.07, 0.07) for k in (-1, 0, 1)], axis=0)
    chev = np.abs(np.abs(y) * 0.9 + (x - 0.75)) - 0.1
    chev = np.maximum(chev, np.abs(y) - 0.55)
    return premul(1.0, soft(np.minimum(d, chev), px * 1.5))


def icon_crown(x, y, px):
    # three-point crown with ball tips on a rounded band
    body = sd_polygon(x, y, [(-0.78, -0.5), (0.78, -0.5), (0.78, 0.32), (0.4, -0.02), (0.0, 0.5), (-0.4, -0.02), (-0.78, 0.32)]) - 0.04
    band = sd_box(x, y + 0.6, 0.82, 0.16, 0.08)
    balls = np.min([sd_circle(x - bx, y - by, 0.13) for bx, by in [(-0.78, 0.42), (0.0, 0.62), (0.78, 0.42)]], axis=0)
    return premul(1.0, soft(np.minimum(np.minimum(body, band), balls), px * 1.5))


def icon_check(x, y, px):
    # rounded tick for READY (never colour alone)
    d = np.minimum(sd_polygon(x, y, [(-0.75, 0.05), (-0.48, 0.32), (-0.15, -0.02), (0.5, 0.65), (0.78, 0.38), (-0.15, -0.55)]), 9) - 0.06
    return premul(1.0, soft(d, px * 1.5))


JOBS = [
    (FX, "T_FX_Puff.png", 256, puff, None), (FX, "T_FX_Star4.png", 256, star4, None),
    (FX, "T_FX_Sparkle.png", 128, sparkle, None), (FX, "T_FX_Droplet.png", 128, droplet, None),
    (FX, "T_FX_Ring.png", 256, ring, None), (FX, "T_FX_SpeedLine.png", 256, speedline, 64),
    (FX, "T_FX_StarBurst.png", 256, starburst, None), (FX, "T_FX_BlobShadow.png", 128, shadow, None),
    (FX, "T_FX_Splat.png", 512, splat, None), (FX, "T_FX_Heart.png", 128, heart, None),
    (FX, "T_FX_Glow.png", 128, glow, None), (FX, "T_FX_Star5.png", 128, star5, None),
    (FX, "T_FX_Confetti.png", 512, confetti, 256), (FX, "T_FX_IceChips.png", 512, ice_chips, 128),
    (UI, "UI_Panel.png", 128, ui_panel, None), (UI, "UI_PanelShadow.png", 128, ui_panel_shadow, None),
    (UI, "UI_PanelOutline.png", 128, ui_outline, None), (UI, "UI_Pill.png", 128, ui_pill, 64),
    (UI, "UI_Circle.png", 128, ui_circle, None), (UI, "UI_Ring.png", 256, ui_ring, None),
    (UI, "UI_Crosshair.png", 128, ui_crosshair, None), (UI, "UI_IconWeight.png", 128, icon_weight, None),
    (UI, "UI_IconSpring.png", 128, icon_spring, None), (UI, "UI_IconDash.png", 128, icon_dash, None),
    (UI, "UI_IconCrown.png", 128, icon_crown, None), (UI, "UI_IconCheck.png", 128, icon_check, None),
]

if __name__ == "__main__":
    for d in (FX, UI): os.makedirs(d, exist_ok=True)
    for folder, name, n, fn, m in JOBS:
        img = render(n, fn, m)
        assert img.shape[:2] == ((m or n), n) and img[..., 3].max() > 0.5, name   # self-check: right size, visible
        save_png(os.path.join(folder, name), img)
        print("wrote", name, img.shape)
