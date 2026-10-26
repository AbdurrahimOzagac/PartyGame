# Materials + procedural textures for CubeCritter v2. Run after build_v2.py (needs srgb()).
import bpy, os, math
import numpy as np

TEX = "/Users/ezelbayraktar/Documents/Development/PartyGame/projects/game_assets/knockback_arena/blender/v2/textures"

# Colour-blind-checked player palette (see DESIGN notes); emblem = body lightened 40%.
PLAYERS = {"Tomato": "#FF4F2E", "Teal": "#00B3A4", "Chartreuse": "#D4F21C", "Violet": "#7B2FD0"}
EMBLEM_CELL = {"Tomato": (0, 1), "Teal": (1, 1), "Chartreuse": (0, 0), "Violet": (1, 0)}  # dot, ring / stripes, chevron

def hex_rgb(h):
    h = h.lstrip('#'); return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]

def lighten(h, k):
    r = [c + (1 - c) * k for c in hex_rgb(h)]
    return '#' + ''.join(f"{int(round(c * 255)):02X}" for c in r)

def save_rgba(name, arr):
    """arr: HxWx4 float 0..1, row 0 = bottom (Blender convention)."""
    os.makedirs(TEX, exist_ok=True)
    h, w = arr.shape[:2]
    img = bpy.data.images.get(name)
    if img: bpy.data.images.remove(img)
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png"); img.file_format = 'PNG'
    img.save()
    return img

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def make_blush(n=128):
    # island UVs are the (s,t) grid; the island is a disc via the elliptical grid mapping, so fade by disc radius
    s, t = np.meshgrid(np.linspace(-1, 1, n), np.linspace(-1, 1, n))
    u = s * np.sqrt(np.clip(1 - t * t / 2, 0, None)); v = t * np.sqrt(np.clip(1 - s * s / 2, 0, None))
    r = np.sqrt(u * u + v * v)
    a = (1 - smoothstep(0.15, 1.0, r)) ** 1.4
    arr = np.ones((n, n, 4)); arr[..., 3] = a
    return save_rgba("T_CC_Blush", arr)

def make_emblems(cell=256):
    n = cell * 2
    y, x = np.mgrid[0:n, 0:n].astype(float)
    out = np.zeros((n, n, 4)); out[..., :3] = 1
    aa = 1.5 / cell
    def put(cx, cy, sdf):
        sub = (slice(cy * cell, (cy + 1) * cell), slice(cx * cell, (cx + 1) * cell))
        px = (x[sub] - cx * cell) / cell * 2 - 1; py = (y[sub] - cy * cell) / cell * 2 - 1
        out[sub + (3,)] = 1 - smoothstep(-aa, aa, sdf(px, py))
    put(0, 1, lambda px, py: np.hypot(px, py) - 0.62)                                   # dot
    put(1, 1, lambda px, py: np.abs(np.hypot(px, py) - 0.55) - 0.17)                    # ring
    put(0, 0, lambda px, py: np.minimum(np.abs(py - 0.30), np.abs(py + 0.30)) - 0.16    # double stripe
        + np.maximum(0, np.abs(px) - 0.72) * 4)
    def chevron(px, py):                                                                 # chevron pointing up
        d = np.abs(py - 0.05 - (0.55 - np.abs(px) * 0.95)) * 0.72 - 0.17
        return np.maximum(d, np.abs(px) - 0.74)
    put(1, 0, chevron)
    return save_rgba("T_CC_Emblems", out)

def principled(m):
    m.use_nodes = True
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")

def vinyl(name, hexcol):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    b = principled(m)
    b.inputs["Base Color"].default_value = srgb(hexcol)
    b.inputs["Roughness"].default_value = 0.34
    b.inputs["Coat Weight"].default_value = 1.0
    b.inputs["Coat Roughness"].default_value = 0.05
    b.inputs["Coat IOR"].default_value = 1.5
    b.inputs["Subsurface Weight"].default_value = 0.18
    b.inputs["Subsurface Radius"].default_value = (1.0, 0.5, 0.25)
    b.inputs["Subsurface Scale"].default_value = 0.06
    m.diffuse_color = srgb(hexcol)
    return m

def textured(name, img, hexcol, uv_scale=(1, 1), uv_offset=(0, 0), rough=0.34, coat=1.0, alpha_mult=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    b = principled(m); nt = m.node_tree
    for n in [n for n in nt.nodes if n.type in ("TEX_IMAGE", "MAPPING", "TEX_COORD", "MATH")]: nt.nodes.remove(n)
    tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (uv_scale[0], uv_scale[1], 1)
    mp.inputs["Location"].default_value = (uv_offset[0], uv_offset[1], 0)
    tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = img; tx.extension = 'CLIP'
    nt.links.new(tc.outputs["UV"], mp.inputs["Vector"]); nt.links.new(mp.outputs["Vector"], tx.inputs["Vector"])
    mul = nt.nodes.new("ShaderNodeMath"); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = alpha_mult
    nt.links.new(tx.outputs["Alpha"], mul.inputs[0]); nt.links.new(mul.outputs[0], b.inputs["Alpha"])
    b.inputs["Base Color"].default_value = srgb(hexcol)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Coat Weight"].default_value = coat
    b.inputs["Coat Roughness"].default_value = 0.05
    try: m.surface_render_method = 'BLENDED'
    except Exception as e: print("render method", e)
    m.diffuse_color = srgb(hexcol)
    return m

def simple(name, hexcol, rough, coat=0.0, emit=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    b = principled(m)
    b.inputs["Base Color"].default_value = srgb(hexcol)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Coat Weight"].default_value = coat
    b.inputs["Coat Roughness"].default_value = 0.04
    if emit:
        b.inputs["Emission Color"].default_value = srgb(hexcol); b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = srgb(hexcol)
    return m

def build_materials():
    blush = make_blush(); emb = make_emblems()
    shared = {
        "Eye": simple("M_CC_Eye", "#0D0D12", 0.05, coat=1.0),
        "EyeShine": simple("M_CC_EyeShine", "#FFFFFF", 0.5, emit=3.0),
        "Mouth": simple("M_CC_Mouth", "#3A1020", 0.4),
        "Tongue": simple("M_CC_Tongue", "#FF7A8A", 0.35, coat=0.5),
        "Blush": textured("M_CC_Blush", blush, "#FF5A78", rough=0.5, coat=0.0, alpha_mult=0.9),
        "Feet": simple("M_CC_Feet", "#3B2A24", 0.32, coat=0.6),
    }
    per_player = {}
    for pname, hexcol in PLAYERS.items():
        cx, cy = EMBLEM_CELL[pname]
        per_player[pname] = {
            "Body": vinyl(f"M_CC_Body_{pname}", hexcol),
            "Emblem": textured(f"M_CC_Emblem_{pname}", emb, lighten(hexcol, 0.45),
                               uv_scale=(0.5, 0.5), uv_offset=(cx * 0.5, cy * 0.5)),
        }
    return shared, per_player

def apply_player(ob, pname, shared, per_player):
    me = ob.data
    for i, slot in enumerate(MATS):
        mat = per_player[pname].get(slot) or shared.get(slot)
        if mat: me.materials[i] = mat
