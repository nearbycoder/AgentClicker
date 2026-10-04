"""
The office room and its fixtures.

Room layout (Blender coords == Unity (x, z, y)):
    x in [-3, 3]          left wall (with window) at x = -3, right wall (door) at x = +3
    y in [-3.5, 1.25]     front wall (behind the monitors) at y = +1.25
    z in [0, 3]           floor to ceiling
"""
import math
import random

import aclib as L

W, FRONT, BACK, H, T = 3.0, 1.25, -3.5, 3.0, 0.12
WIN_Y0, WIN_Y1, WIN_Z0, WIN_Z1 = -2.1, 0.7, 0.85, 2.45
DOOR_Y0, DOOR_Y1, DOOR_Z1 = -3.1, -2.1, 2.15


def room_shell():
    wall = L.mat("MATTE_Wall", "#E6E0D4")
    accent = L.mat("MATTE_WallAccent", "#33505F")
    carpet = L.mat("MATTE_Carpet", "#59626C")
    ceiling = L.mat("MATTE_Ceiling", "#F1F0EC")
    base = L.mat("Baseboard", "#D3CCBE", rough=0.5)
    depth = FRONT - BACK
    cy = (FRONT + BACK) / 2
    parts = [
        L.box("Floor", (2 * W + 2 * T, depth + 2 * T, 0.05), (0, cy, -0.025), carpet),
        L.box("Ceiling", (2 * W + 2 * T, depth + 2 * T, 0.05), (0, cy, H + 0.025), ceiling),
        L.box("Front", (2 * W, T, H), (0, FRONT + T / 2, H / 2), accent),
        L.box("Back", (2 * W, T, H), (0, BACK - T / 2, H / 2), wall),
    ]
    # left wall with window opening
    lx = -W - T / 2
    parts += [
        L.box("LeftA", (T, WIN_Y0 - BACK, H), (lx, (BACK + WIN_Y0) / 2, H / 2), wall),
        L.box("LeftB", (T, FRONT - WIN_Y1, H), (lx, (WIN_Y1 + FRONT) / 2, H / 2), wall),
        L.box("LeftC", (T, WIN_Y1 - WIN_Y0, WIN_Z0), (lx, (WIN_Y0 + WIN_Y1) / 2, WIN_Z0 / 2), wall),
        L.box("LeftD", (T, WIN_Y1 - WIN_Y0, H - WIN_Z1), (lx, (WIN_Y0 + WIN_Y1) / 2, (WIN_Z1 + H) / 2), wall),
    ]
    # right wall with door opening
    rx = W + T / 2
    parts += [
        L.box("RightA", (T, DOOR_Y0 - BACK, H), (rx, (BACK + DOOR_Y0) / 2, H / 2), wall),
        L.box("RightB", (T, FRONT - DOOR_Y1, H), (rx, (DOOR_Y1 + FRONT) / 2, H / 2), wall),
        L.box("RightC", (T, DOOR_Y1 - DOOR_Y0, H - DOOR_Z1), (rx, (DOOR_Y0 + DOOR_Y1) / 2, (DOOR_Z1 + H) / 2), wall),
    ]
    # baseboards
    bh, bt = 0.09, 0.015
    parts += [
        L.box("BaseFront", (2 * W, bt, bh), (0, FRONT - bt / 2, bh / 2), base),
        L.box("BaseBack", (2 * W, bt, bh), (0, BACK + bt / 2, bh / 2), base),
        L.box("BaseLeft", (bt, depth, bh), (-W + bt / 2, cy, bh / 2), base),
        L.box("BaseRightA", (bt, DOOR_Y0 - BACK, bh), (W - bt / 2, (BACK + DOOR_Y0) / 2, bh / 2), base),
        L.box("BaseRightB", (bt, FRONT - DOOR_Y1, bh), (W - bt / 2, (DOOR_Y1 + FRONT) / 2, bh / 2), base),
    ]
    L.join("Room", parts)


