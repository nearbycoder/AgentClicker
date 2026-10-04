#!/usr/bin/env python3
"""Edits the recorded trailer shots into the store trailer, the README teaser loop and screenshots.

Input  (Tools/make_trailer.sh records it): Recordings/trailer/clips/*.mp4  one clip per shot, game SFX only
                                            Recordings/trailer/music_*.wav  the game's lo-fi loop, several seeds
                                            Recordings/trailer/sfx_*.wav    the game's synthesised stingers
                                            Recordings/trailer/stills/*.png  cursor-free screenshots
Output (docs/media): trailer.mp4, trailer-poster.jpg, teaser.webp, screenshots/*.jpg

Needs ffmpeg (libx264, libwebp) and Pillow + numpy:
    python3 -m venv .venv && .venv/bin/pip install pillow numpy && .venv/bin/python Tools/make_trailer.py
Steps can be run on their own: make_trailer.py [segments] [trailer] [teaser] [poster] [stills]
"""
import json
import math
import os
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Recordings" / "trailer"
CLIPS = SRC / "clips"
BUILD = SRC / "build"
OUT = ROOT / "docs" / "media"
FONTS = ROOT / "Unity" / "Assets" / "Resources" / "Fonts"

W, H, FPS = 1920, 1080, 30
BEAT = 60 / 76  # the game's lo-fi loop runs at 76 BPM

# CorpOS palette (Unity/Assets/Scripts/UI/UIKit.cs)
BG = (14, 20, 32)
PANEL = (21, 29, 43)
ACCENT = (77, 208, 225)
ACCENT2 = (124, 77, 255)
GOLD = (255, 209, 102)
TEXT = (230, 237, 247)
DIM = (138, 151, 173)
TERMINAL = (124, 255, 178)


def font(weight, size):
    name = {"bold": "FiraSans-Bold.ttf", "semi": "FiraSans-SemiBold.ttf", "medium": "FiraSans-Medium.ttf",
            "regular": "FiraSans-Regular.ttf", "mono": "DejaVuSansMono.ttf", "monobold": "DejaVuSansMono-Bold.ttf"}[weight]
    return ImageFont.truetype(str(FONTS / name), size)


def run(cmd, **kw):
    print("  $", " ".join(str(c) for c in cmd)[:240], flush=True)
    subprocess.run([str(c) for c in cmd], check=True, **kw)


def ffmpeg(*args):
    run(["nice", "-n", "10", "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", *args])


def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "json", str(path)],
                         check=True, capture_output=True, text=True).stdout
    return float(json.loads(out)["format"]["duration"])


def ease_out(k):
    k = min(max(k, 0.0), 1.0)
    return 1 - (1 - k) ** 3


def ease_in_out(k):
    k = min(max(k, 0.0), 1.0)
    return k * k * (3 - 2 * k)


