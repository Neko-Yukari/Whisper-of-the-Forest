from PIL import Image, ImageDraw, ImageFilter

SIZE = 64
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
flight = (0.80, -0.60)
perp = (-flight[1], flight[0])

trail_layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
tdraw = ImageDraw.Draw(trail_layer)
heads = []
for index, t in enumerate((0.0, 0.5, 1.0)):
    cx = 30 + perp[0] * 13.0 * (t - 0.5) + flight[0] * 6.0
    cy = 38 + perp[1] * 13.0 * (t - 0.5) + flight[1] * 6.0
    heads.append((cx, cy, 3.3 - index * 0.5))
    tail_len = 22 - index * 4
    tail_x = cx - flight[0] * tail_len
    tail_y = cy - flight[1] * tail_len
    steps = 30
    for step in range(steps):
        f = step / (steps - 1)
        x = tail_x + (cx - tail_x) * f
        y = tail_y + (cy - tail_y) * f
        r = (3.3 - index * 0.5) * (0.15 + 0.9 * f)
        alpha = int(20 + 150 * (f ** 1.8))
        tdraw.ellipse((x - r, y - r, x + r, y + r), fill=(70, 150, 255, alpha))

trail_layer = trail_layer.filter(ImageFilter.GaussianBlur(0.8))
img = Image.alpha_composite(img, trail_layer)

glow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
gdraw = ImageDraw.Draw(glow)
for cx, cy, width in heads:
    for radius, alpha in ((6.5, 45), (4.6, 85), (3.2, 140)):
        gdraw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=(120, 205, 255, alpha))
glow = glow.filter(ImageFilter.GaussianBlur(1.6))
img = Image.alpha_composite(img, glow)

core = ImageDraw.Draw(img)
for cx, cy, width in heads:
    core.ellipse((cx - width * 0.7, cy - width * 0.7, cx + width * 0.7, cy + width * 0.7), fill=(225, 245, 255, 255))
    core.ellipse((cx - width * 0.34, cy - width * 0.34, cx + width * 0.34, cy + width * 0.34), fill=(255, 255, 255, 255))

img.save(r"E:/SteamLibrary/steamapps/common/RimWorld/Moddev/Whisper-of-the-Forest/Textures/Experiments/Missiles/ArcaneMissiles.png")
print("saved", img.size)
