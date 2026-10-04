"""Render a quick 3/4 front preview of the current scene (used by build_all --preview)."""
import math
import os

import bpy
from mathutils import Vector

import aclib

PREVIEW_DIR = os.path.join(aclib.ROOT, "Blender", "previews")


def scene_bounds():
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    deps = bpy.context.evaluated_depsgraph_get()
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        ev = obj.evaluated_get(deps)
        for corner in ev.bound_box:
            w = ev.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


def render(name, direction=(0.55, -1.0, 0.55), size=512):
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    scene = bpy.context.scene
    lo, hi = scene_bounds()
    center = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.05)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    d = Vector(direction).normalized()
    fov = 2 * math.atan(36 / (2 * cam_data.lens))
    dist = radius / math.sin(fov / 2) * 1.05
    cam.location = center + d * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_end = dist * 10
    scene.camera = cam

    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "MATERIAL"
    shading.show_shadows = True
    shading.show_cavity = True
    shading.cavity_type = "BOTH"
    scene.display.render_aa = "8"
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.render.filepath = os.path.join(PREVIEW_DIR, name + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print(f"[preview] {scene.render.filepath}")