# ============================================================================ the edit
# Every beat: the clip, where to start in it, how long it runs, and the caption that explains it.
# Shot timings come from Unity/Assets/Scripts/Util/Trailer.cs (each clip starts at its Rec() call).
# Transitions: ("fade" | "slideleft" | "fadeblack" | "cut", seconds) into the NEXT segment.
SEGMENTS = [
    # cold open: the payoff first. Boot screen, the camera pulls back, feet on the desk.
    dict(id="cold", clip="factory", ss=7.3, t=6.8, out=("fadeblack", 0.5)),
    dict(id="title", card="title", t=5.0, out=("fade", 0.45)),

    dict(id="story", clip="story_intro", ss=0.2, t=2.6, out=("fade", 0.3),
         cap=("CHAPTER 1 · THE MANDATE", "Your CEO wants 10x output.", "Synergex just went AI-First. Sam has an idea."), pos="bl"),
    dict(id="clockin", clip="clockin", ss=1.2, t=4.6, out=("fade", 0.3),
         cap=("EVERY MORNING", "Clock in at a real 3D desk", "Log in to CorpOS, a whole game running on your monitor."), pos="bl"),
    dict(id="ship", clip="ship", ss=0.4, t=4.4, out=("slideleft", 0.35),
         cap=("THE CORE LOOP", "Ship code by hand", "Keep a rhythm to build Focus, up to x3 click power."), pos="br"),
    dict(id="hire", clip="hire", ss=0.2, t=5.0, out=("slideleft", 0.35),
         cap=("MODELMART", "Hire AI agents", "20 agents from 8 fictional frontier labs. They work while you don't."), pos="bl"),
    dict(id="upgrades", clip="upgrades", ss=0.4, t=3.4, out=("fade", 0.3),
         cap=("UPGRADES", "Stack the multipliers", "15 tiers per agent, 40 research upgrades, lab contracts."), pos="bl"),
    dict(id="gadget", clip="gadget", ss=0.5, t=4.0, out=("fade", 0.3),
         cap=("THE OFFICE", "Upgrade your desk", "18 gadgets show up in the 3D office, and every one has a real bonus."), pos="bl"),
    dict(id="drop", clip="drop", ss=0.2, t=3.6, out=("cut", 0),
         cap=("RANDOM EVENTS", "Catch the model drop", "Benchmark hype, funding rounds, caffeine rushes."), pos="tr"),
    dict(id="outage", clip="outage", ss=0.5, t=3.3, out=("fade", 0.3),
         cap=("RANDOM EVENTS", "Survive the outage", "Click the banner to fail over before production halves."), pos="bl"),
    dict(id="call", clip="call", ss=1.6, t=3.9, out=("fade", 0.25),
         cap=("INTERRUPTIONS", "The phone rings", "Every reply has consequences. Rapport unlocks perks."), pos="tl"),
    dict(id="call2", clip="call", ss=7.1, t=2.7, out=("fade", 0.3), cap_from="call"),
    dict(id="inbox", clip="inbox", ss=0.3, t=2.8, out=("fade", 0.3),
         cap=("THE STORY", "Six chapters, 26 emails", "A workplace comedy about automating your own job."), pos="bl"),
    dict(id="review", clip="review", ss=1.3, t=3.6, out=("fade", 0.25),
         cap=("5:00 PM", "Hit your quota", "Daily asks, performance reviews, stars and a night shift for your agents."), pos="bl"),
    dict(id="review2", clip="review", ss=6.8, t=2.4, out=("fadeblack", 0.4), cap_from="review"),
    dict(id="day1", clip="office_day1", ss=0.3, t=2.6, out=("fade", 0.9),
         cap=("DAY 1 → DAY 31", "Watch the office grow", "Promotions knock the cubicle down. Gadgets pile up."), pos="bl"),
    dict(id="late", clip="office_late", ss=1.2, t=3.0, out=("fade", 0.3), cap_from="day1"),
    dict(id="promotion", clip="promotion", ss=0.4, t=3.8, out=("fadeblack", 0.4),
         cap=("PROMOTIONS", "From Junior Developer to Chief Agent Officer", "…and on to Employee of Every Month."), pos="bl"),
    dict(id="factory", clip="factory", ss=0.3, t=6.6, out=("fade", 0.3),
         cap=("THE GOAL", "Build the Software Factory", "Every agent in one pipeline. 100% automated."), pos="bl"),
    dict(id="frontier", clip="frontier", ss=0.3, t=4.2, out=("slideleft", 0.35),
         cap=("THE FACTORY IS NOT THE END", "Frontier agents", "Agent Foundry, Dyson Swarm… all the way to The Singularity."), pos="br"),
    dict(id="trophies", clip="trophies", ss=0.3, t=2.7, out=("fade", 0.3),
         cap=("512 TROPHIES", "Earn Clout", "Every trophy feeds the influence upgrades."), pos="bl"),
    dict(id="reorg", clip="reorg", ss=0.3, t=4.6, out=("fade", 0.3),
         cap=("PRESTIGE", "Reorg to the next division", "Marketing, Sales, Legal… The Board, Orbital, the multiverse."), pos="br"),
    dict(id="board", clip="boardroom", ss=0.2, t=3.4, out=("fade", 0.3),
         cap=("THE BOARD ROOM", "Spend your Stock Options", "Permanent perks that survive every reorg."), pos="bl"),
    dict(id="big", clip="bignumbers", ss=0.3, t=3.0, out=("fadeblack", 0.35),
         cap=("HUNDREDS OF HOURS IN", "Numbers to a centillion", "1e303. Nothing ever overflows."), pos="br"),
    dict(id="montage", card="montage", out=("fadeblack", 0.5)),
    dict(id="end", card="end", t=7.0, out=None),
]

# The escalation montage: one musical beat per cut, a word every two cuts.
MONTAGE = [
    ("ship", 2.0), ("drop", 2.3),             # SHIP.
    ("hire", 1.5), ("frontier", 1.8),         # HIRE.
    ("gadget", 2.2), ("promotion", 1.5),      # UPGRADE.
    ("factory", 4.0), ("factory", 9.6),       # AUTOMATE.
    ("reorg", 3.0), ("boardroom", 0.6),       # REORG.
    ("bignumbers", 1.0), ("sam", 0.4),        # REPEAT.
]
MONTAGE_WORDS = ["SHIP.", "HIRE.", "UPGRADE.", "AUTOMATE.", "REORG.", "REPEAT."]