def window():
    frame = L.mat("GLOSS_WindowFrame", "#F4F4F4")
    x = -W - T / 2
    fy0, fy1 = WIN_Y0, WIN_Y1
    ft = 0.06
    parts = [
        L.box("Top", (T + 0.02, fy1 - fy0, ft), (x, (fy0 + fy1) / 2, WIN_Z1 - ft / 2), frame),
        L.box("Bottom", (T + 0.02, fy1 - fy0, ft), (x, (fy0 + fy1) / 2, WIN_Z0 + ft / 2), frame),
        L.box("SideA", (T + 0.02, ft, WIN_Z1 - WIN_Z0), (x, fy0 + ft / 2, (WIN_Z0 + WIN_Z1) / 2), frame),
        L.box("SideB", (T + 0.02, ft, WIN_Z1 - WIN_Z0), (x, fy1 - ft / 2, (WIN_Z0 + WIN_Z1) / 2), frame),
        L.box("Sill", (0.26, fy1 - fy0 + 0.12, 0.035), (-W + 0.07, (fy0 + fy1) / 2, WIN_Z0 - 0.0175), frame, bevel=0.006),
    ]
    for i in (1, 2):
        y = fy0 + (fy1 - fy0) * i / 3
        parts.append(L.box(f"Mullion{i}", (0.05, 0.04, WIN_Z1 - WIN_Z0), (x, y, (WIN_Z0 + WIN_Z1) / 2), frame))
    parts.append(L.box("Transom", (0.05, fy1 - fy0, 0.04), (x, (fy0 + fy1) / 2, WIN_Z1 - 0.45), frame))
    L.join("WindowFrame", parts)
    L.box("Glass", (0.01, fy1 - fy0, WIN_Z1 - WIN_Z0), (x, (fy0 + fy1) / 2, (WIN_Z0 + WIN_Z1) / 2),
          L.mat("GLASS_Window", "#BFD9F2", alpha=0.12))


def skyline():
    """City outside the left window. Building faces toward +X (the room)."""
    rng = random.Random(42)
    tones = [L.mat(f"MATTE_Building{i}", c) for i, c in enumerate(["#5C6B7A", "#6E7C8C", "#4D5966", "#7A8796"])]
    lit = L.mat("EMIT_CityWindows", "#FFD68A", emit=2.5)
    buildings, windows = [], []
    for row, (x0, count) in enumerate(((-14, 9), (-26, 12), (-42, 14))):
        y = -26.0
        for i in range(count):
            w = rng.uniform(3.5, 7.0)
            d = rng.uniform(4, 8)
            h = rng.uniform(18, 46) + row * 10
            y += w / 2 + rng.uniform(0.5, 2.5)
            cx = x0 - d / 2
            buildings.append(L.box(f"B{row}_{i}", (d, w, h), (cx, y, -32 + h / 2), tones[rng.randrange(4)]))
            # lit windows on the room-facing side
            face_x = cx + d / 2 + 0.05
            for fz in range(int(h / 3.2)):
                z = -32 + 2.0 + fz * 3.2
                for wy in range(max(1, int(w / 1.6))):
                    if rng.random() < 0.38:
                        yy = y - w / 2 + 0.8 + wy * 1.6
                        windows.append(L.box("w", (0.05, 0.9, 1.3), (face_x, yy, z), lit))
            y += w / 2
    L.join("Buildings", buildings)
    L.join("CityWindows", windows)
    L.box("Ground", (80, 120, 0.1), (-40, 0, -32), L.mat("MATTE_Ground", "#3B4652"))


def ceiling_light():
    L.join("Fixture", [
        L.box("Housing", (0.62, 1.22, 0.05), (0, 0, -0.025), L.mat("METAL_Fixture", "#DADDE0"), bevel=0.004),
    ])
    L.box("Panel", (0.56, 1.16, 0.01), (0, 0, -0.054), L.mat("EMIT_LightPanel", "#FFF4E0", emit=3))


