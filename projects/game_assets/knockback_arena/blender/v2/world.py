# World assembly for the ice arena: preview material (mirrors the Unity KnockbackArena/Ice shader closely
# enough for renders), default 4x4 layout, background ring, and FBX export of every kit piece.
# Run after arena.py + props.py. Unity rebuilds the actual arena prefab from these pieces.
import bpy, math, os
from mathutils import Vector

UNITY_MODELS = "/Users/ezelbayraktar/Documents/Development/PartyGame/Assets/Ezel/KnockbackArena/Models/Arena"

def _hex(h):
    h = h.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]

ICE = dict(top="#B8D3EB", side="#3B8CC2", deep="#123E86", frost="#EAF2FA", seam="#8FB0D4")

def ice_material(name="M_Ice_Preview"):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    N = lambda t, **kw: nt.nodes.new(t)
    out = N("ShaderNodeOutputMaterial"); bsdf = N("ShaderNodeBsdfPrincipled")
    attr = N("ShaderNodeAttribute"); attr.attribute_name = "Col"
    sep = N("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], sep.inputs[0])
    def mix(a, b, fac):
        n = N("ShaderNodeMix"); n.data_type = 'RGBA'; n.clamp_factor = True
        nt.links.new(fac, n.inputs["Factor"])
        for sock, v in ((n.inputs[6], a), (n.inputs[7], b)):
            if isinstance(v, list): sock.default_value = v
            else: nt.links.new(v, sock)
        return n.outputs[2]
    def math_(op, a, b=None, c=None):
        n = N("ShaderNodeMath"); n.operation = op; n.use_clamp = op in ('MULTIPLY_ADD', 'SUBTRACT')
        for i, v in enumerate((a, b, c)):
            if v is None: continue
            if isinstance(v, (int, float)): n.inputs[i].default_value = v
            else: nt.links.new(v, n.inputs[i])
        return n.outputs[0]
    t = sep.outputs[0]
    lo = math_('MULTIPLY', t, 2.0); hi = math_('SUBTRACT', lo, 1.0)
    alb = mix(mix(_hex(ICE["deep"]), _hex(ICE["side"]), lo), _hex(ICE["top"]), hi)
    alb = mix(alb, _hex(ICE["frost"]), sep.outputs[1])
    ao = N("ShaderNodeMix"); ao.data_type = 'RGBA'; ao.blend_type = 'MULTIPLY'; ao.inputs["Factor"].default_value = 1.0
    nt.links.new(alb, ao.inputs[6]); nt.links.new(sep.outputs[2], ao.inputs[7])
    # dark seam grooves on the 4 m grid, on tops only (normal z) and never on the snow rim
    geo = N("ShaderNodeNewGeometry"); sxyz = N("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Position"], sxyz.inputs[0])
    def line(axis):
        f = math_('PINGPONG', math_('DIVIDE', sxyz.outputs[axis], 4.0), 0.5)     # distance to nearest grid line in tiles
        return math_('LESS_THAN', f, 0.006)
    seam = math_('MAXIMUM', line(0), line(1))
    nz = N("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Normal"], nz.inputs[0])
    seam = math_('MULTIPLY', seam, math_('GREATER_THAN', nz.outputs[2], 0.9))
    seam = math_('MULTIPLY', seam, math_('LESS_THAN', sep.outputs[1], 0.5))
    final = mix(ao.outputs[2], _hex(ICE["seam"]), seam)
    nt.links.new(final, bsdf.inputs["Base Color"])
    rough = math_('MULTIPLY_ADD', sep.outputs[1], 0.45, 0.28)       # ice 0.28, snow 0.73
    nt.links.new(rough, bsdf.inputs["Roughness"])
    bsdf.inputs["Coat Weight"].default_value = 0.25
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    return m

def kit_pieces():
    """Build every exportable piece (arena.py / props.py). Returns {name: object}."""
    pieces = {}
    for o in (tile_top(), tile_edge(1, "Ice_Edge_4_A"), tile_edge(2, "Ice_Edge_4_B"), tile_corner(),
              underside("Island_Underside_16", rounded_square(8.0 * 0.93, 4.0 * 0.93), depth=11.0),
              floe("Floe_A", 3.2, 21), floe("Floe_B", 4.5, 22), floe("Floe_C", 2.4, 23),
              crystal_cluster("Crystal_Cluster_A", 31, size=1.2), crystal_cluster("Crystal_Cluster_B", 32, n=5, size=0.9),
              crystal_cluster("Crystal_Cluster_C", 33, n=8, size=1.5), podium()[0]):
        pieces[o.name] = o
    return pieces

# default 16 m layout: (piece, x, y, rotation deg) in Blender coordinates; Unity mirrors this in ArenaSetup.cs
def layout_4x4():
    cs = [-6, -2, 2, 6]; out = []
    for ix, x in enumerate(cs):
        for iy, y in enumerate(cs):
            bx, by = ix in (0, 3), iy in (0, 3)
            if bx and by: out.append(("Ice_Corner", x, y, {(3, 0): 0, (3, 3): 90, (0, 3): 180, (0, 0): 270}[(ix, iy)]))
            elif bx or by:
                rot = 0 if iy == 0 else 180 if iy == 3 else 90 if ix == 3 else 270
                out.append(("Ice_Edge_4_A" if (ix + iy) % 2 else "Ice_Edge_4_B", x, y, rot))
            else: out.append(("Ice_Top_4x4", x, y, 0))
    return out

def export_piece(ob, path):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    loc, rot = ob.location.copy(), ob.rotation_euler.copy()
    ob.location = (0, 0, 0); ob.rotation_euler = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=path, object_types={'MESH'}, use_selection=True, bake_anim=False,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', colors_type='LINEAR', use_custom_props=False,
                             path_mode='STRIP', embed_textures=False)
    ob.location, ob.rotation_euler = loc, rot
