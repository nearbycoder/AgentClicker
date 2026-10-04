"""Desk gadgets and clutter. Origins at the bottom centre; fronts face -Y."""
import math
import random

import aclib as L


def mug():
    body = L.mat("GLOSS_MugBlue", "#2F6DB5")
    white = L.mat("GLOSS_White", "#F4F4F2")
    parts = [
        L.cyl("Body", 0.042, 0.1, (0, 0, 0.05), body, verts=28),
        L.cyl("Stripe", 0.0425, 0.018, (0, 0, 0.07), white, verts=28),
        L.cyl("Coffee", 0.037, 0.004, (0, 0, 0.088), L.mat("GLOSS_Coffee", "#3B2416"), verts=28),
    ]
    handle = L.torus("Handle", 0.026, 0.008, (0.045, 0, 0.052), body, rot=(90, 0, 0), major_segs=20, minor_segs=8)
    parts.append(handle)
    L.join("Mug", parts)


def rubber_duck():
    yellow = L.mat("GLOSS_DuckYellow", "#FFD23F")
    orange = L.mat("GLOSS_Beak", "#F28C28")
    black = L.mat("GLOSS_Eye", "#111111")
    parts = [
        L.sphere("Body", 0.05, (0, 0.005, 0.038), yellow, scale=(0.9, 1.25, 0.75)),
        L.sphere("Tail", 0.02, (0, 0.06, 0.06), yellow, scale=(0.8, 1.0, 1.0)),
        L.sphere("Head", 0.03, (0, -0.03, 0.09), yellow),
        L.sphere("Beak", 0.016, (0, -0.06, 0.085), orange, scale=(1.1, 1.0, 0.45)),
    ]
    for sx in (-1, 1):
        parts.append(L.sphere("Eye", 0.0055, (sx * 0.014, -0.052, 0.1), black))
    L.join("Duck", parts)


def plant_succulent():
    rng = random.Random(3)
    pot = L.mat("MATTE_Terracotta", "#C66B3D")
    soil = L.mat("MATTE_Soil", "#4A3426")
    greens = [L.mat("Leaf_A", "#6DA06F", rough=0.5), L.mat("Leaf_B", "#8CBF88", rough=0.5)]
    parts = [
        L.cyl("Pot", 0.055, 0.08, (0, 0, 0.04), pot, radius2=0.045, verts=24),
        L.cyl("Rim", 0.06, 0.015, (0, 0, 0.078), pot, verts=24),
        L.cyl("Soil", 0.05, 0.004, (0, 0, 0.083), soil, verts=24),
    ]
    for ring, (count, r, tilt, size) in enumerate(((8, 0.032, 62, 0.03), (6, 0.018, 40, 0.026), (4, 0.006, 15, 0.02))):
        for i in range(count):
            a = 360 * i / count + ring * 22 + rng.uniform(-6, 6)
            ar = math.radians(a)
            leaf = L.sphere(f"Leaf{ring}_{i}", size, (math.cos(ar) * r, math.sin(ar) * r, 0.1 + ring * 0.012),
                            greens[(i + ring) % 2], scale=(0.45, 0.25, 1.0), segs=12, rings=8)
            leaf.rotation_euler = (0, math.radians(tilt), ar)
            L.apply_transform(leaf, location=False)
            parts.append(leaf)
    L.join("Succulent", parts)


def lava_lamp():
    base = L.mat("METAL_LampPurple", "#6A4C93")
    L.join("Lamp", [
        L.cyl("Base", 0.05, 0.08, (0, 0, 0.04), base, radius2=0.032, verts=24),
        L.cyl("Cap", 0.026, 0.045, (0, 0, 0.335), base, radius2=0.012, verts=24),
    ])
    # Tinted translucent glass with three glowing blobs inside (animated by Unity).
    L.cyl("Glass", 0.034, 0.24, (0, 0, 0.2), L.mat("GLASS_LavaLiquid", "#E0457B", alpha=0.35), radius2=0.024, verts=24)
    lava = L.mat("EMIT_Lava", "#FF6B35", emit=4)
    for i, (z, r) in enumerate(((0.12, 0.016), (0.2, 0.012), (0.27, 0.01))):
        L.sphere(f"Blob{i + 1}", r, (0.004 * (i - 1), 0, z), lava, scale=(1, 1, 1.3))