# ============================================================================ captions
def caption_png(kicker, title, sub, path):
    """A CorpOS-style lower-third card: accent bar, kicker, headline, one line of context."""
    fk, ft, fs = font("bold", 26), font("bold", 62), font("medium", 31)
    pad_x, pad_y, bar = 40, 30, 8
    d = ImageDraw.Draw(Image.new("RGBA", (10, 10)))

    def width(text, f, spacing=0):
        return d.textlength(text, font=f) + spacing * max(0, len(text) - 1)

    tw = max(width(kicker, fk, 4), width(title, ft), width(sub, fs))
    w = int(tw + pad_x * 2 + bar)
    h = int(pad_y * 2 + 30 + 14 + 74 + 10 + 38)
    shadow = 40
    img = Image.new("RGBA", (w + shadow * 2, h + shadow * 2), (0, 0, 0, 0))
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([shadow, shadow + 8, shadow + w, shadow + h + 8], 18, fill=(0, 0, 0, 150))
    img = Image.alpha_composite(img, sh.filter(ImageFilter.GaussianBlur(16)))
    dr = ImageDraw.Draw(img)
    x0, y0 = shadow, shadow
    dr.rounded_rectangle([x0, y0, x0 + w, y0 + h], 18, fill=PANEL + (236,), outline=(42, 53, 72, 255), width=2)
    dr.rounded_rectangle([x0, y0, x0 + bar + 10, y0 + h], 18, fill=ACCENT + (255,))
    dr.rectangle([x0 + bar, y0 + 2, x0 + bar + 12, y0 + h - 2], fill=PANEL + (236,))
    x = x0 + bar + pad_x
    y = y0 + pad_y
    cx = x
    for ch in kicker:                                   # letter-spaced kicker
        dr.text((cx, y), ch, font=fk, fill=ACCENT + (255,))
        cx += d.textlength(ch, font=fk) + 4
    y += 30 + 14
    dr.text((x, y - 6), title, font=ft, fill=(255, 255, 255, 255))
    y += 74 + 10
    dr.text((x, y), sub, font=fs, fill=(184, 196, 217, 255))
    img.save(path)
    return img.size, shadow


def caption_filter(size, shadow, pos, start, end):
    """ffmpeg overlay for a caption PNG (input [1]) that slides and fades in at `start` and out at `end`."""
    w, h = size
    margin_x, margin_y = 84, 78
    x0 = margin_x - shadow if pos[1] == "l" else W - margin_x - w + shadow
    y0 = H - margin_y - h + shadow if pos[0] == "b" else margin_y - shadow
    sign = -1 if pos[1] == "l" else 1
    if start < 0:  # already up from the previous shot
        fades, x = "", str(x0)
    else:
        fades = f",fade=t=in:st={start:.3f}:d=0.35:alpha=1"
        x = f"'{x0}+({sign}*80*pow(1-min(max((t-{start:.3f})/0.5,0),1),3))'"
    return (f"[1:v]format=rgba{fades},fade=t=out:st={end:.3f}:d=0.3:alpha=1[cap];"
            f"[0:v][cap]overlay=x={x}:y={y0}:eval=frame:shortest=1")


# ============================================================================ segments
def render_clip_segment(i, seg, cap_info):
    out = BUILD / f"seg_{i:02d}.mkv"
    src = CLIPS / f"{seg['clip']}.mp4"
    t = seg["t"]
    have = duration(src) - seg["ss"]
    if have < t - 0.05:
        print(f"  ! {seg['clip']}: wanted {t:.2f}s from {seg['ss']}, clip only has {have:.2f}s")
        t = have
    args = ["-ss", f"{seg['ss']:.3f}", "-t", f"{t:.3f}", "-i", src]
    if cap_info:
        png, size, shadow, start, end = cap_info
        args += ["-loop", "1", "-framerate", str(FPS), "-t", f"{t:.3f}", "-i", png,
                 "-filter_complex", caption_filter(size, shadow, seg.get("pos", "bl"), start, end) + ",format=yuv420p[v]",
                 "-map", "[v]", "-map", "0:a?"]
    else:
        args += ["-vf", "format=yuv420p", "-map", "0:v", "-map", "0:a?"]
    ffmpeg(*args, "-r", FPS, "-c:v", "libx264", "-preset", "medium", "-crf", "14",
           "-c:a", "pcm_s16le", "-ar", "48000", "-ac", "2", out)
    return out, t


def blurred_background(clip, ss, t, sigma=10, dim=0.55):
    """Frames of a clip, blurred and darkened, as RGB numpy arrays (for the PIL-composited cards)."""
    proc = subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{ss}", "-t", f"{t}", "-i", str(CLIPS / f"{clip}.mp4"),
                           "-vf", f"gblur=sigma={sigma},eq=brightness=-0.04:saturation=0.85,fps={FPS}",
                           "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], check=True, capture_output=True)
    frames = np.frombuffer(proc.stdout, np.uint8).reshape(-1, H, W, 3)
    vign = vignette()
    return [(f.astype(np.float32) * dim * vign).clip(0, 255).astype(np.uint8) for f in frames]


_VIGNETTE = None


