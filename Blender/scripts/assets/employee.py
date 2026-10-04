"""
The employee: a stylised office worker with an armature and seated animations.

Mesh parts are rigidly parented to bones (no skinning), which keeps the FBX simple
and robust in Unity's Generic rig. All animations live on one timeline. The
returned metadata tells Unity how to split it into clips.

The character faces -Y in Blender and is rotated 180 degrees in Unity to face the desk.
The armature origin sits on the floor directly below the hips.
"""
import math

import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

import aclib as L

FPS = 30

SKIN = ("Skin", "#D4A07A")
HAIR = ("Hair", "#3B2A20")
HOODIE = ("Hoodie", "#2A9D8F")
HOODIE_DARK = ("HoodieDark", "#217F74")
JEANS = ("MATTE_Jeans", "#34495E")
SHOE = ("Shoe", "#F2F2F2")
SOLE = ("Sole", "#9AA0A6")

# name: (head, tail, parent)
BONES = {
    "hips": ((0, 0, 0.95), (0, 0, 1.05), None),
    "spine": ((0, 0, 1.05), (0, 0, 1.30), "hips"),
    "chest": ((0, 0, 1.30), (0, 0, 1.46), "spine"),
    "neck": ((0, 0, 1.46), (0, 0, 1.56), "chest"),
    "head": ((0, 0, 1.56), (0, 0, 1.80), "neck"),
}
for side, sx in (("L", 1), ("R", -1)):
    BONES.update({
        f"upper_arm.{side}": ((sx * 0.22, 0, 1.42), (sx * 0.22, 0, 1.12), "chest"),
        f"forearm.{side}": ((sx * 0.22, 0, 1.12), (sx * 0.22, 0, 0.86), f"upper_arm.{side}"),
        f"hand.{side}": ((sx * 0.22, 0, 0.86), (sx * 0.22, 0, 0.78), f"forearm.{side}"),
        f"thigh.{side}": ((sx * 0.1, 0, 0.95), (sx * 0.1, 0, 0.52), "hips"),
        f"shin.{side}": ((sx * 0.1, 0, 0.52), (sx * 0.1, 0, 0.1), f"thigh.{side}"),
        f"foot.{side}": ((sx * 0.1, 0, 0.1), (sx * 0.1, -0.12, 0.03), f"shin.{side}"),
    })


# --------------------------------------------------------------------------- build

def _m(spec):
    return L.mat(spec[0], spec[1], rough=0.55)


def build_armature():
    data = bpy.data.armatures.new("EmployeeRig")
    arm = bpy.data.objects.new("Employee", data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, parent) in BONES.items():
        eb = data.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        eb.roll = 0
        if parent:
            eb.parent = data.edit_bones[parent]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def attach(obj, arm, bone):
    """Rigidly parent `obj` to `bone` without moving it (bone parents attach at the bone tail)."""
    bpy.context.view_layer.update()
    pb = arm.pose.bones[bone]
    parent_world = arm.matrix_world @ pb.matrix @ Matrix.Translation((0, pb.bone.length, 0))
    obj.parent = arm
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    obj.matrix_parent_inverse = parent_world.inverted()