def espresso_machine():
    chrome = L.mat("METAL_Chrome", "#C9CDD2")
    red = L.mat("GLOSS_EspressoRed", "#C0392B")
    black = L.mat("GLOSS_Black", "#151515")
    parts = [
        L.box("Body", (0.26, 0.3, 0.3), (0, 0.02, 0.17), red, bevel=0.02, segments=3),
        L.box("TopTray", (0.26, 0.28, 0.02), (0, 0.02, 0.33), chrome, bevel=0.006),
        L.box("Plinth", (0.28, 0.32, 0.03), (0, 0.02, 0.015), chrome, bevel=0.006),
        L.box("DripTray", (0.2, 0.1, 0.02), (0, -0.16, 0.035), chrome, bevel=0.004),
        L.cyl("Group", 0.03, 0.04, (0, -0.15, 0.22), chrome),
        L.cyl("Portafilter", 0.032, 0.025, (0, -0.15, 0.188), black),
        L.box("PfHandle", (0.02, 0.1, 0.018), (0, -0.22, 0.19), black, bevel=0.006, rot=(-8, 0, 0)),
        L.cyl("Gauge", 0.028, 0.012, (0.075, -0.13, 0.27), chrome, rot=(90, 0, 0)),
        L.cyl("GaugeFace", 0.022, 0.004, (0.075, -0.137, 0.27), L.mat("GLOSS_White", "#F4F4F2"), rot=(90, 0, 0)),
        L.cyl("Wand", 0.006, 0.14, (-0.11, -0.14, 0.17), chrome, rot=(10, 0, 0)),
        L.cyl("Cup", 0.025, 0.04, (0, -0.15, 0.065), L.mat("GLOSS_White", "#F4F4F2"), radius2=0.02),
    ]
    L.join("Espresso", parts)


def mini_fridge():
    body = L.mat("GLOSS_FridgeMint", "#9ED9C8")
    chrome = L.mat("METAL_Chrome", "#C9CDD2")
    parts = [
        L.box("Body", (0.48, 0.5, 0.78), (0, 0, 0.41), body, bevel=0.03, segments=3),
        L.box("Seam", (0.46, 0.004, 0.005), (0, -0.252, 0.6), L.mat("Seam", "#6F9C8F")),
        L.box("Handle", (0.025, 0.03, 0.3), (0.19, -0.27, 0.42), chrome, bevel=0.008),
        L.box("Badge", (0.12, 0.004, 0.03), (-0.12, -0.252, 0.72), chrome, bevel=0.002),
    ]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(L.cyl("Foot", 0.02, 0.02, (sx * 0.2, sy * 0.2, 0.01), chrome))
    L.join("Fridge", parts)


def neon_sign():
    backing = L.box("Backing", (0.95, 0.012, 0.34), (0, 0.02, 0), L.mat("GLASS_Acrylic", "#C9F2FF", alpha=0.25), bevel=0.01)
    neon = L.mat("EMIT_NeonPink", "#FF3EA5", emit=6)
    txt = L.text_mesh("Neon", "SHIP IT", size=0.24, bevel=0.007, material=neon, loc=(0, 0.0, 0), fill_mode="NONE",
                      resolution=8)
    # standoffs
    chrome = L.mat("METAL_Chrome", "#C9CDD2")
    for sx in (-1, 1):
        for sz in (-1, 1):
            L.cyl("Standoff", 0.01, 0.03, (sx * 0.43, 0.035, sz * 0.13), chrome, rot=(90, 0, 0))
    return None