def vignette():
    global _VIGNETTE
    if _VIGNETTE is None:
        y, x = np.mgrid[0:H, 0:W]
        r = np.sqrt(((x - W / 2) / (W / 2)) ** 2 + ((y - H / 2) / (H / 2)) ** 2)
        _VIGNETTE = (1.0 - 0.45 * np.clip(r - 0.35, 0, 1) ** 1.5)[..., None].astype(np.float32)
    return _VIGNETTE


def encode_frames(path, frames_iter, n, audio=None):
    cmd = ["nice", "-n", "10", "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
           "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-"]
    if audio:
        cmd += ["-i", str(audio)]
    else:
        cmd += ["-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo"]
    cmd += ["-t", f"{n / FPS:.3f}", "-c:v", "libx264", "-preset", "medium", "-crf", "14", "-pix_fmt", "yuv420p",
            "-c:a", "pcm_s16le", "-ar", "48000", "-ac", "2", "-shortest", str(path)]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for f in frames_iter:
        p.stdin.write(np.asarray(f.convert("RGB")).tobytes())
    p.stdin.close()
    if p.wait() != 0:
        raise SystemExit(f"ffmpeg failed for {path}")


def text_layer(text, f, fill, spacing=0):
    d = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    w = int(d.textlength(text, font=f) + spacing * max(0, len(text) - 1)) + 8
    asc, desc = f.getmetrics()
    img = Image.new("RGBA", (w, asc + desc + 8), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    x = 4
    for ch in text if spacing else [text]:
        dr.text((x, 4), ch, font=f, fill=fill)
        x += d.textlength(ch, font=f) + spacing
    return img


def paste(canvas, layer, x, y, alpha=1.0, scale=1.0):
    if alpha <= 0.001:
        return
    if scale != 1.0:
        layer = layer.resize((max(1, int(layer.width * scale)), max(1, int(layer.height * scale))), Image.LANCZOS)
        x -= (layer.width - layer.width / scale) / 2
        y -= (layer.height - layer.height / scale) / 2
    if alpha < 1.0:
        layer = layer.copy()
        layer.putalpha(layer.getchannel("A").point(lambda a: int(a * alpha)))
    canvas.alpha_composite(layer, (int(round(x)), int(round(y))))


def logo_layers(size):
    fb = font("bold", size)
    return text_layer("AGENT", fb, (255, 255, 255, 255)), text_layer("CLICKER", fb, ACCENT + (255,))


def render_title(i, seg):
    t = seg["t"]
    n = int(round(t * FPS))
    bg = blurred_background("title_bg", 0.5, t + 0.1, sigma=7, dim=0.62)
    agent, clicker = logo_layers(200)
    gap = 44
    total = agent.width + gap + clicker.width
    lx = (W - total) / 2
    ly = H / 2 - agent.height / 2 - 70
    prompt = "$ ./agent-clicker --automate-everything"
    fm = font("mono", 34)
    tag = text_layer("Automate yourself out of a job. Keep the paycheck.", font("medium", 46), (200, 211, 230, 255))
    bar_w = total

    def frames():
        for k in range(n):
            s = k / FPS
            img = Image.fromarray(bg[min(k, len(bg) - 1)]).convert("RGBA")
            # terminal prompt types itself above the logo
            chars = int(min(len(prompt), max(0, (s - 0.05) / 0.022)))
            typed = text_layer(prompt[:chars] + ("█" if int(s * 3) % 2 == 0 or chars < len(prompt) else " "), fm, TERMINAL + (255,))
            paste(img, typed, (W - text_layer(prompt + "█", fm, TERMINAL).width) / 2, ly - 86, alpha=min(1, s / 0.2) * (1 - ease_in_out((s - t + 0.6) / 0.5)))
            a1 = ease_out((s - 0.55) / 0.55)
            paste(img, agent, lx - 120 * (1 - a1), ly, alpha=a1)
            a2 = ease_out((s - 0.75) / 0.55)
            paste(img, clicker, lx + agent.width + gap + 120 * (1 - a2), ly, alpha=a2)
            # gradient underline grows from the centre
            g = ease_out((s - 1.2) / 0.6)
            if g > 0:
                bw = int(bar_w * g)
                bar = Image.new("RGBA", (max(1, bw), 8))
                ramp = np.linspace(0, 1, max(1, bw))[None, :, None]
                col = (np.array(ACCENT2)[None, None, :] * (1 - ramp) + np.array(ACCENT)[None, None, :] * ramp)
                arr = np.concatenate([np.repeat(col, 8, axis=0), np.full((8, max(1, bw), 1), 255)], axis=2).astype(np.uint8)
                bar = Image.fromarray(arr, "RGBA")
                paste(img, bar, W / 2 - bw / 2, ly + agent.height + 18)
            a3 = ease_out((s - 1.6) / 0.6)
            paste(img, tag, (W - tag.width) / 2, ly + agent.height + 60 + 20 * (1 - a3), alpha=a3)
            yield img

    out = BUILD / f"seg_{i:02d}.mkv"
    encode_frames(out, frames(), n)
    return out, t


def render_end(i, seg):
    t = seg["t"]
    n = int(round(t * FPS))
    bg = blurred_background("feet_up", 0.3, t + 0.1, sigma=5, dim=0.5)
    agent, clicker = logo_layers(150)
    gap = 34
    total = agent.width + gap + clicker.width
    lx = (W - total) / 2
    ly = 300
    tag = text_layer("Automate yourself out of a job. Keep the paycheck.", font("medium", 42), (200, 211, 230, 255))
    url = "github.com/nearbycoder/AgentClicker"
    fm = font("monobold", 44)
    url_full = text_layer(url, fm, TERMINAL + (255,))
    foot = text_layer("Free download for Linux on GitHub Releases  ·  Made with Unity 6 and Blender", font("medium", 28), DIM + (255,))

    def frames():
        for k in range(n):
            s = k / FPS
            img = Image.fromarray(bg[min(k, len(bg) - 1)]).convert("RGBA")
            a = ease_out(s / 0.7)
            paste(img, agent, lx, ly, alpha=a, scale=1.08 - 0.08 * a)
            paste(img, clicker, lx + agent.width + gap, ly, alpha=a, scale=1.08 - 0.08 * a)
            a2 = ease_out((s - 0.5) / 0.6)
            paste(img, tag, (W - tag.width) / 2, ly + agent.height + 34 + 16 * (1 - a2), alpha=a2)
            # the URL types itself into a terminal-style field
            chars = int(min(len(url), max(0, (s - 1.1) / 0.03)))
            if s > 0.9:
                box_w, box_h = url_full.width + 120, url_full.height + 44
                bx, by = (W - box_w) / 2, ly + agent.height + 160
                box = Image.new("RGBA", (box_w, box_h), (0, 0, 0, 0))
                ImageDraw.Draw(box).rounded_rectangle([0, 0, box_w - 1, box_h - 1], 16, fill=(10, 14, 20, 225), outline=ACCENT + (255,), width=3)
                ab = ease_out((s - 0.9) / 0.4)
                paste(img, box, bx, by, alpha=ab)
                cursor = "█" if (int(s * 3) % 2 == 0 or chars < len(url)) else " "
                paste(img, text_layer(url[:chars] + cursor, fm, TERMINAL + (255,)), bx + 60, by + 20, alpha=ab)
            a4 = ease_out((s - 2.6) / 0.6)
            paste(img, foot, (W - foot.width) / 2, H - 150, alpha=a4)
            fade = 1 - ease_in_out((s - (t - 0.8)) / 0.8)
            if fade < 1:
                arr = np.asarray(img.convert("RGB")).astype(np.float32) * fade
                img = Image.fromarray(arr.astype(np.uint8)).convert("RGBA")
            yield img

    out = BUILD / f"seg_{i:02d}.mkv"
    encode_frames(out, frames(), n)
    return out, t


def render_montage(i, seg):
    """Hard cuts on the beat with a word every two cuts, then a short hold on Sam."""
    parts = []
    for k, (clip, ss) in enumerate(MONTAGE):
        p = BUILD / f"montage_{k:02d}.mkv"
        dur = BEAT if k < len(MONTAGE) - 1 else BEAT * 2
        ffmpeg("-ss", f"{ss:.3f}", "-t", f"{dur:.4f}", "-i", CLIPS / f"{clip}.mp4", "-vf", "format=yuv420p",
               "-r", FPS, "-c:v", "libx264", "-preset", "medium", "-crf", "14",
               "-c:a", "pcm_s16le", "-ar", "48000", "-ac", "2", p)
        parts.append(p)
    listing = BUILD / "montage.txt"
    listing.write_text("".join(f"file '{p.name}'\n" for p in parts))
    joined = BUILD / "montage_joined.mkv"
    ffmpeg("-f", "concat", "-safe", "0", "-i", listing, "-c", "copy", joined)
    t = duration(joined)
    # words: big, centred, punch in on the beat
    words = []
    fw = font("bold", 150)
    for k, word in enumerate(MONTAGE_WORDS):
        layer = text_layer(word, fw, (255, 255, 255, 255))
        png = BUILD / f"word_{k}.png"
        pad = 60
        img = Image.new("RGBA", (layer.width + pad * 2, layer.height + pad * 2), (0, 0, 0, 0))
        shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
        shadow.alpha_composite(text_layer(word, fw, (0, 0, 0, 200)), (pad, pad + 6))
        img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14)))
        img.alpha_composite(layer, (pad, pad))
        if word == "AUTOMATE.":
            img.alpha_composite(text_layer(word, fw, ACCENT + (255,)), (pad, pad))
        img.save(png)
        words.append((png, img.size))
    inputs, chain = [], "[0:v]format=yuv420p[v0]"
    last = "v0"
    for k, (png, size) in enumerate(words):
        start, end = k * 2 * BEAT, (k * 2 + 2) * BEAT - 0.04
        inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{t:.3f}", "-i", png]
        x, y = (W - size[0]) // 2, (H - size[1]) // 2
        chain += (f";[{k + 1}:v]format=rgba,fade=t=in:st={start:.3f}:d=0.08:alpha=1[w{k}]"
                  f";[{last}][w{k}]overlay=x={x}:y={y}:enable='between(t,{start:.3f},{end:.3f})'[v{k + 1}]")
        last = f"v{k + 1}"
    out = BUILD / f"seg_{i:02d}.mkv"
    ffmpeg("-i", joined, *inputs, "-filter_complex", chain, "-map", f"[{last}]", "-map", "0:a",
           "-r", FPS, "-c:v", "libx264", "-preset", "medium", "-crf", "14", "-c:a", "copy", out)
    return out, t


