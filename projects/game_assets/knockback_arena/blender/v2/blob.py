# Spit projectile ("SpitBlob") + preview-only flight for renders. Run after build_v2.py (needs srgb).
# Game use: Unity spawns SpitBlob at the Muzzle bone on Spit frame 4, sets blend shape "Stretch" from speed
# and "Squash" on impact. Travel direction = -Y in Blender (= Unity +Z after export), tail on +Y.
import bpy, bmesh, math
from mathutils import Vector

BLOB_R = 0.17        # reads at 16 px at 20 m (60 deg FOV, 1080p); Unity hitbox is separate (0.15)

def build_blob(name="SpitBlob", body_mat=None):
    for o in list(bpy.data.objects):
        if o.name == name: bpy.data.objects.remove(o)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=7, use_grid_fill=True)
    for v in bm.verts: v.co = v.co.normalized() * BLOB_R      # quad sphere, even quads
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    if body_mat: me.materials.append(body_mat)
    ob.shape_key_add(name="Basis", from_mix=False)
    st = ob.shape_key_add(name="Stretch", from_mix=False); st.value = 0.0
    sq = ob.shape_key_add(name="Squash", from_mix=False); sq.value = 0.0
    for i, v in enumerate(me.vertices):
        d = v.co.normalized()
        tail = max(0.0, d.y)                                  # +Y = behind the travel direction
        co = v.co.copy()
        # volume-preserving egg (length 1.35, width 1/sqrt(1.35)), a little fuller at the back. Always convex:
        # a round candy squirting forward, never a drip or a strand
        co.x *= 0.86 * (1 - 0.05 * tail); co.z *= 0.86 * (1 - 0.05 * tail)
        co.y *= 1.35 * (1 + 0.08 * tail)
        st.data[i].co = co
        s = v.co.copy(); s.x *= 1.38; s.z *= 1.38; s.y *= 0.42  # splat against whatever it hits
        sq.data[i].co = s
    return ob

def preview_flight(rig, blob, release_frame=5, speed=0.42, frames=18, droplets=True):
    """Keys a render-only flight: pops out of the mouth at release_frame, flies forward with stretch."""
    sc = bpy.context.scene
    sc.frame_set(release_frame)
    m = rig.matrix_world @ rig.pose.bones["Muzzle"].matrix
    start = m.translation.copy()
    fwd = Vector((0, -1, 0))
    blob.animation_data_clear(); blob.data.shape_keys.animation_data_clear()
    kst = blob.data.shape_keys.key_blocks["Stretch"]
    def key(f, loc, scale, stretch):
        blob.location = loc; blob.scale = (scale,) * 3
        blob.keyframe_insert("location", frame=f); blob.keyframe_insert("scale", frame=f)
        kst.value = stretch; kst.keyframe_insert("value", frame=f)
    key(release_frame - 1, start + fwd * 0.0, 0.0, 0.0)
    key(release_frame, start + fwd * 0.05, 0.55, 0.2)
    key(release_frame + 1, start + fwd * (0.05 + speed * 0.8), 1.05, 1.0)
    key(release_frame + 2, start + fwd * (0.05 + speed * 1.9), 1.0, 0.75)
    key(release_frame + frames, start + fwd * (0.05 + speed * frames) + Vector((0, 0, -0.10)), 1.0, 0.55)
    for fc in blob.animation_data.action.layers[0].strips[0].channelbags[0].fcurves:
        for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
    out = [blob]
    if droplets:  # two little trailing droplets
        for j, (r, lag) in enumerate(((0.035, 2), (0.022, 3))):
            nm = f"SpitDroplet.{j}"
            d = bpy.data.objects.get(nm)
            if not d:
                bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=16, ring_count=8)
                d = bpy.context.active_object; d.name = nm
                bpy.ops.object.shade_smooth()
                if blob.data.materials: d.data.materials.append(blob.data.materials[0])
            d.animation_data_clear()
            for f in range(release_frame - 1, release_frame + 10):
                t = f - release_frame - lag
                vis = 0.0 if t < 0 else max(0.0, 1 - t / 7)
                d.location = start + fwd * (0.05 + max(0, t) * speed * 0.7 + 0.03) + Vector((0, 0, -0.01 * max(0, t) ** 1.3))
                d.scale = (vis,) * 3
                d.keyframe_insert("location", frame=f); d.keyframe_insert("scale", frame=f)
            out.append(d)
    return out
