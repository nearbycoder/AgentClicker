"""Monitors, keyboards and other desk electronics. Origins at the bottom; fronts face -Y."""
import math
import random

import aclib as L

SCREEN_W, SCREEN_H = 0.608, 0.342   # 16:9 panel
PANEL_W, PANEL_H = 0.636, 0.372
SCREEN_CENTER_Z = 0.40              # above the desk surface (monitor with stand)


def _monitor_panel(cz, with_stand):
    bezel = L.mat("GLOSS_Bezel", "#16181C")
    parts = [L.box("Panel", (PANEL_W, 0.028, PANEL_H), (0, 0, cz), bezel, bevel=0.006)]
    parts.append(L.box("Back", (0.3, 0.05, 0.22), (0, 0.03, cz - 0.02), bezel, bevel=0.02))
    parts.append(L.box("Chin", (0.08, 0.004, 0.006), (0.27, -0.0145, cz - PANEL_H / 2 + 0.01),
                       L.mat("EMIT_LedWhite", "#DDE8FF", emit=1)))
    if with_stand:
        stand = L.mat("METAL_Stand", "#5A5F66")
        parts.append(L.box("Neck", (0.06, 0.03, 0.34), (0, 0.08, 0.2), stand, bevel=0.008, rot=(-6, 0, 0)))
        parts.append(L.box("Base", (0.26, 0.2, 0.014), (0, 0.06, 0.007), stand, bevel=0.006))
    body = L.join("Monitor", parts)
    # The screen stays separate so Unity can find it and place the UI canvas on it.
    L.box("Screen", (SCREEN_W, 0.002, SCREEN_H), (0, -0.0145, cz), L.mat("Screen", "#0B0F14", rough=0.1))
    return body


def monitor():
    _monitor_panel(SCREEN_CENTER_Z, True)


def monitor_nostand():
    _monitor_panel(0.0, False)


def monitor_pole():
    metal = L.mat("METAL_Stand", "#5A5F66")
    parts = [
        L.box("Clamp", (0.08, 0.1, 0.05), (0, 0, 0.025), metal, bevel=0.006),
        L.cyl("Pole", 0.02, 0.9, (0, 0, 0.45), metal),
        L.box("Bar", (0.96, 0.04, 0.04), (0, 0, 0.82), metal, bevel=0.008),
    ]
    for sx in (-1, 1):
        parts.append(L.box("Vesa", (0.1, 0.03, 0.1), (sx * 0.33, -0.03, 0.82), metal, bevel=0.006))
    L.join("MonitorPole", parts)


def _key_grid(parts, x0, y0, z, rows, cols, pitch, size, height, mat_fn):
    for r in range(rows):
        for c in range(cols):
            m = mat_fn(r, c)
            parts.append(L.box(f"K{r}_{c}", (size, size, height),
                               (x0 + c * pitch, y0 + r * pitch, z + height / 2), m, bevel=0.0015))


def keyboard_basic():
    base = L.mat("Plastic_Beige", "#D9D9D6", rough=0.5)
    keyc = L.mat("Plastic_Key", "#BDBDB8", rough=0.55)
    parts = [L.box("Base", (0.44, 0.15, 0.018), (0, 0, 0.009), base, bevel=0.004)]
    pitch = 0.0265
    _key_grid(parts, -0.2, -0.045, 0.018, 4, 15, pitch, 0.022, 0.007, lambda r, c: keyc)
    parts.append(L.box("Space", (0.15, 0.022, 0.007), (-0.02, -0.0715, 0.0215), keyc, bevel=0.0015))
    for i, x in enumerate((-0.2, -0.17, 0.09, 0.12, 0.2)):
        parts.append(L.box(f"Mod{i}", (0.022, 0.022, 0.007), (x, -0.0715, 0.0215), keyc, bevel=0.0015))
    L.join("KeyboardBasic", parts)


def keyboard_mech():
    case = L.mat("GLOSS_KbCase", "#2D2A32")
    cream = L.mat("Keycap_Cream", "#F1E9DA", rough=0.45)
    mod = L.mat("Keycap_Slate", "#7D8CA3", rough=0.45)
    accent = L.mat("Keycap_Orange", "#F28C28", rough=0.45)
    parts = [L.box("Case", (0.37, 0.135, 0.03), (0, 0, 0.015), case, bevel=0.006)]
    pitch = 0.0235

    def pick(r, c):
        if (r == 3 and c == 0) or (r == 1 and c == 14):
            return accent
        if c in (0, 14) or r == 0:
            return mod
        return cream
    _key_grid(parts, -0.165, -0.04, 0.03, 5, 15, pitch, 0.0205, 0.011, pick)
    # underglow strip
    parts.append(L.box("Glow", (0.36, 0.125, 0.004), (0, 0, 0.002), L.mat("EMIT_Underglow", "#00E5FF", emit=3)))
    L.join("KeyboardMech", parts)