def cached(i, seg, extra, render):
    """Re-renders a segment only when its definition, caption or source clip changed."""
    clip = CLIPS / f"{seg.get('clip', '')}.mp4"
    key = json.dumps([seg, str(extra), clip.stat().st_mtime if clip.is_file() else 0,
                      Path(__file__).stat().st_mtime if seg.get("card") else 0], sort_keys=True, default=str)
    stamp = BUILD / f"seg_{i:02d}.key"
    out = BUILD / f"seg_{i:02d}.mkv"
    if out.exists() and stamp.exists() and stamp.read_text() == key:
        return out, json.loads((BUILD / f"seg_{i:02d}.t").read_text())
    path, t = render()
    stamp.write_text(key)
    (BUILD / f"seg_{i:02d}.t").write_text(json.dumps(t))
    return path, t


def build_segments():
    BUILD.mkdir(parents=True, exist_ok=True)
    caps = {}
    timeline = []
    for i, seg in enumerate(SEGMENTS):
        print(f"[segment {i:02d}] {seg['id']}", flush=True)
        if seg.get("card") == "title":
            path, t = cached(i, seg, None, lambda: render_title(i, seg))
        elif seg.get("card") == "end":
            path, t = cached(i, seg, MONTAGE_WORDS, lambda: render_end(i, seg))
        elif seg.get("card") == "montage":
            path, t = cached(i, seg, (MONTAGE, MONTAGE_WORDS), lambda: render_montage(i, seg))
        else:
            cap_info = None
            if "cap" in seg:
                png = BUILD / f"cap_{seg['id']}.png"
                size, shadow = caption_png(*seg["cap"], png)
                caps[seg["id"]] = (png, size, shadow)
                # a caption spanning two shots stays up across the cut: in on the first, out on the second
                spans = any(s.get("cap_from") == seg["id"] for s in SEGMENTS)
                cap_info = (png, size, shadow, 0.25, 999 if spans else seg["t"] - 0.55)
            elif "cap_from" in seg:
                png, size, shadow = caps[seg["cap_from"]]
                cap_info = (png, size, shadow, -1.0, seg["t"] - 0.55)
                seg = dict(seg, pos=next(s["pos"] for s in SEGMENTS if s["id"] == seg["cap_from"]))
            path, t = cached(i, seg, cap_info, lambda: render_clip_segment(i, seg, cap_info))
        timeline.append(dict(id=seg["id"], path=str(path), t=t, out=seg.get("out")))
    (BUILD / "timeline.json").write_text(json.dumps(timeline, indent=1))