def name_plate():
    wood = L.mat("Walnut", "#6B4630", rough=0.35)
    brass = L.mat("METAL_Brass", "#C9A24B")
    base = L.box("Base", (0.24, 0.06, 0.02), (0, 0, 0.01), wood, bevel=0.004)
    face = L.box("Face", (0.22, 0.008, 0.055), (0, -0.004, 0.046), brass, bevel=0.002, rot=(-18, 0, 0))
    L.join("NamePlate", [base, face])


def factory_toy():
    wall = L.mat("GLOSS_FactoryWall", "#E8E1D5")
    roof = L.mat("GLOSS_FactoryRoof", "#5B6C7D")
    glow = L.mat("EMIT_FactoryGlow", "#3DFFC5", emit=4)
    belt = L.mat("Belt", "#2B2D31")
    parts = [
        L.box("Plinth", (0.26, 0.18, 0.015), (0, 0, 0.0075), L.mat("GLOSS_Black", "#151515"), bevel=0.003),
        L.box("Hall", (0.16, 0.1, 0.06), (0.02, 0.02, 0.045), wall, bevel=0.002),
        L.cyl("Chimney", 0.012, 0.11, (-0.045, 0.045, 0.07), roof),
        L.cyl("ChimneyTop", 0.014, 0.01, (-0.045, 0.045, 0.125), glow),
        L.box("Belt", (0.24, 0.035, 0.01), (0, -0.06, 0.02), belt, bevel=0.002),
    ]
    for i in range(3):  # saw-tooth roof
        tooth = L.box(f"Tooth{i}", (0.05, 0.1, 0.03), (-0.035 + i * 0.053, 0.02, 0.083), roof, rot=(0, 30, 0))
        parts.append(tooth)
    for i in range(4):
        parts.append(L.box(f"Win{i}", (0.022, 0.003, 0.018), (-0.03 + i * 0.035, -0.031, 0.045), glow))
    for i in range(4):
        parts.append(L.box(f"Crate{i}", (0.022, 0.022, 0.018), (-0.1 + i * 0.065, -0.06, 0.034),
                           L.mat("Crate", "#C9A26B")))
    L.join("Factory", parts)


def photo_frame():
    frame = L.mat("GLOSS_Black", "#151515")
    L.join("Frame", [
        L.box("Frame", (0.13, 0.012, 0.1), (0, 0, 0.05), frame, bevel=0.003, rot=(-12, 0, 0)),
        L.box("Leg", (0.02, 0.06, 0.005), (0, 0.035, 0.03), frame, rot=(45, 0, 0)),
    ])
    L.box("Photo", (0.11, 0.003, 0.08), (0, -0.0075, 0.051), L.mat("MATTE_Photo", "#7FB3D5"), rot=(-12, 0, 0))
    # a tiny "family photo": two silhouettes
    sil = L.mat("MATTE_PhotoPeople", "#F2D7B6")
    L.join("Silhouettes", [
        L.sphere("H1", 0.012, (-0.02, -0.0105, 0.064), sil, scale=(1, 0.2, 1)),
        L.sphere("H2", 0.011, (0.022, -0.0105, 0.06), sil, scale=(1, 0.2, 1)),
        L.sphere("B1", 0.02, (-0.02, -0.0095, 0.035), sil, scale=(1, 0.2, 0.9)),
        L.sphere("B2", 0.019, (0.022, -0.0095, 0.032), sil, scale=(1, 0.2, 0.9)),
    ])


def sticky_notes():
    colors = ["#FFE066", "#FF8FAB", "#A0E7A0", "#8ECAE6"]
    rng = random.Random(11)
    for i, c in enumerate(colors):
        L.box(f"Note{i}", (0.05, 0.002, 0.05), (i * 0.058, -0.001 * i, rng.uniform(-0.006, 0.006)),
              L.mat(f"MATTE_Note{i}", c), rot=(0, rng.uniform(-8, 8), 0))