def cubicle_partition():
    fabric = L.mat("MATTE_Fabric", "#7C8B99")
    trim = L.mat("METAL_Trim", "#B8BEC5")
    w, h, t = 1.6, 1.35, 0.06
    L.join("Partition", [
        L.box("Fabric", (w - 0.04, t - 0.01, h - 0.04), (0, 0, h / 2), fabric),
        L.box("TrimTop", (w, t, 0.03), (0, 0, h - 0.015), trim, bevel=0.005),
        L.box("TrimL", (0.025, t, h), (-w / 2 + 0.0125, 0, h / 2), trim),
        L.box("TrimR", (0.025, t, h), (w / 2 - 0.0125, 0, h / 2), trim),
        L.box("FootL", (0.06, 0.4, 0.03), (-w / 2 + 0.1, 0, 0.015), trim),
        L.box("FootR", (0.06, 0.4, 0.03), (w / 2 - 0.1, 0, 0.015), trim),
    ])


def wall_clock():
    """Origin at the clock centre. Hands point to 12 at rest; Unity rotates them about local Z."""
    rim = L.mat("GLOSS_Black", "#151515")
    face = L.mat("MATTE_ClockFace", "#FAFAF7")
    L.join("Clock", [
        L.cyl("Rim", 0.18, 0.04, (0, 0, 0), rim, rot=(90, 0, 0), verts=40),
        L.cyl("Face", 0.165, 0.005, (0, -0.02, 0), face, rot=(90, 0, 0), verts=40),
    ] + [
        L.box(f"Tick{i}", (0.01 if i % 3 else 0.016, 0.004, 0.03 if i % 3 else 0.04),
              (math.sin(i * math.pi / 6) * 0.14, -0.024, math.cos(i * math.pi / 6) * 0.14), rim,
              rot=(0, i * 30, 0))
        for i in range(12)
    ])
    hour = L.box("HourHand", (0.014, 0.004, 0.09), (0, -0.028, 0.035), rim)
    L.set_origin(hour, (0, -0.028, 0))
    minute = L.box("MinuteHand", (0.009, 0.004, 0.13), (0, -0.032, 0.055), rim)
    L.set_origin(minute, (0, -0.032, 0))
    L.cyl("Pin", 0.01, 0.01, (0, -0.036, 0), L.mat("GLOSS_AccentRed", "#E63946"), rot=(90, 0, 0))


def calendar():
    """Hanging calendar; origin at the top hook. Unity writes the day number on it."""
    L.join("Calendar", [
        L.box("Paper", (0.3, 0.004, 0.42), (0, 0, -0.23), L.mat("MATTE_Paper", "#FBFAF5")),
        L.box("Header", (0.3, 0.006, 0.08), (0, -0.002, -0.06), L.mat("MATTE_CalendarRed", "#D64545")),
        L.box("Binding", (0.31, 0.012, 0.02), (0, -0.003, -0.015), L.mat("METAL_Trim", "#B8BEC5")),
        L.cyl("Nail", 0.005, 0.02, (0, 0.0, 0.0), L.mat("METAL_Trim", "#B8BEC5"), rot=(90, 0, 0)),
    ])


def poster():
    """Framed poster; origin at the centre. The canvas is separate so Unity can tint it and add text."""
    L.box("Frame", (0.62, 0.025, 0.86), (0, 0, 0), L.mat("GLOSS_Black", "#151515"), bevel=0.004)
    L.box("Canvas", (0.56, 0.004, 0.8), (0, -0.013, 0), L.mat("MATTE_PosterCanvas", "#FFFFFF"))