# ============================================================================ assembly
def assemble():
    timeline = json.loads((BUILD / "timeline.json").read_text())
    starts, pos = [], 0.0
    for k, seg in enumerate(timeline):
        starts.append(pos)
        xd = 0.0
        if seg["out"] and k < len(timeline) - 1:
            xd = max(seg["out"][1], 1 / FPS)
        pos += seg["t"] - xd
    total = starts[-1] + timeline[-1]["t"]
    print(f"[trailer] {len(timeline)} segments, {total:.1f}s", flush=True)

    # ---- video: chained xfades
    inputs, chain, last = [], [], "0:v"
    for k, seg in enumerate(timeline):
        inputs += ["-i", seg["path"]]
    for k in range(len(timeline)):
        chain.append(f"[{k}:v]fps={FPS},settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p[n{k}]")
    last = "n0"
    for k in range(len(timeline) - 1):
        kind, xd = timeline[k]["out"]
        trans = {"fade": "fade", "slideleft": "slideleft", "fadeblack": "fadeblack", "cut": "fade"}[kind]
        xd = max(xd, 1 / FPS)
        label = f"x{k}"
        chain.append(f"[{last}][n{k + 1}]xfade=transition={trans}:duration={xd:.4f}:offset={starts[k + 1]:.4f}[{label}]")
        last = label
    chain.append(f"[{last}]format=yuv420p,fps={FPS}[vout]")

    # ---- game audio: every segment's SFX placed at its start, short fades at the edges
    sfx_parts = []
    for k, seg in enumerate(timeline):
        d_ms = int(starts[k] * 1000)
        sfx_parts.append(f"[{k}:a]aresample=48000,afade=t=in:d=0.05,afade=t=out:st={max(0, seg['t'] - 0.12):.3f}:d=0.12,"
                         f"adelay={d_ms}|{d_ms}[s{k}]")
    sfx_mix = "".join(f"[s{k}]" for k in range(len(timeline)))
    chain += sfx_parts
    chain.append(f"{sfx_mix}amix=inputs={len(timeline)}:normalize=0:dropout_transition=0,"
                 f"atrim=0:{total:.3f},volume=1.6,alimiter=limit=0.9[sfx]")

    # ---- stingers from the game: whoosh into the title, chapter chime on the logo, fanfare on the end card
    n_in = len(timeline)
    stingers = []
    t_title = starts[1]
    t_end = starts[-1]
    t_montage = starts[-2]
    for name, at, vol in [("whoosh", t_title - 0.35, 1.2), ("chapter", t_title + 0.6, 0.9),
                          ("promotion", t_end + 0.2, 0.8), ("whoosh", t_montage - 0.3, 0.8)]:
        path = SRC / f"sfx_{name}.wav"
        if path.exists():
            inputs += ["-i", path]
            ms = int(max(0, at) * 1000)
            stingers.append(f"[{n_in}:a]aresample=48000,aformat=channel_layouts=stereo,volume={vol},adelay={ms}|{ms}[st{n_in}]")
            n_in += 1
    chain += stingers

    # ---- music bed: the game's loop, a few seeds back to back, with a duck envelope and sidechain
    music = music_bed(total)
    inputs += ["-i", music]
    m_idx = n_in
    # quiet under the busy middle, up for title, montage and end card; fade in from the cold open
    env = (f"volume='if(lt(t,{starts[1]:.2f}),0.55+0.25*t/{starts[1]:.2f},"
           f"if(lt(t,{starts[2] + 0.5:.2f}),1.0,"
           f"if(lt(t,{t_montage:.2f}),0.8,1.0)))':eval=frame")
    chain.append(f"[{m_idx}:a]aresample=48000,aformat=channel_layouts=stereo,{env},"
                 f"afade=t=in:d=1.2,afade=t=out:st={total - 2.2:.2f}:d=2.2,atrim=0:{total:.3f}[mus]")
    chain.append("[sfx]asplit=2[sfxa][sfxkey]")
    chain.append("[mus][sfxkey]sidechaincompress=threshold=0.06:ratio=4:attack=15:release=320:makeup=1[musduck]")
    st_labels = "".join(f"[st{k}]" for k in range(len(timeline), m_idx))
    n_mix = 2 + (m_idx - len(timeline))
    chain.append(f"[sfxa][musduck]{st_labels}amix=inputs={n_mix}:normalize=0:dropout_transition=0,"
                 f"loudnorm=I=-15:TP=-1.5:LRA=11,aresample=48000[aout]")

    OUT.mkdir(parents=True, exist_ok=True)
    graph = BUILD / "trailer.filtergraph"
    graph.write_text(";\n".join(chain))
    out = OUT / "trailer.mp4"
    ffmpeg(*inputs, "-/filter_complex", graph, "-map", "[vout]", "-map", "[aout]",
           "-c:v", "libx264", "-preset", "slow", "-crf", "21", "-maxrate", "4M", "-bufsize", "8M",
           "-profile:v", "high", "-pix_fmt", "yuv420p", "-r", FPS,
           "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-movflags", "+faststart", out)
    (BUILD / "beats.json").write_text(json.dumps([dict(id=s["id"], start=round(st, 2), t=s["t"]) for s, st in zip(timeline, starts)], indent=1))
    print(f"[trailer] wrote {out} ({out.stat().st_size / 1e6:.1f} MB, {duration(out):.1f}s)")


