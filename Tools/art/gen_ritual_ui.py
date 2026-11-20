"""Generates the Ritual Host codex window's UI textures.

Procedural generation via Pillow + numpy with fixed seeds and supersampling, so every asset
is deterministic, crisp, anti-aliased, and sized as a power-of-two / multiple-of-four PNG:

    python Tools/art/gen_ritual_ui.py

Output: Textures/UI/Ritual/*.png. No text is baked into any image; all labels render via
RimWorld GameFont for crisp English & Chinese localization.
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Textures", "UI", "Ritual")

S = 6  # supersampling factor for crisp anti-aliased vector shapes

# Official palette grounded in 取色板.md & Spellbook grimoire craft
MOSS_DARK = (27, 67, 50)        # #1B4332 古树苍苔绿
MOSS_DEEP = (17, 43, 32)
VELLUM_BASE = (234, 228, 215)   # #EAE4D7 桦木原浆白
VELLUM_SHADE = (206, 191, 162)
INK = (43, 33, 20)              # #2B2114 iron-gall sepia
INK_MID = (74, 54, 32)
GOLD = (229, 169, 59)           # #E5A93B 超导赤金
GOLD_HI = (252, 226, 156)       # #FCE29C 日耀流金
GOLD_LO = (132, 92, 34)
LEAF = (46, 139, 87)            # #2E8B57 催化生态绿
LEAF_BRIGHT = (118, 185, 71)    # #76B947 光合嫩芽绿
LEAF_DARK = (32, 82, 54)
MANA_BLUE = (0, 180, 216)       # #00B4D8 全息菌丝蓝
BERRY_RED = (158, 43, 28)       # #9E2B1C rubrication vermilion
WHITE = (255, 255, 255, 255)


def canvas(w, h):
    return Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, img.size, img.mode)


def smooth_noise(rng, size, cells):
    grid = rng.random((cells, cells)).astype(np.float32)
    img = Image.fromarray((grid * 255).astype(np.uint8), "L").resize((size, size), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32) / 255.0


def fbm(rng, size, octaves):
    total = np.zeros((size, size), np.float32)
    amp, norm = 1.0, 0.0
    for cells in octaves:
        total += smooth_noise(rng, size, cells) * amp
        norm += amp
        amp *= 0.55
    return total / norm


def leaf(draw, cx, cy, length, width, angle, fill, vein=None):
    pts = []
    steps = 28
    for i in range(steps + 1):
        t = i / steps
        w = math.sin(math.pi * t) * width * (1.0 - 0.35 * t)
        pts.append((t * length, w))
    for i in range(steps, -1, -1):
        t = i / steps
        w = math.sin(math.pi * t) * width * (1.0 - 0.35 * t)
        pts.append((t * length, -w))
    ca, sa = math.cos(angle), math.sin(angle)
    poly = [(cx + px * ca - py * sa, cy + px * sa + py * ca) for px, py in pts]
    draw.polygon(poly, fill=fill)
    if vein:
        draw.line([(cx, cy), (cx + length * 0.84 * ca, cy + length * 0.84 * sa)],
                  fill=vein, width=max(1, int(S * 0.9)))


def spiral(draw, cx, cy, r0, turns, start, color, width, direction=1):
    pts = []
    steps = int(140 * turns)
    for i in range(steps + 1):
        t = i / steps
        ang = start + direction * t * turns * 2.0 * math.pi
        r = r0 * (1.0 - 0.82 * t)
        pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r))
    draw.line(pts, fill=color, width=width, joint="curve")


def atlas_from_corner(size, corner, edge_drawer, clear=0):
    big = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    edge_drawer(d, size)
    if clear:
        k, far = clear * S, size * S - 1
        for x0, y0 in ((0, 0), (far - k, 0), (0, far - k), (far - k, far - k)):
            d.rectangle([x0, y0, x0 + k, y0 + k], fill=(0, 0, 0, 0))
    cw = corner.size[0]
    big.alpha_composite(corner, (0, 0))
    big.alpha_composite(corner.transpose(Image.FLIP_LEFT_RIGHT), (size * S - cw, 0))
    big.alpha_composite(corner.transpose(Image.FLIP_TOP_BOTTOM), (0, size * S - cw))
    big.alpha_composite(corner.transpose(Image.ROTATE_180), (size * S - cw, size * S - cw))
    return big


# ---------------------------------------------------------------- cover & vellum

def ritual_cover():
    size = 512
    rng = np.random.default_rng(4207)
    n_coarse = fbm(rng, size, [5, 11, 27, 64])
    n_grain = fbm(rng, size, [32, 64, 128])
    mix = np.clip(0.35 * n_coarse + 0.25 * n_grain, 0.0, 1.0)

    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / (size - 1)
    dist = np.sqrt((x - 0.5) ** 2 + (y - 0.5) ** 2) * 1.414
    vig = np.clip(dist ** 1.7, 0.0, 1.0)

    c_hi = np.array([34, 82, 61], np.float32)
    c_mid = np.array(MOSS_DARK, np.float32)
    c_lo = np.array(MOSS_DEEP, np.float32)

    rgb = c_hi[None, None, :] * (1.0 - mix[..., None]) + c_mid[None, None, :] * mix[..., None]
    rgb = rgb * (1.0 - vig[..., None] * 0.45) + c_lo[None, None, :] * (vig[..., None] * 0.45)
    pore = rng.normal(0, 2.4, (size, size, 1)).astype(np.float32)
    rgb = np.clip(rgb + pore, 0, 255).astype(np.uint8)
    save(Image.fromarray(rgb, "RGB"), "RitualCover")


def ritual_page():
    size = 512
    rng = np.random.default_rng(2026)
    base = np.array(VELLUM_BASE, np.float32)
    shade = np.array(VELLUM_SHADE, np.float32)

    # Keep central reading zone low-frequency for crisp CJK & English typography
    n = fbm(rng, size, [4, 9, 21, 52])
    n = (n - n.min()) / (n.max() - n.min())
    mix = 0.06 + 0.24 * (n ** 2)

    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / (size - 1)
    edge = np.minimum(np.minimum(x, 1.0 - x), np.minimum(y, 1.0 - y))
    burn = np.clip(1.0 - edge / 0.11, 0.0, 1.0) ** 2.1
    mix = np.clip(mix + burn * 0.52, 0.0, 1.0)

    rgb = base[None, None, :] * (1.0 - mix[..., None]) + shade[None, None, :] * mix[..., None]
    grain = rng.normal(0, 2.2, (size, size, 1)).astype(np.float32)
    rgb = np.clip(rgb + grain, 0, 255).astype(np.uint8)
    img = Image.fromarray(rgb, "RGB").convert("RGBA")

    fibres = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(fibres)
    prng = random.Random(93)
    for _ in range(340):
        x0, y0 = prng.uniform(0, size), prng.uniform(0, size)
        ang = prng.uniform(0, math.pi)
        ln = prng.uniform(5, 18)
        x1, y1 = x0 + math.cos(ang) * ln, y0 + math.sin(ang) * ln
        tone = prng.choice([(132, 110, 76, 20), (255, 252, 244, 24)])
        d.line([(x0, y0), (x1, y1)], fill=tone, width=1)
    img = Image.alpha_composite(img, fibres)
    save(img.convert("RGB"), "RitualPage")


def ritual_cover_frame():
    size = 128  # 9-slice corner = 32
    hi = GOLD + (255,)
    lo = GOLD_LO + (235,)

    def edges(d, sz):
        u = S
        d.rectangle([4 * u, 4 * u, sz * u - 4 * u, sz * u - 4 * u], outline=lo, width=3 * u)
        d.rectangle([5 * u, 5 * u, sz * u - 5 * u, sz * u - 5 * u], outline=hi, width=1 * u)
        d.rectangle([10 * u, 10 * u, sz * u - 10 * u, sz * u - 10 * u], outline=GOLD_LO + (180,), width=1 * u)

    corner = Image.new("RGBA", (32 * S, 32 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(corner)
    u = S
    d.polygon([(0, 0), (23 * u, 0), (0, 23 * u)], fill=GOLD_LO + (255,))
    d.polygon([(2 * u, 2 * u), (20 * u, 2 * u), (2 * u, 20 * u)], fill=GOLD + (255,))
    d.line([(3 * u, 3 * u), (17 * u, 3 * u)], fill=GOLD_HI + (255,), width=u)
    d.line([(3 * u, 3 * u), (3 * u, 17 * u)], fill=GOLD_HI + (255,), width=u)
    leaf(d, 7 * u, 7 * u, 16 * u, 4.4 * u, math.pi / 4, GOLD_HI + (255,), GOLD_LO + (255,))
    # Mycelium-blue jewel inset on brass clasp
    d.ellipse([4.5 * u, 4.5 * u, 10.5 * u, 10.5 * u], fill=GOLD_LO + (255,))
    d.ellipse([5.3 * u, 5.3 * u, 9.7 * u, 9.7 * u], fill=MANA_BLUE + (255,))
    d.ellipse([6.0 * u, 5.8 * u, 7.6 * u, 7.4 * u], fill=(190, 245, 255, 230))
    big = atlas_from_corner(size, corner, edges)
    save(finish(big, size, size), "RitualCoverFrameAtlas")


def ritual_page_frame():
    size = 256  # 9-slice corner = 64
    ink_outer = INK_MID + (225,)
    ink_inner = INK_MID + (140,)

    def edges(d, sz):
        u = S
        d.rectangle([10 * u, 10 * u, sz * u - 10 * u, sz * u - 10 * u], outline=ink_outer, width=2 * u)
        d.rectangle([15 * u, 15 * u, sz * u - 15 * u, sz * u - 15 * u], outline=ink_inner, width=1 * u)

    corner = Image.new("RGBA", (64 * S, 64 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(corner)
    u = S
    spiral(d, 20 * u, 20 * u, 11 * u, 1.35, math.pi * 0.25, ink_outer, 2 * u, 1)
    d.line([(26 * u, 10 * u), (60 * u, 10 * u)], fill=ink_outer, width=2 * u)
    d.line([(10 * u, 26 * u), (10 * u, 60 * u)], fill=ink_outer, width=2 * u)
    leaf(d, 32 * u, 10 * u, 16 * u, 4.8 * u, -0.58, LEAF + (235,), LEAF_DARK + (255,))
    leaf(d, 44 * u, 10 * u, 12.5 * u, 4.0 * u, 0.54, LEAF_BRIGHT + (220,), LEAF_DARK + (255,))
    leaf(d, 54 * u, 10 * u, 8.5 * u, 2.8 * u, -0.45, LEAF + (220,), LEAF_DARK + (255,))
    leaf(d, 10 * u, 32 * u, 16 * u, 4.8 * u, math.pi / 2 + 0.58, LEAF + (235,), LEAF_DARK + (255,))
    leaf(d, 10 * u, 44 * u, 12.5 * u, 4.0 * u, math.pi / 2 - 0.54, LEAF_BRIGHT + (220,), LEAF_DARK + (255,))
    leaf(d, 10 * u, 54 * u, 8.5 * u, 2.8 * u, math.pi / 2 + 0.45, LEAF + (220,), LEAF_DARK + (255,))
    d.ellipse([27 * u, 27 * u, 33 * u, 33 * u], fill=GOLD + (255,), outline=GOLD_LO + (255,), width=u)
    big = atlas_from_corner(size, corner, edges, clear=24)
    save(finish(big, size, size), "RitualPageFrameAtlas")


def rounded_atlas(name, radius, fill, border, border_w, gradient=None, highlight=None):
    size = 64  # 9-slice corner = 16
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    box = [1 * u, 1 * u, size * u - 1 * u - 1, size * u - 1 * u - 1]
    if gradient:
        top, bottom = gradient
        grad = Image.new("RGBA", big.size, (0, 0, 0, 0))
        gd = ImageDraw.Draw(grad)
        for yy in range(big.size[1]):
            t = yy / (big.size[1] - 1)
            v = int(top + (bottom - top) * t)
            gd.line([(0, yy), (big.size[0], yy)], fill=(v, v, v, 255))
        mask = Image.new("L", big.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle(box, radius * u, fill=255)
        big.paste(grad, (0, 0), mask)
    else:
        d.rounded_rectangle(box, radius * u, fill=fill)
    if highlight:
        d.line([(radius * u, 3 * u), (size * u - radius * u, 3 * u)], fill=highlight, width=u)
    if border:
        d.rounded_rectangle(box, radius * u, outline=border, width=int(border_w * u))
    save(finish(big, size, size), name)


def spine_shade():
    w, h = 64, 8
    x = np.linspace(0, 1, w, dtype=np.float32)
    a = (1.0 - x) ** 2.2
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., 0] = 38
    rgba[..., 1] = 27
    rgba[..., 2] = 14
    rgba[..., 3] = np.clip(a * 255, 0, 255).astype(np.uint8)[None, :]
    save(Image.fromarray(rgba, "RGBA"), "SpineShade")


def ritual_divider():
    w, h = 512, 24
    big = canvas(w, h)
    d = ImageDraw.Draw(big)
    u = S
    cx, cy = w * u / 2, h * u / 2
    ink = INK_MID + (225,)
    for sgn in (-1, 1):
        d.polygon(
            [(cx + sgn * 28 * u, cy - 1.3 * u),
             (cx + sgn * (w / 2 - 6) * u, cy),
             (cx + sgn * 28 * u, cy + 1.3 * u)],
            fill=ink,
        )
        leaf(d, cx + sgn * 13 * u, cy, 16 * u, 4.3 * u,
             0 if sgn > 0 else math.pi, LEAF + (255,), LEAF_DARK + (255,))
        d.ellipse([cx + sgn * 32 * u - 2.2 * u, cy - 2.2 * u,
                   cx + sgn * 32 * u + 2.2 * u, cy + 2.2 * u], fill=GOLD + (255,))
    # Center sigil: iron-gall diamond framing a glowing crystal-blue core
    d.polygon([(cx, cy - 7.5 * u), (cx + 7.5 * u, cy), (cx, cy + 7.5 * u), (cx - 7.5 * u, cy)],
              fill=INK_MID + (245,))
    d.polygon([(cx, cy - 4.8 * u), (cx + 4.8 * u, cy), (cx, cy + 4.8 * u), (cx - 4.8 * u, cy)],
              fill=GOLD + (255,))
    d.polygon([(cx, cy - 2.6 * u), (cx + 2.6 * u, cy), (cx, cy + 2.6 * u), (cx - 2.6 * u, cy)],
              fill=MANA_BLUE + (255,))
    save(finish(big, w, h), "RitualDivider")


# ---------------------------------------------------------------- white-mask UI icons (64x64)

def icon_family_crafting():
    """Botanical compass & anvil-rune sigil."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    # Outer caliper ring (broken at bottom for compass legs)
    d.arc([12 * u, 6 * u, 52 * u, 46 * u], start=135, end=45, fill=WHITE, width=int(4.2 * u))
    # Top finial diamond
    d.polygon([(32 * u, 2 * u), (38 * u, 8 * u), (32 * u, 14 * u), (26 * u, 8 * u)], fill=WHITE)
    # Compass legs
    d.line([(32 * u, 12 * u), (14 * u, 56 * u)], fill=WHITE, width=int(5.2 * u))
    d.line([(32 * u, 12 * u), (50 * u, 56 * u)], fill=WHITE, width=int(5.2 * u))
    # Horizontal chisel bar + carved center diamond
    d.rounded_rectangle([16 * u, 37 * u, 48 * u, 43 * u], 2 * u, fill=WHITE)
    d.polygon([(32 * u, 22 * u), (39 * u, 30 * u), (32 * u, 38 * u), (25 * u, 30 * u)], fill=WHITE)
    d.polygon([(32 * u, 26 * u), (35.5 * u, 30 * u), (32 * u, 34 * u), (28.5 * u, 30 * u)], fill=(0, 0, 0, 0))
    save(finish(big, size, size), "FamilyCrafting")