def whiteboard():
    board = L.mat("GLOSS_Whiteboard", "#FAFAFA")
    frame = L.mat("METAL_Trim", "#B8BEC5")
    blue = L.mat("MATTE_MarkerBlue", "#2D6CDF")
    red = L.mat("MATTE_MarkerRed", "#D64545")
    green = L.mat("MATTE_MarkerGreen", "#2E9E5B")
    w, h = 1.5, 0.95
    parts = [
        L.box("Board", (w, 0.02, h), (0, 0, 0), board),
        L.box("FrameT", (w + 0.04, 0.03, 0.02), (0, 0, h / 2 + 0.01), frame),
        L.box("FrameB", (w + 0.04, 0.03, 0.02), (0, 0, -h / 2 - 0.01), frame),
        L.box("FrameL", (0.02, 0.03, h), (-w / 2 - 0.01, 0, 0), frame),
        L.box("FrameR", (0.02, 0.03, h), (w / 2 + 0.01, 0, 0), frame),
        L.box("Tray", (0.5, 0.07, 0.015), (0, -0.04, -h / 2 - 0.02), frame),
    ]
    for i, m in enumerate((blue, red, green)):
        parts.append(L.cyl(f"Marker{i}", 0.008, 0.12, (-0.12 + i * 0.07, -0.05, -h / 2 - 0.005), m, rot=(0, 90, 0)))
    L.join("Whiteboard", parts)

    # architecture diagram: boxes + arrows, drawn as thin strokes
    strokes = []

    def rect(cx, cz, rw, rh, m):
        t = 0.008
        strokes.extend([
            L.box("s", (rw, 0.004, t), (cx, -0.012, cz + rh / 2), m),
            L.box("s", (rw, 0.004, t), (cx, -0.012, cz - rh / 2), m),
            L.box("s", (t, 0.004, rh), (cx - rw / 2, -0.012, cz), m),
            L.box("s", (t, 0.004, rh), (cx + rw / 2, -0.012, cz), m),
        ])

    def arrow(x0, z0, x1, z1, m):
        dx, dz = x1 - x0, z1 - z0
        length = math.hypot(dx, dz)
        ang = math.degrees(math.atan2(dz, dx))
        strokes.append(L.box("a", (length, 0.004, 0.007), ((x0 + x1) / 2, -0.012, (z0 + z1) / 2), m, rot=(0, -ang, 0)))
        for s in (-1, 1):
            hx = x1 - math.cos(math.radians(ang + s * 30)) * 0.04
            hz = z1 - math.sin(math.radians(ang + s * 30)) * 0.04
            strokes.append(L.box("h", (0.045, 0.004, 0.007), ((x1 + hx) / 2, -0.012, (z1 + hz) / 2), m,
                                 rot=(0, -(ang + s * 30), 0)))

    rect(-0.5, 0.2, 0.3, 0.18, blue)
    rect(0.0, 0.2, 0.3, 0.18, blue)
    rect(0.5, 0.2, 0.3, 0.18, blue)
    rect(0.0, -0.22, 0.5, 0.2, green)
    arrow(-0.35, 0.2, -0.15, 0.2, red)
    arrow(0.15, 0.2, 0.35, 0.2, red)
    arrow(0.0, 0.11, 0.0, -0.12, red)
    strokes.append(L.text_mesh("Label", "AGENTS", size=0.07, material=blue, loc=(0, -0.013, -0.22)))
    strokes.append(L.text_mesh("Label2", "PROFIT?", size=0.05, material=red, loc=(0.48, -0.013, -0.25), rot=(90, -8, 0)))
    L.join("Diagram", strokes)