def build_body(arm):
    skin, hair, hoodie, hoodie_dark = _m(SKIN), _m(HAIR), _m(HOODIE), _m(HOODIE_DARK)
    jeans, shoe, sole = _m(JEANS), _m(SHOE), _m(SOLE)
    black = L.mat("GLOSS_Eye", "#111111")
    parts = {}

    # torso
    parts["hips"] = [L.box("Pelvis", (0.34, 0.22, 0.17), (0, 0, 0.97), jeans, bevel=0.06, segments=3)]
    parts["spine"] = [L.box("Belly", (0.37, 0.24, 0.3), (0, 0, 1.17), hoodie, bevel=0.08, segments=3),
                      L.box("Pocket", (0.22, 0.03, 0.09), (0, -0.115, 1.12), hoodie_dark, bevel=0.015)]
    badge = L.mat("GLOSS_White", "#F4F4F2")
    parts["chest"] = [
        L.box("Chest", (0.43, 0.25, 0.24), (0, 0, 1.36), hoodie, bevel=0.09, segments=3),
        L.torus("Hood", 0.1, 0.045, (0, 0.06, 1.47), hoodie_dark, rot=(70, 0, 0), major_segs=20, minor_segs=8),
        L.limb("LanyardL", (0.06, -0.11, 1.46), (0.0, -0.13, 1.3), 0.006, L.mat("GLOSS_AccentRed", "#E63946")),
        L.limb("LanyardR", (-0.06, -0.11, 1.46), (0.0, -0.13, 1.3), 0.006, L.mat("GLOSS_AccentRed", "#E63946")),
        L.box("Badge", (0.06, 0.008, 0.085), (0, -0.135, 1.26), badge, bevel=0.004),
    ]
    parts["neck"] = [L.cyl("Neck", 0.052, 0.12, (0, 0, 1.51), skin)]

    # head
    head_parts = [L.sphere("Head", 0.13, (0, 0, 1.68), skin, scale=(0.95, 1.0, 1.08), segs=24, rings=16)]
    head_parts.append(L.sphere("Hair", 0.138, (0, 0.022, 1.715), hair, scale=(1.0, 1.04, 0.88), segs=24, rings=16))
    head_parts.append(L.sphere("Fringe", 0.07, (0.03, -0.085, 1.79), hair, scale=(1.5, 0.7, 0.45)))
    for sx in (-1, 1):
        head_parts.append(L.sphere("Ear", 0.028, (sx * 0.125, 0.0, 1.68), skin, scale=(0.5, 0.9, 1.2)))
        head_parts.append(L.sphere("Eye", 0.015, (sx * 0.045, -0.118, 1.69), black))
        head_parts.append(L.box("Brow", (0.04, 0.012, 0.01), (sx * 0.046, -0.122, 1.725), _m(HAIR), bevel=0.003))
        head_parts.append(L.torus("Lens", 0.027, 0.0045, (sx * 0.046, -0.135, 1.69), black, rot=(90, 0, 0),
                                  major_segs=20, minor_segs=6))
        head_parts.append(L.limb("Temple", (sx * 0.072, -0.135, 1.695), (sx * 0.122, -0.02, 1.7), 0.0035, black))
    head_parts.append(L.box("Bridge", (0.034, 0.006, 0.006), (0, -0.136, 1.695), black))
    head_parts.append(L.sphere("Nose", 0.02, (0, -0.13, 1.66), skin, scale=(0.8, 0.8, 1.0)))
    head_parts.append(L.box("Mouth", (0.04, 0.01, 0.008), (0, -0.124, 1.618), L.mat("Mouth", "#7A3B3B"), bevel=0.003))
    parts["head"] = head_parts

    for side, sx in (("L", 1), ("R", -1)):
        parts[f"upper_arm.{side}"] = [L.limb("UpperArm", (sx * 0.22, 0, 1.42), (sx * 0.22, 0, 1.12), 0.055, hoodie)]
        parts[f"forearm.{side}"] = [L.limb("Forearm", (sx * 0.22, 0, 1.12), (sx * 0.22, 0, 0.88), 0.048, hoodie),
                                    L.cyl("Cuff", 0.046, 0.03, (sx * 0.22, 0, 0.885), hoodie_dark)]
        parts[f"hand.{side}"] = [L.sphere("Hand", 0.045, (sx * 0.22, -0.005, 0.83), skin, scale=(0.75, 0.9, 1.2)),
                                 L.sphere("Thumb", 0.018, (sx * 0.205, -0.04, 0.85), skin, scale=(1, 1, 1.4))]
        parts[f"thigh.{side}"] = [L.limb("Thigh", (sx * 0.1, 0, 0.95), (sx * 0.1, 0, 0.52), 0.075, jeans, radius1=0.06)]
        parts[f"shin.{side}"] = [L.limb("Shin", (sx * 0.1, 0, 0.52), (sx * 0.1, 0, 0.12), 0.058, jeans, radius1=0.05)]
        parts[f"foot.{side}"] = [L.box("Shoe", (0.11, 0.25, 0.08), (sx * 0.1, -0.06, 0.05), shoe, bevel=0.035, segments=3),
                                 L.box("Sole", (0.115, 0.26, 0.02), (sx * 0.1, -0.06, 0.012), sole, bevel=0.008)]

    for bone, objs in parts.items():
        joined = L.join(f"Mesh_{bone}", objs)
        L.set_origin(joined, BONES[bone][0])
        attach(joined, arm, bone)


# --------------------------------------------------------------------------- posing

def _pb(arm, name):
    return arm.pose.bones[name]


def set_euler(arm, name, x=0.0, y=0.0, z=0.0):
    """Rotate a bone by Euler angles expressed in armature axes (relative to its parent)."""
    pb = _pb(arm, name)
    q = Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ").to_quaternion()
    r = pb.bone.matrix_local.to_3x3().to_quaternion()
    pb.rotation_quaternion = r.inverted() @ q @ r
    bpy.context.view_layer.update()