def icon_family_life():
    """Four-petal livingwood blossom with carved center seed."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    cx, cy = 32 * u, 32 * u
    # Four cardinal pointed petals radiating from center
    for k in range(4):
        ang = k * (math.pi / 2) - math.pi / 2
        sx = cx + math.cos(ang) * 7 * u
        sy = cy + math.sin(ang) * 7 * u
        leaf(d, sx, sy, 23 * u, 8.6 * u, ang, WHITE)
    # Diagonal small sepals
    for k in range(4):
        ang = k * (math.pi / 2) + math.pi / 4
        sx = cx + math.cos(ang) * 9 * u
        sy = cy + math.sin(ang) * 9 * u
        leaf(d, sx, sy, 13 * u, 3.8 * u, ang, WHITE)
    # Negative-space ring & core seed
    d.ellipse([23 * u, 23 * u, 41 * u, 41 * u], fill=(0, 0, 0, 0))
    d.ellipse([26.5 * u, 26.5 * u, 37.5 * u, 37.5 * u], fill=WHITE)
    save(finish(big, size, size), "FamilyLife")


def icon_family_domain():
    """Astrolabe ring with slender 4-point starlight compass."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    cx, cy = 32 * u, 32 * u
    # Outer astrolabe ring
    d.ellipse([10 * u, 10 * u, 54 * u, 54 * u], outline=WHITE, width=int(3.6 * u))
    # Slender 4-point star extending beyond ring with safe margin
    star = [
        (32 * u, 5 * u), (36.5 * u, 27.5 * u),
        (59 * u, 32 * u), (36.5 * u, 36.5 * u),
        (32 * u, 59 * u), (27.5 * u, 36.5 * u),
        (5 * u, 32 * u), (27.5 * u, 27.5 * u),
    ]
    d.polygon(star, fill=WHITE)
    # Cut inner center ring so it reads crisply at 16px
    d.ellipse([26.5 * u, 26.5 * u, 37.5 * u, 37.5 * u], fill=(0, 0, 0, 0))
    d.ellipse([29 * u, 29 * u, 35 * u, 35 * u], fill=WHITE)
    save(finish(big, size, size), "FamilyDomain")