def mouse():
    pad = L.mat("MATTE_MousePad", "#1F2328")
    shell = L.mat("GLOSS_MouseShell", "#1B1C20")
    parts = [
        L.box("Pad", (0.28, 0.23, 0.003), (0, 0, 0.0015), pad, bevel=0.001),
        L.sphere("Shell", 0.05, (0, 0.0, 0.016), shell, scale=(0.6, 1.0, 0.42)),
        L.box("Logo", (0.008, 0.012, 0.004), (0, 0.02, 0.035), L.mat("EMIT_MouseLogo", "#7C4DFF", emit=2)),
    ]
    L.join("Mouse", parts)


def stream_deck():
    body = L.mat("GLOSS_DeckBody", "#202226")
    parts = [L.box("Body", (0.12, 0.085, 0.018), (0, 0, 0.026), body, bevel=0.004, rot=(-12, 0, 0)),
             L.box("Stand", (0.1, 0.05, 0.02), (0, 0.015, 0.01), body, bevel=0.003)]
    colors = ["#FF3EA5", "#00E5FF", "#FFD23F", "#7CFF6B", "#7C4DFF"]
    for r in range(3):
        for c in range(5):
            m = L.mat(f"EMIT_Macro{(r * 5 + c) % 5}", colors[(r + c) % 5], emit=2)
            x = -0.044 + c * 0.022
            y = -0.025 + r * 0.023
            z = 0.036 + y * math.tan(math.radians(12))
            parts.append(L.box(f"Key{r}{c}", (0.017, 0.017, 0.005), (x, y, z), m, bevel=0.002, rot=(-12, 0, 0)))
    L.join("MacroPad", parts)


def headphones():
    black = L.mat("GLOSS_HeadphoneBlack", "#18191C")
    accent = L.mat("GLOSS_AccentRed", "#E63946")
    cushion = L.mat("MATTE_Cushion", "#2E3036")
    metal = L.mat("METAL_Stand", "#5A5F66")
    parts = [
        L.cyl("StandBase", 0.07, 0.012, (0, 0, 0.006), metal),
        L.cyl("StandPole", 0.008, 0.26, (0, 0, 0.13), metal),
        L.box("StandTop", (0.1, 0.03, 0.012), (0, 0, 0.262), metal, bevel=0.004),
    ]
    band = L.torus("Band", 0.085, 0.012, (0, 0, 0.2), black, rot=(90, 0, 0))
    # keep only the upper half of the band
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(band.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.01], context="VERTS")
    bm.to_mesh(band.data)
    bm.free()
    parts.append(band)
    for sx in (-1, 1):
        parts.append(L.cyl("Cup", 0.045, 0.035, (sx * 0.09, 0, 0.19), accent, rot=(0, 90, 0)))
        parts.append(L.cyl("Pad", 0.042, 0.02, (sx * 0.07, 0, 0.19), cushion, rot=(0, 90, 0)))
    L.join("Headphones", parts)


def server_rack():
    rng = random.Random(7)
    cab = L.mat("GLOSS_Rack", "#1B1D22")
    unit = L.mat("Server", "#2E3138", rough=0.4)
    face = L.mat("METAL_ServerFace", "#3F444C")
    parts = [
        L.box("Bottom", (0.6, 0.6, 0.06), (0, 0, 0.03), cab, bevel=0.006),
        L.box("Top", (0.6, 0.6, 0.04), (0, 0, 1.18), cab, bevel=0.006),
        L.box("SideL", (0.03, 0.6, 1.16), (-0.285, 0, 0.6), cab, bevel=0.004),
        L.box("SideR", (0.03, 0.6, 1.16), (0.285, 0, 0.6), cab, bevel=0.004),
        L.box("BackPanel", (0.56, 0.02, 1.12), (0, 0.29, 0.6), cab),
    ]
    leds_a, leds_b = [], []
    z = 0.1
    for i in range(7):
        h = rng.choice((0.09, 0.09, 0.13))
        parts.append(L.box(f"Unit{i}", (0.52, 0.5, h - 0.01), (0, 0.02, z + h / 2), unit, bevel=0.003))
        parts.append(L.box(f"Face{i}", (0.52, 0.01, h - 0.02), (0, -0.235, z + h / 2), face, bevel=0.002))
        for j in range(6):
            led = L.box(f"Led{i}_{j}", (0.008, 0.004, 0.008), (0.12 + j * 0.022, -0.242, z + h / 2),
                        L.mat("EMIT_LedGreen", "#39FF88", emit=4) if j % 3 else L.mat("EMIT_LedAmber", "#FFB020", emit=4))
            (leds_a if (i + j) % 2 else leds_b).append(led)
        z += h + 0.012
    L.join("Rack", parts)
    L.join("LEDs_A", leds_a)
    L.join("LEDs_B", leds_b)
    L.box("Door", (0.56, 0.01, 1.1), (0, -0.3, 0.6), L.mat("GLASS_RackDoor", "#99AABB", alpha=0.2))


ASSETS = {
    "monitor": monitor,
    "monitor_nostand": monitor_nostand,
    "monitor_pole": monitor_pole,
    "keyboard_basic": keyboard_basic,
    "keyboard_mech": keyboard_mech,
    "mouse": mouse,
    "stream_deck": stream_deck,
    "headphones": headphones,
    "server_rack": server_rack,
}
