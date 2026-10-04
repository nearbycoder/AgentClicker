"""Desks and chairs. Origins at floor level; fronts face -Y (the player)."""
import math

import aclib as L


# --------------------------------------------------------------------------- desks

def desk_basic():
    top = L.mat("Laminate", "#CFC7BA", rough=0.5)
    metal = L.mat("METAL_DeskLeg", "#3A3D42")
    panel = L.mat("MATTE_DeskPanel", "#9B958C")
    parts = [L.box("Top", (1.6, 0.75, 0.03), (0, 0, 0.735), top, bevel=0.006)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(L.box("Leg", (0.05, 0.05, 0.72), (sx * 0.76, sy * 0.33, 0.36), metal, bevel=0.004))
        parts.append(L.box("Foot", (0.06, 0.7, 0.03), (sx * 0.76, 0, 0.015), metal, bevel=0.004))
    parts.append(L.box("Modesty", (1.45, 0.02, 0.38), (0, 0.33, 0.5), panel, bevel=0.003))
    parts.append(L.box("CableTray", (0.9, 0.12, 0.06), (0, 0.25, 0.66), metal, bevel=0.003))
    L.join("DeskBasic", parts)


def desk_executive():
    wood = L.mat("Walnut", "#6B4630", rough=0.35)
    wood_dark = L.mat("WalnutDark", "#4E3222", rough=0.4)
    metal = L.mat("METAL_Black", "#202226")
    brass = L.mat("METAL_Brass", "#C9A24B")
    parts = [L.box("Top", (2.2, 0.85, 0.05), (0, 0, 0.735), wood, bevel=0.01, segments=3)]
    # T-legs on the right, pedestal on the left
    parts.append(L.box("LegR", (0.06, 0.06, 0.71), (0.98, 0, 0.355), metal, bevel=0.005))
    parts.append(L.box("FootR", (0.08, 0.78, 0.03), (0.98, 0, 0.015), metal, bevel=0.005))
    parts.append(L.box("Beam", (1.4, 0.05, 0.05), (0.25, 0.3, 0.68), metal, bevel=0.005))
    # pedestal with three drawers
    parts.append(L.box("Pedestal", (0.5, 0.78, 0.71), (-0.82, 0, 0.355), wood_dark, bevel=0.008))
    for i, z in enumerate((0.58, 0.38, 0.15)):
        h = 0.17 if i < 2 else 0.22
        parts.append(L.box(f"Drawer{i}", (0.46, 0.02, h), (-0.82, -0.395, z), wood, bevel=0.004))
        parts.append(L.box(f"Handle{i}", (0.14, 0.015, 0.012), (-0.82, -0.41, z + h * 0.25), brass, bevel=0.003))
    parts.append(L.box("Back", (1.6, 0.025, 0.42), (0.18, 0.39, 0.5), wood_dark, bevel=0.004))
    # leather desk pad
    parts.append(L.box("Pad", (0.9, 0.42, 0.004), (0, -0.12, 0.762), L.mat("MATTE_DeskPad", "#26292E"), bevel=0.002))
    L.join("DeskExecutive", parts)


# --------------------------------------------------------------------------- chairs

def _star_base(parts, metal, wheel, radius=0.3, height=0.42, column_r=0.025):
    parts.append(L.cyl("Hub", 0.05, 0.05, (0, 0, 0.1), metal))
    for i in range(5):
        a = math.radians(90 + i * 72)
        cx, cy = math.cos(a) * radius / 2, math.sin(a) * radius / 2
        leg = L.box(f"Spoke{i}", (radius, 0.04, 0.03), (cx, cy, 0.09), metal, bevel=0.008, rot=(0, 0, math.degrees(a)))
        parts.append(leg)
        wx, wy = math.cos(a) * radius, math.sin(a) * radius
        parts.append(L.sphere(f"Wheel{i}", 0.03, (wx, wy, 0.035), wheel, scale=(1, 0.6, 1)))
    parts.append(L.cyl("Gas", column_r, height - 0.1, (0, 0, 0.1 + (height - 0.1) / 2), L.mat("METAL_Chrome", "#C9CDD2")))


def chair_office():
    fabric = L.mat("MATTE_ChairFabric", "#4A5563")
    plastic = L.mat("Plastic", "#2A2D33", rough=0.5)
    parts = []
    _star_base(parts, plastic, plastic)
    parts.append(L.box("SeatShell", (0.46, 0.44, 0.04), (0, 0, 0.44), plastic, bevel=0.01))
    parts.append(L.box("Seat", (0.48, 0.46, 0.08), (0, -0.01, 0.5), fabric, bevel=0.03, segments=3))
    parts.append(L.box("BackPost", (0.06, 0.03, 0.32), (0, 0.24, 0.6), plastic, bevel=0.008, rot=(-8, 0, 0)))
    parts.append(L.box("Back", (0.44, 0.06, 0.48), (0, 0.27, 0.9), fabric, bevel=0.025, segments=3, rot=(-8, 0, 0)))
    for sx in (-1, 1):
        parts.append(L.box("ArmPost", (0.03, 0.04, 0.2), (sx * 0.25, 0.02, 0.6), plastic, bevel=0.006))
        parts.append(L.box("ArmPad", (0.06, 0.24, 0.025), (sx * 0.25, -0.02, 0.71), plastic, bevel=0.01))
    L.join("ChairOffice", parts)


def chair_gaming():
    black = L.mat("Leatherette", "#1A1A1D", rough=0.45)
    red = L.mat("GLOSS_AccentRed", "#E63946")
    plastic = L.mat("Plastic", "#2A2D33", rough=0.5)
    parts = []
    _star_base(parts, plastic, plastic, radius=0.33)
    parts.append(L.box("Seat", (0.5, 0.5, 0.09), (0, -0.01, 0.5), black, bevel=0.03, segments=3))
    for sx in (-1, 1):
        parts.append(L.box("Bolster", (0.07, 0.48, 0.07), (sx * 0.23, -0.01, 0.56), red, bevel=0.025, segments=3))
    tilt = -10
    parts.append(L.box("Back", (0.5, 0.09, 0.86), (0, 0.29, 1.0), black, bevel=0.035, segments=3, rot=(tilt, 0, 0)))
    for sx in (-1, 1):
        parts.append(L.box("Wing", (0.07, 0.1, 0.6), (sx * 0.24, 0.28, 0.92), red, bevel=0.025, segments=3, rot=(tilt, 0, 0)))
        parts.append(L.box("ArmPost", (0.035, 0.05, 0.2), (sx * 0.29, 0.03, 0.6), plastic, bevel=0.008))
        parts.append(L.box("ArmPad", (0.08, 0.26, 0.03), (sx * 0.29, 0.0, 0.715), black, bevel=0.012))
    parts.append(L.box("Stripe", (0.12, 0.095, 0.7), (0, 0.285, 0.98), red, bevel=0.01, rot=(tilt, 0, 0)))
    parts.append(L.box("Pillow", (0.28, 0.1, 0.14), (0, 0.22, 1.28), red, bevel=0.05, segments=3, rot=(tilt, 0, 0)))
    parts.append(L.box("Lumbar", (0.3, 0.1, 0.14), (0, 0.24, 0.72), black, bevel=0.05, segments=3, rot=(tilt, 0, 0)))
    L.join("ChairGaming", parts)


def chair_recliner():
    leather = L.mat("GLOSS_Leather", "#6B3E26")
    leather_dark = L.mat("GLOSS_LeatherDark", "#4F2C1A")
    chrome = L.mat("METAL_Chrome", "#C9CDD2")
    parts = []
    _star_base(parts, chrome, L.mat("Plastic", "#2A2D33"), radius=0.36)
    parts.append(L.box("Seat", (0.62, 0.58, 0.14), (0, -0.02, 0.48), leather, bevel=0.05, segments=4))
    for sx in (-1, 1):
        parts.append(L.box("Arm", (0.12, 0.6, 0.22), (sx * 0.33, 0.0, 0.62), leather_dark, bevel=0.05, segments=4))
    tilt = -22
    parts.append(L.box("Back", (0.62, 0.16, 0.8), (0, 0.38, 0.95), leather, bevel=0.06, segments=4, rot=(tilt, 0, 0)))
    # tufted rolls
    for i in range(3):
        z = 0.78 + i * 0.2
        y = 0.3 + (z - 0.78) * math.tan(math.radians(-tilt))
        parts.append(L.box(f"Roll{i}", (0.56, 0.08, 0.12), (0, y, z), leather_dark, bevel=0.04, segments=3, rot=(tilt, 0, 0)))
    parts.append(L.box("Head", (0.42, 0.12, 0.16), (0, 0.52, 1.37), leather_dark, bevel=0.06, segments=4, rot=(tilt, 0, 0)))
    L.join("ChairRecliner", parts)


def side_table():
    """Small cabinet next to the desk (the espresso machine lives on top)."""
    wood = L.mat("Oak", "#A07850", rough=0.5)
    white = L.mat("GLOSS_CabinetWhite", "#E9E6DF")
    brass = L.mat("METAL_Brass", "#C9A24B")
    L.join("SideTable", [
        L.box("Body", (0.5, 0.48, 0.72), (0, 0, 0.37), white, bevel=0.01),
        L.box("Top", (0.54, 0.52, 0.03), (0, 0, 0.735), wood, bevel=0.006),
        L.box("Door", (0.44, 0.01, 0.6), (0, -0.242, 0.37), white, bevel=0.004),
        L.box("Knob", (0.06, 0.02, 0.015), (0.15, -0.255, 0.6), brass, bevel=0.004),
        L.box("Plinth", (0.46, 0.44, 0.02), (0, 0, 0.01), L.mat("Plastic", "#2A2D33")),
    ])


ASSETS = {
    "side_table": side_table,
    "desk_basic": desk_basic,
    "desk_executive": desk_executive,
    "chair_office": chair_office,
    "chair_gaming": chair_gaming,
    "chair_recliner": chair_recliner,
}