def icon_mana_crystal():
    """Faceted hexagonal mana crystal with clean inner bevel cutout."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    outer = [
        (32 * u, 4 * u),
        (50 * u, 18 * u),
        (50 * u, 46 * u),
        (32 * u, 60 * u),
        (14 * u, 46 * u),
        (14 * u, 18 * u),
    ]
    d.polygon(outer, fill=WHITE)
    # Cut facet lines to show crystalline planes
    inner_cut = [
        (32 * u, 12 * u),
        (43 * u, 21 * u),
        (43 * u, 43 * u),
        (32 * u, 52 * u),
        (21 * u, 43 * u),
        (21 * u, 21 * u),
    ]
    d.polygon(inner_cut, outline=(0, 0, 0, 0), width=int(2.6 * u))
    d.line([(32 * u, 4 * u), (32 * u, 22 * u)], fill=(0, 0, 0, 0), width=int(2.4 * u))
    d.line([(32 * u, 42 * u), (32 * u, 60 * u)], fill=(0, 0, 0, 0), width=int(2.4 * u))
    # Bright inner core diamond
    d.polygon([(32 * u, 21 * u), (40 * u, 32 * u), (32 * u, 43 * u), (24 * u, 32 * u)], fill=(0, 0, 0, 0))
    d.polygon([(32 * u, 24.5 * u), (37.5 * u, 32 * u), (32 * u, 39.5 * u), (26.5 * u, 32 * u)], fill=WHITE)
    save(finish(big, size, size), "ManaCrystal")


def icon_chalk_rune_cell():
    """Square chalk rune cell frame + inscribed nested diamond sigil (tinted for R3)."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    # Outer chalk square border
    d.rounded_rectangle([3 * u, 3 * u, 61 * u, 61 * u], 5 * u, outline=WHITE, width=int(4.2 * u))
    # Subtle soft fill inside
    d.rounded_rectangle([7 * u, 7 * u, 57 * u, 57 * u], 3 * u, fill=(255, 255, 255, 58))
    # Inscribed chalk diamond glyph (echoes Buildings/ChalkMark)
    d.polygon([(32 * u, 13 * u), (51 * u, 32 * u), (32 * u, 51 * u), (13 * u, 32 * u)],
              outline=WHITE, width=int(3.6 * u))
    d.polygon([(32 * u, 24 * u), (40 * u, 32 * u), (32 * u, 40 * u), (24 * u, 32 * u)],
              fill=WHITE)
    save(finish(big, size, size), "ChalkRuneCell")


