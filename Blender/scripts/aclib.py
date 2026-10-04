"""
aclib — shared helpers for building Agent Clicker assets procedurally in Blender.

Conventions
-----------
* Units are metres. Z is up.
* Every asset's "front" faces Blender -Y. After FBX export (forward -Z, up Y) and
  Unity's "Bake Axis Conversion", Blender (x, y, z) lands at Unity (x, z, y). The
  player sits at Unity -Z looking +Z, so props built facing -Y already face the
  player with no rotation. The employee is built facing -Y as well and is turned
  180 degrees in Unity so they face the desk.
* Material names carry meaning for the Unity importer (see MaterialSetup.cs):
    EMIT_*   -> emissive (colour taken from the material's base colour)
    GLASS_*  -> transparent
    METAL_*  -> metallic
    Screen   -> monitor screen surface (dark; UI is drawn in front of it)
"""
import json
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXPORT_DIR = os.path.join(ROOT, "Unity", "Assets", "Art", "Models")
BLEND_DIR = os.path.join(ROOT, "Blender", "source")


# --------------------------------------------------------------------------- scene

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.fps = 30
    _MATS.clear()


# --------------------------------------------------------------------------- colour

def hex_rgb(h):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    # Blender base colours are linear
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb)


_MATS = {}


def mat(name, color="#808080", rough=0.6, metal=0.0, emit=0.0, alpha=1.0):
    """Get or create a Principled material. `color` is an sRGB hex string."""
    if name in _MATS:
        return _MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    rgb = hex_rgb(color)
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit > 0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        m.blend_method = "BLEND" if hasattr(m, "blend_method") else None
    m.diffuse_color = (*rgb, alpha)
    _MATS[name] = m
    return m


# --------------------------------------------------------------------------- primitives

def _finish(obj, name, material, bevel=0.0, segments=2, smooth=None):
    obj.name = name
    obj.data.name = name
    if material is not None:
        obj.data.materials.clear()
        obj.data.materials.append(material)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(40)
    if smooth is not None:
        set_smooth(obj, smooth)
    return obj


def set_smooth(obj, angle_deg=35):
    with bpy.context.temp_override(active_object=obj, object=obj, selected_editable_objects=[obj], selected_objects=[obj]):
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle_deg))


def box(name, size, loc=(0, 0, 0), material=None, bevel=0.0, segments=2, rot=(0, 0, 0)):
    """Axis-aligned box. `loc` is the box centre."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=[math.radians(r) for r in rot])
    obj = bpy.context.active_object
    obj.scale = size
    apply_transform(obj, location=False)
    return _finish(obj, name, material, bevel, segments, smooth=30 if bevel > 0 else None)


def cyl(name, radius, depth, loc=(0, 0, 0), material=None, verts=24, rot=(0, 0, 0), bevel=0.0, radius2=None):
    """Cylinder along Z (before rotation). `radius2` makes a truncated cone."""
    if radius2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc,
                                            rotation=[math.radians(r) for r in rot])
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=radius2, depth=depth, location=loc,
                                        rotation=[math.radians(r) for r in rot])
    obj = bpy.context.active_object
    apply_transform(obj, location=False)
    return _finish(obj, name, material, bevel, 2, smooth=40)


def sphere(name, radius, loc=(0, 0, 0), material=None, scale=(1, 1, 1), segs=20, rings=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, radius=radius, location=loc,
                                         rotation=[math.radians(r) for r in rot])
    obj = bpy.context.active_object
    obj.scale = scale
    apply_transform(obj, location=False)
    return _finish(obj, name, material, smooth=80)


def torus(name, major, minor, loc=(0, 0, 0), material=None, rot=(0, 0, 0), major_segs=32, minor_segs=10):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc,
                                     rotation=[math.radians(r) for r in rot],
                                     major_segments=major_segs, minor_segments=minor_segs)
    obj = bpy.context.active_object
    apply_transform(obj, location=False)
    return _finish(obj, name, material, smooth=80)


def capsule(name, radius, length, loc=(0, 0, 0), material=None, rot=(0, 0, 0), segs=16):
    """Capsule along Z: a cylinder of `length` with hemispherical caps (total = length + 2r)."""
    bm = bmesh.new()
    rings = 6
    verts_rings = []
    # bottom cap (south pole -> equator), cylinder, top cap
    profile = []
    for i in range(rings + 1):
        a = -math.pi / 2 + (math.pi / 2) * i / rings
        profile.append((radius * math.cos(a), -length / 2 + radius * math.sin(a)))
    for i in range(rings + 1):
        a = (math.pi / 2) * i / rings
        profile.append((radius * math.cos(a), length / 2 + radius * math.sin(a)))
    for (r, z) in profile:
        ring = []
        for s in range(segs):
            t = 2 * math.pi * s / segs
            ring.append(bm.verts.new((r * math.cos(t), r * math.sin(t), z)))
        verts_rings.append(ring)
    for a, b in zip(verts_rings[:-1], verts_rings[1:]):
        for s in range(segs):
            v = [a[s], a[(s + 1) % segs], b[(s + 1) % segs], b[s]]
            if len(set(v)) == 4:
                try:
                    bm.faces.new(v)
                except ValueError:
                    pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    obj.rotation_euler = [math.radians(r) for r in rot]
    obj.location = loc
    bpy.context.view_layer.objects.active = obj
    apply_transform(obj, location=False)
    return _finish(obj, name, material, smooth=80)


def limb(name, p0, p1, radius, material, radius1=None):
    """Capsule-ish limb from point p0 to p1 (world coords)."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    length = d.length
    obj = capsule(name, radius, max(length - 0.0, 0.001), loc=(0, 0, 0), material=material)
    if radius1 is not None:
        # taper: scale the top half
        for v in obj.data.vertices:
            t = (v.co.z + length / 2) / max(length, 1e-6)
            t = min(max(t, 0.0), 1.0)
            k = 1.0 + (radius1 / radius - 1.0) * t
            v.co.x *= k
            v.co.y *= k
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = q
    obj.location = (p0 + p1) / 2
    apply_transform(obj, location=True)
    return obj