def aim(arm, name, direction):
    """Point a bone along an armature-space direction, given its parent's current pose."""
    pb = _pb(arm, name)
    bone = pb.bone
    if pb.parent:
        rest_rel = bone.parent.matrix_local.inverted() @ bone.matrix_local
        m0 = pb.parent.matrix @ rest_rel
    else:
        m0 = bone.matrix_local
    q0 = m0.to_3x3().to_quaternion()
    current = (m0.to_3x3() @ Vector((0, 1, 0))).normalized()
    d = current.rotation_difference(Vector(direction).normalized())
    pb.rotation_quaternion = q0.inverted() @ d @ q0
    bpy.context.view_layer.update()


def set_hips_offset(arm, offset):
    pb = _pb(arm, "hips")
    r = pb.bone.matrix_local.to_3x3()
    pb.location = r.inverted() @ Vector(offset)
    bpy.context.view_layer.update()


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()


def mirror(v):
    return (-v[0], v[1], v[2])


SEAT = (0, 0, -0.39)


def pose_seated_base(arm, hips=SEAT, hips_rot=0.0, spine=3.0, chest=0.0, head=(3.0, 0.0, 0.0), legs="sit"):
    reset_pose(arm)
    set_hips_offset(arm, hips)
    set_euler(arm, "hips", x=hips_rot)
    set_euler(arm, "spine", x=spine)
    set_euler(arm, "chest", x=chest)
    set_euler(arm, "neck", x=head[0] * 0.4, z=head[2] * 0.4)
    set_euler(arm, "head", x=head[0] * 0.6, y=head[1], z=head[2] * 0.6)
    for side, sx in (("L", 1), ("R", -1)):
        if legs == "sit":
            aim(arm, f"thigh.{side}", (sx * 0.03, -1, 0.02))
            aim(arm, f"shin.{side}", (0, -0.12, -1))
            aim(arm, f"foot.{side}", (0, -1, -0.25))
        else:  # feet up on the desk, angled to the character's right
            aim(arm, f"thigh.{side}", (-0.42 + sx * 0.05, -0.9, 0.3))
            aim(arm, f"shin.{side}", (-0.3, -1, 0.1))
            aim(arm, f"foot.{side}", (-0.1, -0.45, 1))


def arms_typing(arm, l_lift=0.0, r_lift=0.0):
    for side, sx, lift in (("L", 1, l_lift), ("R", -1, r_lift)):
        aim(arm, f"upper_arm.{side}", (sx * 0.12, -0.45, -0.88))
        aim(arm, f"forearm.{side}", (sx * -0.25, -1, 0.08 + lift))
        aim(arm, f"hand.{side}", (sx * -0.1, -1, -0.2 - lift * 2))


def arms_behind_head(arm):
    for side, sx in (("L", 1), ("R", -1)):
        aim(arm, f"upper_arm.{side}", (sx * 0.5, 0.25, 0.83))
        aim(arm, f"forearm.{side}", (sx * -0.95, 0.15, 0.2))
        aim(arm, f"hand.{side}", (sx * -0.8, 0.3, -0.3))


def arms_up(arm, spread=0.45, bend=0.0):
    for side, sx in (("L", 1), ("R", -1)):
        aim(arm, f"upper_arm.{side}", (sx * spread, -0.1, 0.89))
        aim(arm, f"forearm.{side}", (sx * (spread - 0.15 - bend), -0.1 - bend, 0.95))
        aim(arm, f"hand.{side}", (sx * 0.1, -0.1, 1))


def arm_facepalm(arm):
    """Right palm to the forehead."""
    aim(arm, "upper_arm.R", (0.15, -0.6, 0.2))
    aim(arm, "forearm.R", (0.13, 0.17, 0.15))
    aim(arm, "hand.R", (0.25, 0.35, 0.55))


def arm_sip(arm, lift=1.0):
    """Right hand raises the mug toward the mouth (lift 0 = on the way, 1 = at the mouth)."""
    aim(arm, "upper_arm.R", (0.15 * lift + 0.08 * (1 - lift), -0.7, -0.45 * (1 - lift)))
    aim(arm, "forearm.R", (0.13, 0.06 * lift - 0.6 * (1 - lift), 0.17 + 0.2 * (1 - lift)))
    aim(arm, "hand.R", (0.3, -0.2, 0.5))


def arm_chin(arm):
    """Left elbow on the desk, chin in hand."""
    aim(arm, "upper_arm.L", (-0.07, -0.7, -0.7))
    aim(arm, "forearm.L", (-0.17, 0.05, 0.25))
    aim(arm, "hand.L", (-0.3, 0.1, 0.6))