def bookshelf():
    rng = random.Random(5)
    wood = L.mat("Oak", "#A07850", rough=0.5)
    book_mats = [L.mat(f"MATTE_Book{i}", c) for i, c in
                 enumerate(["#C0392B", "#2980B9", "#27AE60", "#F39C12", "#8E44AD", "#2C3E50", "#D35400", "#16A085"])]
    w, d, h = 0.95, 0.32, 1.9
    parts = [
        L.box("SideL", (0.025, d, h), (-w / 2, 0, h / 2), wood),
        L.box("SideR", (0.025, d, h), (w / 2, 0, h / 2), wood),
        L.box("Back", (w, 0.01, h), (0, d / 2 - 0.005, h / 2), wood),
    ]
    shelves = [0.04, 0.42, 0.8, 1.18, 1.56, 1.89]
    for z in shelves:
        parts.append(L.box("Shelf", (w, d, 0.025), (0, 0, z), wood))
    books = []
    for z0, z1 in zip(shelves[:-1], shelves[1:]):
        x = -w / 2 + 0.03
        while x < w / 2 - 0.08:
            bw = rng.uniform(0.025, 0.05)
            bh = rng.uniform(0.2, min(0.33, z1 - z0 - 0.04))
            if rng.random() < 0.12:
                x += 0.08
                continue
            tilt = rng.uniform(-4, 4) if rng.random() < 0.2 else 0
            books.append(L.box("Book", (bw, rng.uniform(0.18, 0.24), bh), (x + bw / 2, 0.0, z0 + 0.0125 + bh / 2),
                               book_mats[rng.randrange(len(book_mats))], bevel=0.002, rot=(0, tilt, 0)))
            x += bw + 0.003
    L.join("Bookshelf", parts + books)


def sofa():
    fabric = L.mat("MATTE_Sofa", "#C8553D")
    cushion = L.mat("MATTE_SofaCushion", "#D86A52")
    legs = L.mat("Oak", "#A07850", rough=0.5)
    parts = [L.box("Base", (1.8, 0.85, 0.25), (0, 0, 0.28), fabric, bevel=0.03, segments=3)]
    parts.append(L.box("Back", (1.8, 0.22, 0.5), (0, 0.32, 0.6), fabric, bevel=0.06, segments=3))
    for sx in (-1, 1):
        parts.append(L.box("Arm", (0.18, 0.85, 0.32), (sx * 0.81, 0, 0.5), fabric, bevel=0.06, segments=3))
        parts.append(L.box("Seat", (0.7, 0.62, 0.12), (sx * 0.36, -0.08, 0.46), cushion, bevel=0.04, segments=3))
        parts.append(L.box("BackCushion", (0.68, 0.16, 0.36), (sx * 0.36, 0.16, 0.67), cushion, bevel=0.06, segments=3,
                           rot=(-10, 0, 0)))
        for sy in (-1, 1):
            parts.append(L.cyl("Leg", 0.025, 0.15, (sx * 0.82, sy * 0.36, 0.075), legs, radius2=0.018))
    L.join("Sofa", parts)


def rug():
    parts = []
    colors = ["#E0C9A6", "#B5838D", "#E0C9A6", "#6D597A"]
    for i, (r, c) in enumerate(zip((1.15, 0.95, 0.7, 0.35), colors)):
        parts.append(L.cyl(f"Ring{i}", r, 0.008, (0, 0, 0.004 + i * 0.0008), L.mat(f"MATTE_Rug{i}", c), verts=56))
    L.join("Rug", parts)


def floor_plant():
    rng = random.Random(9)
    pot = L.mat("GLOSS_PotWhite", "#EDEDE8")
    leaf = [L.mat("Leaf_Dark", "#2F6B3A", rough=0.5), L.mat("Leaf_B", "#8CBF88", rough=0.5), L.mat("Leaf_A", "#6DA06F", rough=0.5)]
    stem = L.mat("Stem", "#4E6B3A")
    parts = [L.cyl("Pot", 0.2, 0.38, (0, 0, 0.19), pot, radius2=0.16, verts=28),
             L.cyl("Soil", 0.185, 0.01, (0, 0, 0.37), L.mat("MATTE_Soil", "#4A3426"), verts=28)]
    for i in range(11):
        a = rng.uniform(0, 2 * math.pi)
        tilt = rng.uniform(15, 50)
        length = rng.uniform(0.5, 0.95)
        tx = math.sin(math.radians(tilt)) * math.cos(a) * length
        ty = math.sin(math.radians(tilt)) * math.sin(a) * length
        tz = 0.37 + math.cos(math.radians(tilt)) * length
        parts.append(L.limb("Stem", (0, 0, 0.37), (tx, ty, tz), 0.008, stem))
        lf = L.sphere("Leaf", 0.16, (tx, ty, tz), leaf[i % 3], scale=(0.55, 1.0, 0.08), segs=14, rings=8)
        lf.rotation_euler = (math.radians(rng.uniform(-25, 25)), math.radians(tilt * 0.6), a - math.pi / 2)
        L.apply_transform(lf, location=False)
        parts.append(lf)
    L.join("FloorPlant", parts)


