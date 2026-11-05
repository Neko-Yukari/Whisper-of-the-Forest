"""Generates the spellbook window's decoration textures.

Everything is drawn procedurally (PIL + numpy, fixed seed), so the set is original and can be
rebuilt byte-for-byte:

    python Tools/art/gen_spellbook_ui.py

Output: Textures/Experiments/Spellbook/UI/*.png. Every size is a power of two (RimWorld may
DXT-compress UI textures, which needs multiples of four).

Two kinds of texture:
- natural colour (parchment, frames, socket, inlay, divider, vine): drawn as-is;
- white masks (button, panel, ribbon, glow, icons): tinted in code with GUI.color, so one
  texture serves every state and kind colour.

Atlases (*Atlas) follow Verse.Widgets.DrawAtlas: a square texture whose corner is a quarter of
its width, drawn at that size in screen pixels.
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Textures", "Experiments", "Spellbook", "UI")

S = 4  # supersampling factor for anti-aliased shapes

INK = (74, 54, 32)
BRASS = (196, 160, 82)
BRASS_HI = (240, 214, 140)
BRASS_LO = (112, 84, 38)
LEAF = (88, 128, 72)
LEAF_DARK = (52, 84, 46)


def canvas(w, h):
    return Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, img.size)


def smooth_noise(rng, size, cells):
    """Value noise: a random grid upsampled bicubically, in 0..1."""
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


# ---------------------------------------------------------------- parchment

def parchment():
    size = 512
    rng = np.random.default_rng(1207)
    base = np.array([233, 221, 190], np.float32)
    shade = np.array([196, 172, 128], np.float32)

    n = fbm(rng, size, [4, 9, 23, 61])
    n = (n - n.min()) / (n.max() - n.min())
    mix = 0.10 + 0.35 * n ** 2

    # Edge burn, kept soft so the page stays readable when stretched.
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / (size - 1)
    edge = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    burn = np.clip(1.0 - edge / 0.10, 0, 1) ** 2.2
    mix = np.clip(mix + burn * 0.55, 0, 1)

    rgb = base[None, None, :] * (1 - mix[..., None]) + shade[None, None, :] * mix[..., None]
    grain = rng.normal(0, 3.0, (size, size, 1)).astype(np.float32)
    rgb = np.clip(rgb + grain, 0, 255).astype(np.uint8)
    img = Image.fromarray(rgb, "RGB").convert("RGBA")

    # Fibres: short faint strokes.
    fibres = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(fibres)
    prng = random.Random(77)
    for _ in range(420):
        x0, y0 = prng.uniform(0, size), prng.uniform(0, size)
        ang = prng.uniform(0, math.pi)
        ln = prng.uniform(6, 22)
        x1, y1 = x0 + math.cos(ang) * ln, y0 + math.sin(ang) * ln
        tone = prng.choice([(120, 96, 60, 26), (255, 250, 235, 30)])
        d.line([(x0, y0), (x1, y1)], fill=tone, width=1)
    img = Image.alpha_composite(img, fibres)
    save(img.convert("RGB"), "Parchment")


# ---------------------------------------------------------------- vine ornaments

def leaf(draw, cx, cy, length, width, angle, fill, vein=None):
    """Pointed leaf from (cx, cy) along angle; coordinates already supersampled."""
    pts = []
    steps = 24
    for i in range(steps + 1):
        t = i / steps
        w = math.sin(math.pi * t) * width * (1 - 0.35 * t)
        pts.append((t * length, w))
    for i in range(steps, -1, -1):
        t = i / steps
        w = math.sin(math.pi * t) * width * (1 - 0.35 * t)
        pts.append((t * length, -w))
    ca, sa = math.cos(angle), math.sin(angle)
    poly = [(cx + px * ca - py * sa, cy + px * sa + py * ca) for px, py in pts]
    draw.polygon(poly, fill=fill)
    if vein:
        draw.line([(cx, cy), (cx + length * 0.85 * ca, cy + length * 0.85 * sa)], fill=vein, width=max(1, S))


def spiral(draw, cx, cy, r0, turns, start, color, width, direction=1):
    pts = []
    steps = int(120 * turns)
    for i in range(steps + 1):
        t = i / steps
        ang = start + direction * t * turns * 2 * math.pi
        r = r0 * (1 - 0.82 * t)
        pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r))
    draw.line(pts, fill=color, width=width, joint="curve")


def corner_vine(size, color, leaf_fill, leaf_vein):
    """Top-left corner ornament in a size x size supersampled tile."""
    tile = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(tile)
    u = S
    # Curl tucked in the corner.
    spiral(d, 20 * u, 20 * u, 11 * u, 1.35, math.pi * 0.25, color, 2 * u, 1)
    # Two stems running along the edges, each with leaves.
    d.line([(26 * u, 10 * u), (size * u * 0.92, 10 * u)], fill=color, width=2 * u)
    d.line([(10 * u, 26 * u), (10 * u, size * u * 0.92)], fill=color, width=2 * u)
    leaf(d, 32 * u, 10 * u, 16 * u, 5.0 * u, -0.60, leaf_fill, leaf_vein)
    leaf(d, 44 * u, 10 * u, 13 * u, 4.2 * u, 0.55, leaf_fill, leaf_vein)
    leaf(d, 54 * u, 10 * u, 9 * u, 3.0 * u, -0.45, leaf_fill, leaf_vein)
    leaf(d, 10 * u, 32 * u, 16 * u, 5.0 * u, math.pi / 2 + 0.60, leaf_fill, leaf_vein)
    leaf(d, 10 * u, 44 * u, 13 * u, 4.2 * u, math.pi / 2 - 0.55, leaf_fill, leaf_vein)
    leaf(d, 10 * u, 54 * u, 9 * u, 3.0 * u, math.pi / 2 + 0.45, leaf_fill, leaf_vein)
    # A small berry where the stems meet.
    d.ellipse([27 * u, 27 * u, 33 * u, 33 * u], fill=(148, 60, 46, 255))
    return tile


def atlas_from_corner(size, corner, edge_drawer, clear=0):
    """Mirrors a top-left corner tile into the four corners of a size x size atlas.

    clear: square (in texture pixels) at each corner where the edge rules are erased first, so
    the ornament is not crossed by them.
    """
    big = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    edge_drawer(d, size)
    if clear:
        k, far = clear * S, size * S - 1
        for x0, y0 in ((0, 0), (far - k, 0), (0, far - k), (far - k, far - k)):
            d.rectangle([x0, y0, x0 + k, y0 + k], fill=(0, 0, 0, 0))
    c = corner
    cw = c.size[0]
    big.alpha_composite(c, (0, 0))
    big.alpha_composite(c.transpose(Image.FLIP_LEFT_RIGHT), (size * S - cw, 0))
    big.alpha_composite(c.transpose(Image.FLIP_TOP_BOTTOM), (0, size * S - cw))
    big.alpha_composite(c.transpose(Image.ROTATE_180), (size * S - cw, size * S - cw))
    return big


def page_frame():
    size = 256  # corner 64
    ink = INK + (235,)
    thin = INK + (150,)

    def edges(d, sz):
        u = S
        # Double rule along every edge; the corner tiles cover the joins.
        d.rectangle([10 * u, 10 * u, sz * u - 10 * u, sz * u - 10 * u], outline=ink, width=2 * u)
        d.rectangle([15 * u, 15 * u, sz * u - 15 * u, sz * u - 15 * u], outline=thin, width=1 * u)

    corner = corner_vine(64, ink, LEAF + (240,), LEAF_DARK + (255,))
    big = atlas_from_corner(size, corner, edges, clear=24)
    save(finish(big, size, size), "PageFrameAtlas")


def cover_frame():
    size = 128  # corner 32
    hi = BRASS + (255,)
    lo = BRASS_LO + (220,)

    def edges(d, sz):
        u = S
        d.rectangle([4 * u, 4 * u, sz * u - 4 * u, sz * u - 4 * u], outline=lo, width=3 * u)
        d.rectangle([5 * u, 5 * u, sz * u - 5 * u, sz * u - 5 * u], outline=hi, width=1 * u)
        d.rectangle([10 * u, 10 * u, sz * u - 10 * u, sz * u - 10 * u], outline=BRASS_LO + (170,), width=1 * u)

    corner = Image.new("RGBA", (32 * S, 32 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(corner)
    u = S
    # Brass corner piece: a quarter plate with a leaf knot.
    d.polygon([(0, 0), (22 * u, 0), (0, 22 * u)], fill=BRASS_LO + (255,))
    d.polygon([(2 * u, 2 * u), (19 * u, 2 * u), (2 * u, 19 * u)], fill=BRASS + (255,))
    d.line([(4 * u, 4 * u), (16 * u, 4 * u)], fill=BRASS_HI + (255,), width=u)
    leaf(d, 7 * u, 7 * u, 16 * u, 4.5 * u, math.pi / 4, BRASS_HI + (255,), BRASS_LO + (255,))
    d.ellipse([5 * u, 5 * u, 10 * u, 10 * u], fill=(70, 150, 128, 255))
    big = atlas_from_corner(size, corner, edges)
    save(finish(big, size, size), "CoverFrameAtlas")


# ---------------------------------------------------------------- tintable atlases

def rounded_atlas(name, radius, fill, border, border_w, gradient=None, highlight=None):
    size = 64  # corner 16
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


def inlay():
    size = 64
    big = canvas(size, size)
    u = S
    box = [1 * u, 1 * u, size * u - 1 * u - 1, size * u - 1 * u - 1]
    d = ImageDraw.Draw(big)
    d.rounded_rectangle(box, 6 * u, fill=(30, 40, 33, 240))
    # Inner shadow along the top edge.
    shadow = Image.new("RGBA", big.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([3 * u, 2 * u, size * u - 3 * u, 9 * u], 4 * u, fill=(0, 0, 0, 110))
    shadow = shadow.filter(ImageFilter.GaussianBlur(3 * u))
    mask = Image.new("L", big.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, 6 * u, fill=255)
    big = Image.composite(Image.alpha_composite(big, shadow), big, mask)
    d = ImageDraw.Draw(big)
    d.rounded_rectangle(box, 6 * u, outline=(18, 22, 18, 255), width=int(1.5 * u))
    d.line([(7 * u, size * u - 3 * u), (size * u - 7 * u, size * u - 3 * u)], fill=(232, 214, 170, 70), width=u)
    save(finish(big, size, size), "InlayAtlas")


# ---------------------------------------------------------------- socket, glow, shade

def socket():
    size = 128
    big = canvas(size, size)
    u = S
    c = size * u / 2
    yy, xx = np.mgrid[0:size * u, 0:size * u].astype(np.float32)
    dx, dy = xx - c, yy - c
    r = np.sqrt(dx * dx + dy * dy) / u

    # Recess: dark disc so an empty socket reads as a hole in the page.
    rec = np.clip((48.5 - r) * 1.5, 0, 1)
    # Ring with a metallic light from the upper left.
    ring = np.clip((r - 46) * 1.5, 0, 1) * np.clip((55 - r) * 1.5, 0, 1)
    light = np.clip(0.5 - (dx + dy) / (2 * 55 * u) * 0.9, 0, 1)
    ridge = 1 - np.abs((r - 50.5) / 4.5)
    col = np.array(BRASS_LO, np.float32)[None, None] * (1 - light[..., None]) + \
        np.array(BRASS_HI, np.float32)[None, None] * light[..., None]
    col = col * (0.75 + 0.25 * np.clip(ridge, 0, 1)[..., None])

    rgba = np.zeros((size * u, size * u, 4), np.float32)
    rgba[..., :3] = np.array([26, 34, 29], np.float32)
    rgba[..., 3] = rec * 215
    rgba[..., :3] = rgba[..., :3] * (1 - ring[..., None]) + col * ring[..., None]
    rgba[..., 3] = np.maximum(rgba[..., 3], ring * 255)
    big = Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8), "RGBA")

    # Four leaves at the diagonals, outside the ring.
    d = ImageDraw.Draw(big)
    for k in range(4):
        ang = math.pi / 4 + k * math.pi / 2
        sx, sy = c + math.cos(ang) * 52 * u, c + math.sin(ang) * 52 * u
        leaf(d, sx, sy, 11 * u, 3.6 * u, ang, LEAF + (255,), LEAF_DARK + (255,))
    save(finish(big, size, size), "Socket")


def glow():
    size = 128
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2
    r = np.sqrt((xx - c) ** 2 + (yy - c) ** 2) / c
    a = np.exp(-(r ** 2) * 3.2) * np.clip(1 - r, 0, 1) ** 0.5
    rgba = np.zeros((size, size, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.clip(a * 255, 0, 255).astype(np.uint8)
    save(Image.fromarray(rgba, "RGBA"), "Glow")


def spine_shade():
    w, h = 64, 8
    x = np.linspace(0, 1, w, dtype=np.float32)
    a = (1 - x) ** 2.2
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., 0] = 40
    rgba[..., 1] = 28
    rgba[..., 2] = 14
    rgba[..., 3] = np.clip(a * 255, 0, 255).astype(np.uint8)[None, :]
    save(Image.fromarray(rgba, "RGBA"), "SpineShade")


# ---------------------------------------------------------------- ribbon

def ribbon():
    w, h = 128, 64
    big = canvas(w, h)
    u = S
    notch = 22
    poly = [(0, 4 * u), (w * u, 4 * u), ((w - notch) * u, h * u / 2), (w * u, (h - 4) * u), (0, (h - 4) * u)]
    mask = Image.new("L", big.size, 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)

    rng = np.random.default_rng(31)
    yy = np.arange(h * u, dtype=np.float32)[:, None] / (h * u)
    # Soft fold shading across the ribbon plus a woven stripe.
    shade = 0.80 + 0.20 * np.sin(yy * math.pi) + 0.03 * np.sin(yy * h * math.pi * 1.0)
    weave = 0.03 * np.sin(np.arange(w * u, dtype=np.float32)[None, :] / u * math.pi * 0.9)
    noise = rng.normal(0, 0.015, (h * u, w * u)).astype(np.float32)
    v = np.clip(shade + weave + noise, 0, 1)
    rgba = np.zeros((h * u, w * u, 4), np.uint8)
    rgba[..., :3] = (v * 255).astype(np.uint8)[..., None]
    rgba[..., 3] = 255
    fabric = Image.fromarray(rgba, "RGBA")
    big.paste(fabric, (0, 0), mask)

    d = ImageDraw.Draw(big)
    # Stitched hems.
    for yline in (9 * u, (h - 9) * u):
        x = 2 * u
        while x < (w - notch - 6) * u:
            d.line([(x, yline), (x + 5 * u, yline)], fill=(150, 150, 150, 255), width=u)
            x += 9 * u
    d.polygon(poly, outline=(110, 110, 110, 255), width=u)
    save(finish(big, w, h), "Ribbon")


# ---------------------------------------------------------------- divider and vine track

def divider():
    w, h = 512, 24
    big = canvas(w, h)
    d = ImageDraw.Draw(big)
    u = S
    cx, cy = w * u / 2, h * u / 2
    # Tapering rule: a thin polygon from the centre to each end.
    for sgn in (-1, 1):
        d.polygon([(cx + sgn * 26 * u, cy - 1.4 * u), (cx + sgn * (w / 2 - 6) * u, cy),
                   (cx + sgn * 26 * u, cy + 1.4 * u)], fill=INK + (220,))
        leaf(d, cx + sgn * 12 * u, cy, 16 * u, 4.4 * u, 0 if sgn > 0 else math.pi, LEAF + (255,), LEAF_DARK + (255,))
        d.ellipse([cx + sgn * 30 * u - 2 * u, cy - 2 * u, cx + sgn * 30 * u + 2 * u, cy + 2 * u], fill=INK + (230,))
    # Centre rune: a small diamond with a jade dot.
    d.polygon([(cx, cy - 7 * u), (cx + 7 * u, cy), (cx, cy + 7 * u), (cx - 7 * u, cy)], fill=INK + (240,))
    d.polygon([(cx, cy - 4 * u), (cx + 4 * u, cy), (cx, cy + 4 * u), (cx - 4 * u, cy)], fill=(70, 150, 128, 255))
    save(finish(big, w, h), "Divider")


def vine_track():
    w, h = 512, 32
    big = canvas(w, h)
    d = ImageDraw.Draw(big)
    u = S
    pts = []
    for i in range(w + 1):
        x = i
        y = h / 2 + math.sin(i / w * math.pi * 6) * 3.5
        pts.append((x * u, y * u))
    d.line(pts, fill=LEAF_DARK + (200,), width=int(2 * u), joint="curve")
    prng = random.Random(5)
    for k in range(18):
        x = (k + 0.5) / 18 * w
        y = h / 2 + math.sin(x / w * math.pi * 6) * 3.5
        up = -1 if k % 2 == 0 else 1
        ang = -math.pi / 2 * up * 0.75 + prng.uniform(-0.3, 0.3)
        if up < 0:
            ang = -ang
        leaf(d, x * u, y * u, prng.uniform(8, 11) * u, 3 * u, ang if up > 0 else -abs(ang), LEAF + (190,), LEAF_DARK + (220,))
    save(finish(big, w, h), "VineTrack")


# ---------------------------------------------------------------- white icons (tinted in code)

WHITE = (255, 255, 255, 255)


def icon_ok():
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    leaf(d, 10 * u, 54 * u, 56 * u, 17 * u, -math.pi / 4, WHITE)
    # Cut the vein out so the leaf reads at 16 px.
    d.line([(14 * u, 50 * u), (46 * u, 18 * u)], fill=(0, 0, 0, 0), width=int(3.5 * u))
    save(finish(big, size, size), "IconOk")


def icon_warn():
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    d.polygon([(32 * u, 3 * u), (61 * u, 32 * u), (32 * u, 61 * u), (3 * u, 32 * u)], fill=WHITE)
    d.rounded_rectangle([28.5 * u, 15 * u, 35.5 * u, 39 * u], 3 * u, fill=(0, 0, 0, 0))
    d.ellipse([28 * u, 43 * u, 36 * u, 51 * u], fill=(0, 0, 0, 0))
    save(finish(big, size, size), "IconWarn")


def icon_remove():
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    d.ellipse([2 * u, 2 * u, 62 * u, 62 * u], fill=WHITE)
    w = int(7 * u)
    d.line([(20 * u, 20 * u), (44 * u, 44 * u)], fill=(0, 0, 0, 0), width=w)
    d.line([(44 * u, 20 * u), (20 * u, 44 * u)], fill=(0, 0, 0, 0), width=w)
    save(finish(big, size, size), "IconRemove")


def icon_search():
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    d.ellipse([6 * u, 6 * u, 42 * u, 42 * u], outline=WHITE, width=int(6 * u))
    d.line([(37 * u, 37 * u), (57 * u, 57 * u)], fill=WHITE, width=int(8 * u))
    save(finish(big, size, size), "IconSearch")


def icon_hourglass():
    size = 64
    big = canvas(size, size)
    d = ImageDraw.Draw(big)
    u = S
    d.rounded_rectangle([12 * u, 4 * u, 52 * u, 10 * u], 2 * u, fill=WHITE)
    d.rounded_rectangle([12 * u, 54 * u, 52 * u, 60 * u], 2 * u, fill=WHITE)
    d.polygon([(16 * u, 10 * u), (48 * u, 10 * u), (35 * u, 32 * u), (48 * u, 54 * u), (16 * u, 54 * u), (29 * u, 32 * u)],
              outline=WHITE, width=int(4 * u))
    d.polygon([(22 * u, 50 * u), (42 * u, 50 * u), (32 * u, 38 * u)], fill=WHITE)
    save(finish(big, size, size), "IconHourglass")


def main():
    parchment()
    page_frame()
    cover_frame()
    # Button: bevelled plaque, light top to darker bottom; tinted per role.
    rounded_atlas("ButtonAtlas", 7, None, (70, 70, 70, 255), 1.5, gradient=(250, 178), highlight=(255, 255, 255, 150))
    # Panel: faint wash with a firm outline; tinted per state.
    rounded_atlas("PanelAtlas", 7, (255, 255, 255, 46), (255, 255, 255, 230), 1.5)
    inlay()
    socket()
    glow()
    spine_shade()
    ribbon()
    divider()
    vine_track()
    icon_ok()
    icon_warn()
    icon_remove()
    icon_search()
    icon_hourglass()


if __name__ == "__main__":
    main()