def arm_phone(arm, nod=0.0):
    """Left hand holds a phone handset to the left ear, elbow out."""
    aim(arm, "upper_arm.L", (0.75, -0.45, 0.48 - nod))
    aim(arm, "forearm.L", (-0.285, 0.115, 0.05 + nod))
    aim(arm, "hand.L", (-0.25, 0.1, 1))


def key_all(arm, frame):
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)


# --------------------------------------------------------------------------- clips

def animate(arm):
    clips = []
    arm.animation_data_create()
    arm.animation_data.action = bpy.data.actions.new("EmployeeTimeline")

    def clip(name, start, end, loop):
        clips.append({"name": name, "start": start, "end": end, "loop": loop})

    # Idle: resting hands on the keyboard, breathing and glancing between monitors
    s = 1
    for i, (breath, look) in enumerate(((0, 0), (1.5, -7), (0, 0), (1.5, 7), (0, 0))):
        pose_seated_base(arm, spine=3 - breath, head=(3, 0, look))
        arms_typing(arm)
        key_all(arm, s + i * 30)
    clip("Idle", s, s + 120, True)

    # Typing: alternating hands
    s = 131
    for i in range(5):
        pose_seated_base(arm, spine=5, head=(6 + (1.5 if i % 2 else 0), 0, 0))
        lift = 0.12 if i % 2 == 0 else -0.04
        arms_typing(arm, l_lift=lift, r_lift=0.08 - lift)
        key_all(arm, s + i * 4)
    clip("Typing", s, s + 16, True)

    # Stretch: arms overhead then back to the keyboard
    s = 161
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s)
    for f, sp in ((s + 22, -8), (s + 48, -12)):
        pose_seated_base(arm, spine=sp, chest=-4, head=(-18, 0, 0))
        arms_up(arm, spread=0.12, bend=0.0)
        key_all(arm, f)
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s + 75)
    clip("Stretch", s, s + 75, False)

    # Relax: leaning back with hands behind the head, gentle sway
    s = 251
    for i, sway in enumerate((0, 3, 0, -3, 0)):
        pose_seated_base(arm, spine=-12, chest=-4, head=(8, 0, sway * 1.5))
        set_euler(arm, "spine", x=-12, z=sway)
        arms_behind_head(arm)
        key_all(arm, s + i * 22 + (1 if i == 4 else 0) * 2)
    clip("Relax", s, s + 90, True)

    # FeetUp: reclined, feet on the desk. The chair rolls back in Unity.
    s = 351
    for i, bob in enumerate((0, 1, 0, -1, 0)):
        pose_seated_base(arm, hips=(0, 0.12, -0.36), hips_rot=-14, spine=-6 + bob, chest=-3,
                         head=(20, 0, bob * 4), legs="up")
        arms_behind_head(arm)
        key_all(arm, s + i * 22 + (1 if i == 4 else 0) * 2)
    clip("FeetUp", s, s + 90, True)

    # Celebrate: fist pumps
    s = 451
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s)
    for i, f in enumerate((s + 8, s + 15, s + 22, s + 29)):
        up = i % 2 == 0
        pose_seated_base(arm, hips=(0, 0, -0.39 + (0.03 if up else 0.0)), spine=-4, head=(-12, 0, 0))
        arms_up(arm, spread=0.5 if up else 0.42, bend=0.0 if up else 0.25)
        key_all(arm, f)
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s + 45)
    clip("Celebrate", s, s + 45, False)

    # Facepalm: outages and missed quotas
    s = 511
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s)
    for f, head, spine in ((s + 12, 22, 9), (s + 40, 26, 11)):
        pose_seated_base(arm, spine=spine, head=(head, 0, 4))
        arms_typing(arm)
        arm_facepalm(arm)
        key_all(arm, f)
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s + 60)
    clip("Facepalm", s, s + 60, False)

    # Sip: coffee break (Unity shows the held mug and hides the desk mug)
    s = 581
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s)
    pose_seated_base(arm, spine=1, head=(2, 0, 0))
    arms_typing(arm)
    arm_sip(arm, 0.4)
    key_all(arm, s + 16)
    for f in (s + 32, s + 58):
        pose_seated_base(arm, spine=-3, head=(-10, 0, 0))
        arms_typing(arm)
        arm_sip(arm, 1.0)
        key_all(arm, f)
    pose_seated_base(arm, spine=1, head=(2, 0, 0))
    arms_typing(arm)
    arm_sip(arm, 0.4)
    key_all(arm, s + 74)
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s + 90)
    clip("Sip", s, s + 90, False)
    sip_hold = s + 45

    # Think: chin in hand, looking at the screen, gentle breathing
    s = 681
    for i, breath in enumerate((0, 1, 0, 1, 0)):
        pose_seated_base(arm, spine=12 - breath, head=(4, 8, -6))
        arms_typing(arm)
        arm_chin(arm)
        key_all(arm, s + i * 22 + (2 if i == 4 else 0))
    clip("Think", s, s + 90, True)

    # LookAround: glance at the left monitor, then the right one
    s = 781
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s)
    for f, turn in ((s + 18, 1), (s + 48, 1), (s + 66, -1), (s + 96, -1)):
        pose_seated_base(arm, head=(4, 0, turn * 34))
        set_euler(arm, "chest", z=turn * 9)
        arms_typing(arm)
        key_all(arm, f)
    pose_seated_base(arm)
    arms_typing(arm)
    key_all(arm, s + 120)
    clip("LookAround", s, s + 120, False)

    # Phone: talking on the desk phone, nodding along, free hand resting on the keyboard
    s = 911
    for i, (nod, look) in enumerate(((0, 0), (0.04, 6), (0, -3), (0.03, 4), (0, 0))):
        pose_seated_base(arm, spine=1, head=(5 + nod * 60, 9, look))
        arms_typing(arm)
        arm_phone(arm, nod)
        key_all(arm, s + i * 22 + (2 if i == 4 else 0))
    clip("Phone", s, s + 90, True)
    phone_hold = s

    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 1001
    scene["sip_hold"] = sip_hold
    scene["phone_hold"] = phone_hold
    reset_pose(arm)
    pose_seated_base(arm)
    arms_typing(arm)
    return clips


