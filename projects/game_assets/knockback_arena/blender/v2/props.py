# Small hero props: the leader's crown and the jump power-up. Blender Z-up, metres, -Y = front.
#   Crown        sits on the critter's head around the goo curl (Unity parents it to the "Top" bone).
#   Pickup_Jump  floating spring power-up; origin at its centre (Unity bobs and spins it).
import bpy, bmesh, math
from mathutils import Vector, Matrix

def _mat(name, hexcol, rough, metal=0.0, coat=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    h = hexcol.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    b.inputs["Base Color"].default_value = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1]
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    b.inputs["Coat Weight"].default_value = coat
    return m

def _obj(name, bm, mats, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    for m in mats: me.materials.append(m)
    return ob

def _subsurf(ob, levels=2):
    mod = ob.modifiers.new("ss", 'SUBSURF'); mod.levels = levels; mod.render_levels = levels
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier=mod.name)

def _sphere(bm, c, r, mat=0, seg=16, rings=10):
    g = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    for v in g["verts"]: v.co += Vector(c)
    for f in {f for v in g["verts"] for f in v.link_faces}: f.material_index = mat

GOLD = "#FFC23D"

def build_crown(name="Crown", points=5, r=0.165, band=0.07, spike=0.08, wall=0.032):
    """Gold band with rounded points and ball tips, a ruby in front. Origin at the band's bottom centre."""
    gold = _mat("M_Crown_Gold", GOLD, 0.22, metal=1.0)
    ruby = _mat("M_Crown_Gem", "#FF3D6E", 0.08, coat=1.0)
    bm = bmesh.new()
    N = points * 12
    def height(a):
        # rounded triangular points, peaks at a = k * tau / points; a = 0 faces front (-Y)
        t = (a / (math.tau / points) + 0.5) % 1.0
        k = 1 - abs(t - 0.5) * 2
        return band + spike * (k ** 1.6)
    outer, inner, top_o, top_i = [], [], [], []
    for i in range(N):
        a = math.tau * i / N                              # a point in front, the gem on the band below it
        d = Vector((math.sin(a), -math.cos(a), 0))
        flare = 1 + 0.10 * (height(a) - band) / spike     # points lean out a little: reads as a crown, not a can
        h = height(a)
        outer.append(bm.verts.new(d * (r + wall * 0.5) + Vector((0, 0, 0))))
        inner.append(bm.verts.new(d * (r - wall * 0.5)))
        top_o.append(bm.verts.new(d * (r + wall * 0.5) * flare + Vector((0, 0, h))))
        top_i.append(bm.verts.new(d * (r - wall * 0.5) * flare + Vector((0, 0, h))))
    for i in range(N):
        j = (i + 1) % N
        bm.faces.new((outer[i], outer[j], top_o[j], top_o[i]))
        bm.faces.new((inner[j], inner[i], top_i[i], top_i[j]))
        bm.faces.new((top_o[i], top_o[j], top_i[j], top_i[i]))
        bm.faces.new((inner[i], inner[j], outer[j], outer[i]))
    # a raised rim band along the bottom
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ob = _obj(name, bm, [gold, ruby])
    _subsurf(ob, 2)
    # ball tips + gem (added after smoothing so they stay round)
    bm = bmesh.new(); bm.from_mesh(ob.data)
    for k in range(points):
        a = math.tau * k / points
        d = Vector((math.sin(a), -math.cos(a), 0))
        _sphere(bm, d * r * 1.10 + Vector((0, 0, band + spike + 0.006)), 0.024)
    _sphere(bm, Vector((0, -(r + wall * 0.5 + 0.004), band * 0.5)), 0.034, mat=1, seg=20, rings=12)
    for v in bm.verts:   # flatten the gem a little into the band
        if v.co.y < -(r + 0.004) and v.co.z < band: v.co.y = -(r + wall * 0.5) + (v.co.y + r + wall * 0.5) * 0.55
    bm.to_mesh(ob.data); bm.free()
    for p in ob.data.polygons: p.use_smooth = True
    return ob