def icon_plus():
    """Slender botanical cross with small center diamond."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    d.rounded_rectangle([28.5 * u, 11 * u, 35.5 * u, 53 * u], 3 * u, fill=WHITE)
    d.rounded_rectangle([11 * u, 28.5 * u, 53 * u, 35.5 * u], 3 * u, fill=WHITE)
    d.polygon([(32 * u, 24 * u), (40 * u, 32 * u), (32 * u, 40 * u), (24 * u, 32 * u)], fill=WHITE)
    save(finish(big, size, size), "Plus")


def icon_radio():
    """Botanical ring radio buttons (on & off)."""
    size = 64
    u = S
    # Off
    big_off = canvas(size, size)
    d_off = ImageDraw.Draw(big_off)
    d_off.ellipse([8 * u, 8 * u, 56 * u, 56 * u], outline=WHITE, width=int(5.0 * u))
    save(finish(big_off, size, size), "RadioOff")

    # On
    big_on = canvas(size, size)
    d_on = ImageDraw.Draw(big_on)
    d_on.ellipse([8 * u, 8 * u, 56 * u, 56 * u], outline=WHITE, width=int(5.0 * u))
    d_on.polygon([(32 * u, 18 * u), (46 * u, 32 * u), (32 * u, 46 * u), (18 * u, 32 * u)], fill=WHITE)
    save(finish(big_on, size, size), "RadioOn")


# ---------------------------------------------------------------- natural-color gizmo icons

def gizmo_host():
    """Altar ritual circle gizmo icon: crisp birch-gold outer ring + emerald inner circle + blue core."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    cx, cy = 32 * u, 32 * u
    # Dark backing halo for contrast on RimWorld's command tile
    d.ellipse([5 * u, 5 * u, 59 * u, 59 * u], fill=MOSS_DEEP + (210,))
    # Gold outer ritual ring + cardinal ticks
    d.ellipse([7 * u, 7 * u, 57 * u, 57 * u], outline=GOLD + (255,), width=int(4.2 * u))
    for k in range(4):
        ang = k * (math.pi / 2)
        p0 = (cx + math.cos(ang) * 21 * u, cy + math.sin(ang) * 21 * u)
        p1 = (cx + math.cos(ang) * 28.5 * u, cy + math.sin(ang) * 28.5 * u)
        d.line([p0, p1], fill=GOLD_HI + (255,), width=int(3.0 * u))
    # Livingwood emerald inner circle
    d.ellipse([15 * u, 15 * u, 49 * u, 49 * u], outline=LEAF_BRIGHT + (255,), width=int(3.2 * u))
    # Altar diamond centerpiece
    d.polygon([(32 * u, 20 * u), (44 * u, 32 * u), (32 * u, 44 * u), (20 * u, 32 * u)],
              fill=GOLD + (255,), outline=GOLD_HI + (255,), width=int(1.6 * u))
    d.polygon([(32 * u, 26 * u), (38 * u, 32 * u), (32 * u, 38 * u), (26 * u, 32 * u)],
              fill=MANA_BLUE + (255,))
    save(finish(big, size, size), "Host")