def build_held_mug(arm, frame):
    """A mug rigidly attached to the right hand, modelled upright at the mouth in the Sip pose."""
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    hand = bpy.data.objects["Mesh_hand.R"]
    deps = bpy.context.evaluated_depsgraph_get()
    ev = hand.evaluated_get(deps)
    corners = [ev.matrix_world @ Vector(c) for c in ev.bound_box]
    centre = sum(corners, Vector()) / 8
    # mug sits against the palm, toward the body's centre line and slightly forward
    pos = centre + Vector((0.055, -0.03, 0.01))
    body = L.mat("GLOSS_MugBlue", "#2F6DB5")
    parts = [
        L.cyl("HeldBody", 0.04, 0.095, pos, body, verts=24),
        L.cyl("HeldStripe", 0.0405, 0.017, pos + Vector((0, 0, 0.018)), L.mat("GLOSS_White", "#F4F4F2"), verts=24),
        L.cyl("HeldCoffee", 0.035, 0.004, pos + Vector((0, 0, 0.038)), L.mat("GLOSS_Coffee", "#3B2416"), verts=24),
        L.torus("HeldHandle", 0.024, 0.0075, pos + Vector((-0.043, 0, 0.002)), body, rot=(90, 0, 0), major_segs=16, minor_segs=6),
    ]
    mug = L.join("HeldMug", parts)
    L.set_origin(mug, pos)
    attach(mug, arm, "hand.R")
    scene.frame_set(1)


def build_held_handset(arm, frame):
    """A phone handset attached to the left hand, modelled at the ear in the Phone pose."""
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    head = bpy.data.objects["Mesh_head"].evaluated_get(deps)
    corners = [head.matrix_world @ Vector(c) for c in head.bound_box]
    centre = sum(corners, Vector()) / 8
    body = L.mat("GLOSS_PhoneBody", "#2A2D33")
    # long axis runs from the ear (top) down toward the mouth (forward)
    pos = centre + Vector((0.135, -0.03, -0.06))
    parts = [
        L.box("HeldGrip", (0.03, 0.05, 0.19), pos, body, bevel=0.012, rot=(-22, 0, 0)),
        L.box("HeldEar", (0.04, 0.055, 0.05), pos + Vector((-0.005, 0.03, 0.075)), body, bevel=0.014, rot=(-22, 0, 0)),
        L.box("HeldMouth", (0.04, 0.055, 0.05), pos + Vector((-0.005, -0.035, -0.08)), body, bevel=0.014, rot=(-22, 0, 0)),
    ]
    handset = L.join("HeldHandset", parts)
    L.set_origin(handset, pos)
    attach(handset, arm, "hand.L")
    scene.frame_set(1)


def employee():
    arm = build_armature()
    build_body(arm)
    clips = animate(arm)
    build_held_mug(arm, bpy.context.scene["sip_hold"])
    build_held_handset(arm, bpy.context.scene["phone_hold"])
    bpy.context.scene.frame_set(1)
    return {"animated": True, "fps": FPS, "clips": clips}


ASSETS = {"employee": employee}