def build_jump_pickup(name="Pickup_Jump"):
    """Chunky cartoon spring: pink pad on top with a white up-chevron, gold coil, pink base. ~0.5 m tall."""
    gold = _mat("M_Pickup_Gold", GOLD, 0.25, metal=1.0)
    pink = _mat("M_Pickup_Pink", "#FF4FA3", 0.18, coat=1.0)
    white = _mat("M_Pickup_White", "#FFFFFF", 0.2, coat=1.0)
    bm = bmesh.new()
    # coil: tube swept along a helix
    turns, R, tube, z0, z1 = 2.25, 0.13, 0.027, -0.17, 0.15
    steps, ring = int(turns * 40), 10
    rings = []
    for i in range(steps + 1):
        t = i / steps
        a = t * turns * math.tau
        c = Vector((math.cos(a) * R, math.sin(a) * R, z0 + (z1 - z0) * t))
        tang = Vector((-math.sin(a) * R * turns * math.tau, math.cos(a) * R * turns * math.tau, z1 - z0)).normalized()
        nrm = Vector((math.cos(a), math.sin(a), 0)); bin_ = tang.cross(nrm)
        rings.append([bm.verts.new(c + (nrm * math.cos(b) + bin_ * math.sin(b)) * tube)
                      for b in [math.tau * k / ring for k in range(ring)]])
    for i in range(steps):
        for k in range(ring):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % ring], rings[i + 1][(k + 1) % ring], rings[i + 1][k]))
            f.material_index = 0
    for end in (rings[0], rings[-1]):
        f = bm.faces.new(end if end is rings[-1] else end[::-1]); f.material_index = 0
    # pads: rounded discs (bevelled cylinders)
    for zc, rr, th in ((0.19, 0.185, 0.07), (-0.2, 0.155, 0.055)):
        g = bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=rr, radius2=rr, depth=th)
        for v in g["verts"]: v.co.z += zc
        for f in {f for v in g["verts"] for f in v.link_faces}: f.material_index = 1
        bmesh.ops.bevel(bm, geom=[e for e in bm.edges if all(v in g["verts"] for v in e.verts) and
                                  abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-6],
                        offset=th * 0.42, segments=4, affect='EDGES', profile=0.5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ob = _obj(name, bm, [gold, pink, white])
    # chevron on the top pad (separate shell, slightly proud of the surface)
    bm = bmesh.new(); bm.from_mesh(ob.data)
    zt = 0.19 + 0.07 / 2 + 0.001
    pts = [(-0.09, -0.04), (0.0, 0.06), (0.09, -0.04), (0.09, -0.09), (0.0, 0.0), (-0.09, -0.09)]
    lo = [bm.verts.new((x, y, zt - 0.006)) for x, y in pts]
    hi = [bm.verts.new((x, y, zt + 0.012)) for x, y in pts]
    for quad in [(0, 1, 4, 5), (1, 2, 3, 4)]:
        bm.faces.new([hi[i] for i in quad]).material_index = 2
        bm.faces.new([lo[i] for i in reversed(quad)]).material_index = 2
    for i in range(6):
        j = (i + 1) % 6
        bm.faces.new((lo[i], lo[j], hi[j], hi[i])).material_index = 2
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[-14:])
    bm.to_mesh(ob.data); bm.free()
    for p in ob.data.polygons: p.use_smooth = p.material_index != 2
    return ob

if __name__ == "__main__":   # quick self-check when run on its own in Blender
    c = build_crown(); p = build_jump_pickup()
    zs = [v.co.z for v in c.data.vertices]
    assert 0.12 < max(zs) - min(zs) < 0.2, (min(zs), max(zs))
    assert len(p.data.materials) == 3
    print("props OK", len(c.data.vertices), len(p.data.vertices))
