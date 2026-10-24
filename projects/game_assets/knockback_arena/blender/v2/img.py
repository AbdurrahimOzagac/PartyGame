# Tiny numpy image toolkit for the procedural 2D assets (sprites, UI, ice textures). No PIL needed:
# run with Blender's bundled python, which has numpy:
#   /Applications/Blender.app/Contents/Resources/5.2/python/bin/python3.13 sprites.py
import struct, zlib
import numpy as np


def save_png(path, img):
    """img: float array HxW (grey), HxWx3 or HxWx4 in 0..1, row 0 = top."""
    a = np.clip(np.asarray(img, dtype=np.float64), 0, 1)
    if a.ndim == 2: a = a[..., None]
    ch = a.shape[2]
    ctype = {1: 0, 2: 4, 3: 2, 4: 6}[ch]
    px = (a * 255 + 0.5).astype(np.uint8)
    raw = b"".join(b"\x00" + px[y].tobytes() for y in range(px.shape[0]))
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    hdr = struct.pack(">IIBBBBB", px.shape[1], px.shape[0], 8, ctype, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", hdr) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def uv(n, m=None):
    """Pixel-centre coordinates in [-1, 1], y up. Returns (x, y) arrays of shape (m, n)."""
    m = m or n
    x = (np.arange(n) + 0.5) / n * 2 - 1
    y = 1 - (np.arange(m) + 0.5) / m * 2
    return np.meshgrid(x, y)


def cover(d, px):
    """Anti-aliased coverage of an SDF (negative inside); px = size of one pixel in SDF units."""
    return np.clip(0.5 - d / px, 0, 1)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def sd_circle(x, y, r): return np.hypot(x, y) - r


def sd_box(x, y, hx, hy, r=0.0):
    qx, qy = np.abs(x) - hx + r, np.abs(y) - hy + r
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r


def sd_star(x, y, r, n=4, inner=0.38, round_=0.0):
    """Rounded n-point star: radius r at the tips, r*inner at the valleys (polar approximation, good for sprites)."""
    a = np.arctan2(y, x)
    k = 0.5 + 0.5 * np.cos(a * n)
    rad = r * (inner + (1 - inner) * k ** 2.2)
    return (np.hypot(x, y) - rad) * 0.7 - round_


def sd_polygon(x, y, pts):
    """Exact signed distance to a closed polygon (any shape), negative inside. pts: [(x, y), ...]."""
    pts = np.asarray(pts, dtype=np.float64)
    d = (x - pts[0, 0]) ** 2 + (y - pts[0, 1]) ** 2
    sign = np.ones_like(x)
    j = len(pts) - 1
    for i in range(len(pts)):
        ex, ey = pts[j] - pts[i]
        wx, wy = x - pts[i, 0], y - pts[i, 1]
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        bx, by = wx - ex * t, wy - ey * t
        d = np.minimum(d, bx * bx + by * by)
        c1 = y >= pts[i, 1]; c2 = y < pts[j, 1]; c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        sign = np.where(flip, -sign, sign)
        j = i
    return sign * np.sqrt(d)


def to_rgba(rgb, a):
    rgb = np.broadcast_to(np.asarray(rgb, dtype=np.float64), a.shape + (3,))
    return np.concatenate([rgb, a[..., None]], axis=-1)


def white(a):
    """White sprite with alpha a (Unity tints it with vertex / material colour)."""
    return to_rgba((1.0, 1.0, 1.0), a)


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)])


# ---------------------------------------------------------------- tileable noise (FFT-filtered, so seamless)
def tile_noise(n, scale, seed=0, octaves=4, gain=0.5):
    """Seamless fBm-like noise in [0, 1]: white noise band-limited in the frequency domain (periodic by construction)."""
    rng = np.random.default_rng(seed)
    f = np.fft.fftfreq(n) * n
    fr = np.hypot(*np.meshgrid(f, f))
    out = np.zeros((n, n)); amp = 1.0; tot = 0.0
    for o in range(octaves):
        c = scale * 2 ** o
        band = np.exp(-((fr - c) / (0.6 * c + 1e-6)) ** 2)
        w = np.fft.ifft2(np.fft.fft2(rng.standard_normal((n, n))) * band).real
        out += amp * w / (w.std() + 1e-9); tot += amp; amp *= gain
    out /= tot
    return np.clip(0.5 + 0.18 * out, 0, 1)


def tile_voronoi(n, cells, seed=0, warp=0.0):
    """Seamless Voronoi: returns (F1, F2-F1) distance fields in cell units. F2-F1 ~ 0 on cell borders (cracks).
    warp > 0 bends the borders with tileable noise (in cells), so cracks wander instead of drawing straight hexes."""
    rng = np.random.default_rng(seed)
    pts = rng.random((cells * cells, 2)) * cells
    ys, xs = np.mgrid[0:n, 0:n] / n * cells
    if warp:
        xs = (xs + (tile_noise(n, 3, seed=seed + 100, octaves=3) - 0.5) * 2 * warp) % cells
        ys = (ys + (tile_noise(n, 3, seed=seed + 200, octaves=3) - 0.5) * 2 * warp) % cells
    d1 = np.full((n, n), 1e9); d2 = np.full((n, n), 1e9)
    for px, py in pts:
        dx = np.abs(xs - px); dx = np.minimum(dx, cells - dx)
        dy = np.abs(ys - py); dy = np.minimum(dy, cells - dy)
        d = np.hypot(dx, dy)
        d2 = np.where(d < d1, d1, np.minimum(d2, d)); d1 = np.minimum(d1, d)
    return d1, d2 - d1


def normal_from_height(h, strength):
    """Tangent-space normal map (OpenGL / Unity convention, +Y up) from a tileable height field."""
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * strength
    dy = (np.roll(h, 1, 0) - np.roll(h, -1, 0)) * 0.5 * strength   # row 0 = top, so +y is the previous row
    nz = 1 / np.sqrt(dx * dx + dy * dy + 1)
    return np.stack([0.5 - 0.5 * dx * nz, 0.5 - 0.5 * dy * nz, 0.5 + 0.5 * nz], -1)


if __name__ == "__main__":   # self-check: PNG round trip header, SDF signs, seamless noise
    import os, tempfile
    x, y = uv(64)
    assert sd_circle(0, 0, 0.5) < 0 < sd_circle(0.9, 0, 0.5)
    assert sd_box(np.array(0.0), np.array(0.0), 0.5, 0.5) < 0
    sq = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    assert sd_polygon(np.array(0.0), np.array(0.0), sq) < 0 < sd_polygon(np.array(2.0), np.array(0.0), sq)
    assert abs(sd_polygon(np.array(2.0), np.array(0.0), sq) - 1) < 1e-9
    t = tile_noise(64, 4, octaves=2)
    assert abs(t[:, 0] - t[:, -1]).mean() < abs(t[:, 0] - t[:, 32]).mean()   # wraps around
    p = os.path.join(tempfile.gettempdir(), "img_selftest.png")
    save_png(p, white(cover(sd_circle(x, y, 0.8), 2 / 64)))
    assert open(p, "rb").read(8) == b"\x89PNG\r\n\x1a\n"
    print("img.py self-check OK")