def trash_can():
    rng = random.Random(4)
    can = L.mat("GLOSS_Black", "#151515")
    L.join("Can", [
        L.cyl("Bottom", 0.13, 0.01, (0, 0, 0.005), can, verts=24),
        L.cyl("Rim", 0.16, 0.012, (0, 0, 0.34), can, verts=24),
    ] + [
        L.box(f"Wire{i}", (0.008, 0.008, 0.34), (math.cos(i * math.pi / 9) * 0.145, math.sin(i * math.pi / 9) * 0.145, 0.17),
              can, rot=(0, 0, 0))
        for i in range(18)
    ])
    paper = L.mat("MATTE_Paper", "#FBFAF5")
    for i in range(6):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0, 0.07) if i < 4 else rng.uniform(0.18, 0.3)
        z = 0.06 + i * 0.05 if i < 4 else 0.035
        L.sphere(f"Paper{i + 1}", 0.04, (math.cos(a) * r, math.sin(a) * r, z), paper, segs=7, rings=5,
                 scale=(1, 0.85, 0.9))


def plaque():
    """Wall plaque; origin at the centre back. Unity adds the engraved text."""
    L.join("Plaque", [
        L.box("Wood", (0.3, 0.02, 0.38), (0, 0, 0), L.mat("Walnut", "#6B4630", rough=0.35), bevel=0.006),
        L.box("Brass", (0.24, 0.006, 0.28), (0, -0.012, 0), L.mat("METAL_Brass", "#C9A24B"), bevel=0.003),
    ])


def door():
    """Door panel and frame for the right-wall opening. Origin at the floor, centre of the opening, wall plane."""
    wood = L.mat("DoorWood", "#B08D6A", rough=0.45)
    frame = L.mat("GLOSS_WindowFrame", "#F4F4F4")
    dw = DOOR_Y1 - DOOR_Y0
    L.join("Door", [
        L.box("Panel", (0.045, dw - 0.06, DOOR_Z1 - 0.03), (0, 0, (DOOR_Z1 - 0.03) / 2), wood, bevel=0.004),
        L.box("FrameT", (0.16, dw + 0.1, 0.06), (0, 0, DOOR_Z1 + 0.0), frame),
        L.box("FrameA", (0.16, 0.05, DOOR_Z1), (0, -dw / 2 - 0.0, DOOR_Z1 / 2), frame),
        L.box("FrameB", (0.16, 0.05, DOOR_Z1), (0, dw / 2 + 0.0, DOOR_Z1 / 2), frame),
        L.sphere("Knob", 0.03, (-0.05, dw / 2 - 0.12, 1.0), L.mat("METAL_Brass", "#C9A24B")),
        L.box("Sign", (0.01, 0.22, 0.08), (-0.03, 0, 1.6), L.mat("GLOSS_Black", "#151515")),
    ])


ASSETS = {
    "room_shell": room_shell,
    "window": window,
    "skyline": skyline,
    "ceiling_light": ceiling_light,
    "cubicle_partition": cubicle_partition,
    "wall_clock": wall_clock,
    "calendar": calendar,
    "poster": poster,
    "whiteboard": whiteboard,
    "bookshelf": bookshelf,
    "sofa": sofa,
    "rug": rug,
    "floor_plant": floor_plant,
    "trash_can": trash_can,
    "plaque": plaque,
    "door": door,
}