def pizza_box():
    card = L.mat("MATTE_Cardboard", "#C9A26B")
    logo = L.mat("MATTE_PizzaLogo", "#C0392B")
    parts = []
    for i, rot in enumerate((0, 7)):
        z = 0.02 + i * 0.041
        parts.append(L.box(f"Box{i}", (0.36, 0.36, 0.04), (0.01 * i, -0.01 * i, z), card, bevel=0.003, rot=(0, 0, rot)))
        parts.append(L.cyl(f"Logo{i}", 0.06, 0.002, (0.01 * i, -0.01 * i, z + 0.021), logo, verts=24))
    L.join("PizzaBoxes", parts)


def trophy():
    gold = L.mat("METAL_Gold", "#E6B422")
    base = L.mat("GLOSS_Black", "#151515")
    parts = [
        L.box("Base", (0.1, 0.1, 0.04), (0, 0, 0.02), base, bevel=0.004),
        L.box("Plate", (0.06, 0.003, 0.02), (0, -0.051, 0.02), gold),
        L.cyl("Stem", 0.01, 0.06, (0, 0, 0.07), gold),
        L.cyl("Knob", 0.022, 0.015, (0, 0, 0.1), gold),
        L.cyl("Cup", 0.05, 0.09, (0, 0, 0.15), gold, radius2=0.03, rot=(180, 0, 0)),
    ]
    for sx in (-1, 1):
        parts.append(L.torus("Handle", 0.022, 0.005, (sx * 0.055, 0, 0.16), gold, rot=(90, 0, 0), major_segs=16, minor_segs=6))
    L.join("Trophy", parts)


def desk_phone():
    """Office desk phone. The handset and LED are separate objects so Unity can ring/lift them."""
    body = L.mat("GLOSS_PhoneBody", "#2A2D33")
    key = L.mat("Plastic_Key", "#BDBDB8", rough=0.55)
    L.join("PhoneBase", [
        L.box("Base", (0.22, 0.2, 0.05), (0, 0, 0.025), body, bevel=0.01),
        L.box("Slope", (0.12, 0.16, 0.03), (0.04, 0.0, 0.055), body, bevel=0.008, rot=(-14, 0, 0)),
        L.box("Display", (0.08, 0.004, 0.03), (0.04, -0.06, 0.068), L.mat("EMIT_PhoneScreen", "#7FE3C4", emit=1.2), rot=(-14 + 90, 0, 0)),
        L.box("Cradle", (0.07, 0.19, 0.02), (-0.065, 0, 0.055), body, bevel=0.006),
    ] + [
        L.box(f"Key{r}{c}", (0.022, 0.016, 0.006), (0.012 + c * 0.028, -0.035 + r * 0.022, 0.071 + r * 0.005), key, bevel=0.002, rot=(-14, 0, 0))
        for r in range(4) for c in range(3)
    ])
    handset = L.join("Handset", [
        L.box("Grip", (0.05, 0.2, 0.03), (-0.065, 0, 0.08), body, bevel=0.012),
        L.box("Ear", (0.055, 0.05, 0.035), (-0.065, 0.08, 0.077), body, bevel=0.014),
        L.box("Mouth", (0.055, 0.05, 0.035), (-0.065, -0.08, 0.077), body, bevel=0.014),
    ])
    L.set_origin(handset, (-0.065, 0, 0.065))
    led = L.box("Led", (0.012, 0.012, 0.006), (0.085, 0.075, 0.053), L.mat("EMIT_PhoneLed", "#FF4D4D", emit=4))
    cord = L.join("Cord", [L.torus(f"Coil{i}", 0.008, 0.002, (-0.11 - i * 0.004, -0.07 + i * 0.006, 0.02), body, rot=(0, 90, 0),
                                   major_segs=10, minor_segs=4) for i in range(10)])


ASSETS = {
    "desk_phone": desk_phone,
    "mug": mug,
    "rubber_duck": rubber_duck,
    "plant_succulent": plant_succulent,
    "lava_lamp": lava_lamp,
    "espresso_machine": espresso_machine,
    "mini_fridge": mini_fridge,
    "neon_sign": neon_sign,
    "name_plate": name_plate,
    "factory_toy": factory_toy,
    "photo_frame": photo_frame,
    "sticky_notes": sticky_notes,
    "pizza_box": pizza_box,
    "trophy": trophy,
}