def gizmo_auto():
    """Auto-repeat cyclic rune gizmo icon: luminous sprout-green arc + gold arrowhead & center."""
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    cx, cy = 32 * u, 32 * u
    d.ellipse([5 * u, 5 * u, 59 * u, 59 * u], fill=MOSS_DEEP + (185,))
    # Swept circular arc (from 42 deg clockwise to 318 deg)
    pts = []
    r = 20.5 * u
    a_start = math.radians(40)
    a_end = math.radians(318)
    for i in range(96):
        t = i / 95
        a = a_start + t * (a_end - a_start)
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    d.line(pts, fill=LEAF_BRIGHT + (255,), width=int(6.0 * u), joint="curve")
    # Crisp arrowhead at top-right (318 deg)
    tip_ang = math.radians(325)
    tx, ty = cx + math.cos(tip_ang) * r, cy + math.sin(tip_ang) * r
    d.polygon([
        (tx + 4 * u, ty + 2 * u),
        (tx - 11 * u, ty + 3 * u),
        (tx - 2 * u, ty - 11 * u),
    ], fill=GOLD_HI + (255,))
    # Center superconducting-gold & mana-blue seed
    d.ellipse([24 * u, 24 * u, 40 * u, 40 * u], fill=GOLD + (255,))
    d.ellipse([27.5 * u, 27.5 * u, 36.5 * u, 36.5 * u], fill=MANA_BLUE + (255,))
    save(finish(big, size, size), "Auto")


def main():
    ritual_cover()
    ritual_page()
    ritual_cover_frame()
    ritual_page_frame()
    # PanelAtlas: faint white wash with firm outline, tinted per state/surface
    rounded_atlas("RitualPanelAtlas", 7, (255, 255, 255, 230), (255, 255, 255, 255), 1.4)
    # ButtonAtlas: beveled plaque, light top to darker bottom, tinted per role
    rounded_atlas("RitualButtonAtlas", 7, None, (92, 92, 92, 255), 1.5,
                  gradient=(252, 188), highlight=(255, 255, 255, 165))
    spine_shade()
    ritual_divider()
    icon_family_crafting()
    icon_family_life()
    icon_family_domain()
    icon_mana_crystal()
    icon_chalk_rune_cell()
    icon_plus()
    icon_radio()
    gizmo_host()
    gizmo_auto()


if __name__ == "__main__":
    main()