def text_mesh(name, body, size=0.1, extrude=0.0, bevel=0.0, material=None, loc=(0, 0, 0), rot=(90, 0, 0),
              align="CENTER", fill_mode=None, resolution=6):
    """Text converted to mesh. Default rotation stands the text up, readable from -Y."""
    bpy.ops.object.text_add(location=loc, rotation=[math.radians(r) for r in rot])
    obj = bpy.context.active_object
    obj.data.body = body
    obj.data.size = size
    obj.data.extrude = extrude
    obj.data.bevel_depth = bevel
    obj.data.bevel_resolution = 2
    obj.data.resolution_u = resolution
    obj.data.align_x = align
    obj.data.align_y = "CENTER"
    if fill_mode:
        obj.data.fill_mode = fill_mode
    if material is not None:
        obj.data.materials.append(material)
    with bpy.context.temp_override(active_object=obj, object=obj, selected_editable_objects=[obj], selected_objects=[obj]):
        bpy.ops.object.convert(target="MESH")
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    apply_transform(obj, location=False)
    return obj


# --------------------------------------------------------------------------- object ops

def apply_transform(obj, location=True, rotation=True, scale=True):
    with bpy.context.temp_override(active_object=obj, object=obj, selected_editable_objects=[obj], selected_objects=[obj]):
        bpy.ops.object.transform_apply(location=location, rotation=rotation, scale=scale)


def apply_modifiers(obj):
    with bpy.context.temp_override(active_object=obj, object=obj):
        for mod in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)


def join(name, objs):
    """Join meshes into one object (modifiers applied first). Origin stays at world origin."""
    objs = [o for o in objs if o is not None]
    for o in objs:
        apply_modifiers(o)
    target = objs[0]
    with bpy.context.temp_override(active_object=target, selected_editable_objects=objs, selected_objects=objs):
        bpy.ops.object.join()
    target.name = name
    target.data.name = name
    return target


def set_origin(obj, point):
    """Move the object's origin to `point` (world space) without moving geometry."""
    point = Vector(point)
    offset = obj.matrix_world.inverted() @ point
    obj.data.transform(Matrix.Translation(-offset))
    obj.location = obj.matrix_world @ offset


def mirror_x(obj, name=None):
    """Duplicate an object mirrored across the YZ plane."""
    dup = obj.copy()
    dup.data = obj.data.copy()
    bpy.context.collection.objects.link(dup)
    dup.data.transform(Matrix.Scale(-1, 4, (1, 0, 0)))
    dup.data.flip_normals()
    dup.location.x = -obj.location.x
    if name:
        dup.name = name
        dup.data.name = name
    return dup


def array(objs_factory, count, step):
    """Call objs_factory(i, offset_vector) for i in range(count)."""
    out = []
    for i in range(count):
        out.append(objs_factory(i, Vector(step) * i))
    return out


def parent(child, parent_obj):
    mw = child.matrix_world.copy()
    child.parent = parent_obj
    child.matrix_world = mw


def empty(name, loc=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    obj.location = loc
    bpy.context.collection.objects.link(obj)
    return obj


# --------------------------------------------------------------------------- export

def save_blend(path):
    """Save the scene data without UI state (screens and file browsers would embed local paths)."""
    ids = set(bpy.data.scenes) | set(bpy.data.actions) | set(bpy.data.materials)
    bpy.data.libraries.write(path, ids, path_remap="NONE", compress=True)


def export(asset_name, extra_meta=None):
    """Export everything in the scene to Unity/Assets/Art/Models/<asset_name>.fbx and save a .blend."""
    os.makedirs(EXPORT_DIR, exist_ok=True)
    os.makedirs(BLEND_DIR, exist_ok=True)
    # apply bevels etc. so the exported mesh matches what you see
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.select_set(True)
    path = os.path.join(EXPORT_DIR, asset_name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={"MESH", "EMPTY", "ARMATURE"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=bool(extra_meta and extra_meta.get("animated")),
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",
    )
    save_blend(os.path.join(BLEND_DIR, asset_name + ".blend"))
    if extra_meta:
        with open(os.path.join(EXPORT_DIR, asset_name + ".anim.json"), "w") as f:
            json.dump(extra_meta, f, indent=2)
    print(f"[aclib] exported {asset_name} -> {path}")
    return path