def music_bed(total):
    seeds = sorted(SRC.glob("music_*.wav"))
    if not seeds:
        raise SystemExit("no music_*.wav in Recordings/trailer (run Tools/make_trailer.sh)")
    loop = duration(seeds[0])
    reps = math.ceil(total / loop) + 1
    order = [seeds[k % len(seeds)] for k in range(reps)]
    listing = BUILD / "music.txt"
    listing.write_text("".join(f"file '{p}'\n" for p in order))
    bed = BUILD / "music_bed.wav"
    ffmpeg("-f", "concat", "-safe", "0", "-i", listing, "-ar", "48000", "-ac", "2", bed)
    return bed


# ============================================================================ poster, teaser, stills
def poster():
    """Click-to-play poster for the README: a frame from the trailer with a play button."""
    beats = {b["id"]: b for b in json.loads((BUILD / "beats.json").read_text())}
    at = beats["title"]["start"] + 3.2
    frame = BUILD / "poster_frame.png"
    ffmpeg("-ss", f"{at:.2f}", "-i", OUT / "trailer.mp4", "-frames:v", "1", frame)
    img = Image.open(frame).convert("RGBA")
    r = 92
    cx, cy = W // 2, H // 2 + 300
    ov = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    d.ellipse([cx - r - 10, cy - r - 10 + 8, cx + r + 10, cy + r + 10 + 8], fill=(0, 0, 0, 110))
    ov = ov.filter(ImageFilter.GaussianBlur(10))
    d = ImageDraw.Draw(ov)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=ACCENT + (240,))
    d.polygon([(cx - 26, cy - 42), (cx - 26, cy + 42), (cx + 46, cy)], fill=BG + (255,))
    lbl = text_layer("WATCH THE TRAILER", font("bold", 34), (255, 255, 255, 255), spacing=4)
    ov.alpha_composite(lbl, (cx - lbl.width // 2, cy + r + 24))
    img = Image.alpha_composite(img, ov).convert("RGB")
    img.save(OUT / "trailer-poster.jpg", quality=88, optimize=True, progressive=True)
    print(f"[poster] {OUT / 'trailer-poster.jpg'} ({(OUT / 'trailer-poster.jpg').stat().st_size / 1e3:.0f} KB)")


TEASER = [("ship", 1.4, 1.8), ("gadget", 1.0, 2.2), ("promotion", 1.0, 2.0), ("factory", 9.0, 2.6)]


def teaser():
    """A ~8 s loop for the top of the README (animated WebP)."""
    parts = []
    for k, (clip, ss, t) in enumerate(TEASER):
        p = BUILD / f"teaser_{k}.mp4"
        ffmpeg("-ss", f"{ss}", "-t", f"{t}", "-i", CLIPS / f"{clip}.mp4", "-an",
               "-vf", "scale=800:450:flags=lanczos,fps=15", "-c:v", "libx264", "-crf", "12", p)
        parts.append(p)
    # crossfade the parts into a loop: the last shot fades back into the first
    inputs = sum((["-i", p] for p in parts + parts[:1]), [])
    chain, last, pos = [], "0:v", 0.0
    xd = 0.3
    for k in range(1, len(parts) + 1):
        pos += TEASER[k - 1][2] - xd
        chain.append(f"[{last}][{k}:v]xfade=transition=fade:duration={xd}:offset={pos:.3f}[t{k}]")
        last = f"t{k}"
    loop_len = sum(t for _, _, t in TEASER) - xd * len(TEASER)
    chain.append(f"[{last}]trim=start={xd:.3f}:duration={loop_len:.3f},setpts=PTS-STARTPTS[v]")
    out = OUT / "teaser.webp"
    ffmpeg(*inputs, "-filter_complex", ";".join(chain), "-map", "[v]", "-c:v", "libwebp_anim",
           "-lossless", "0", "-q:v", "72", "-compression_level", "6", "-loop", "0", out)
    print(f"[teaser] {out} ({out.stat().st_size / 1e6:.2f} MB, {loop_len:.1f}s)")


STILLS = [
    ("title", "01-title"), ("ship_code", "02-ship-code"), ("gadget_showcase", "03-gadget-showcase"),
    ("phone_call", "04-phone-call"), ("corpos_late", "05-corpos-late-game"), ("office_late", "06-office-late-game"),
    ("review", "07-performance-review"), ("automated", "08-software-factory"), ("frontier_agents", "09-frontier-agents"),
    ("board_room", "10-board-room"),
]


def stills():
    dest = OUT / "screenshots"
    if dest.exists():
        shutil.rmtree(dest)
    dest.mkdir(parents=True)
    for src, name in STILLS:
        p = SRC / "stills" / f"{src}.png"
        if not p.exists():
            print(f"  ! missing still {p}")
            continue
        img = Image.open(p).convert("RGB")
        if img.size != (W, H):
            img = img.resize((W, H), Image.LANCZOS)
        out = dest / f"{name}.jpg"
        img.save(out, quality=90, optimize=True, progressive=True, subsampling=0)
        print(f"[still] {out.name} {out.stat().st_size / 1e3:.0f} KB")


def main():
    steps = sys.argv[1:] or ["segments", "trailer", "poster", "teaser", "stills"]
    for step in steps:
        {"segments": build_segments, "trailer": assemble, "poster": poster, "teaser": teaser, "stills": stills}[step]()


if __name__ == "__main__":
    main()
