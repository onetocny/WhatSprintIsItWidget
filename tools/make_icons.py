"""Generates the Sprint Widget icon/asset set.

Design: a rounded-square badge with an Azure DevOps blue -> teal gradient
(the teal #00B7C3 matches one of the sprint colors used by whatsprintis.it),
featuring a white checkered "sprint" finish flag with a motion swoosh.

All raster assets are rendered at 4x supersampling and downsampled with
LANCZOS for crisp edges at small sizes.
"""

import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SS = 4  # supersampling factor
ASSETS = os.path.join(os.path.dirname(__file__), "..", "src", "Assets")
ASSETS = os.path.abspath(ASSETS)
os.makedirs(ASSETS, exist_ok=True)

# Azure DevOps blue -> teal.
TOP = (0x2B, 0x88, 0xD8)
BOTTOM = (0x00, 0xB7, 0xC3)
WHITE = (255, 255, 255, 255)


def _font(path_candidates, size):
    for p in path_candidates:
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


SEGOE_BOLD = [r"C:\Windows\Fonts\segoeuib.ttf"]
SEGOE_SEMI = [r"C:\Windows\Fonts\seguisb.ttf", r"C:\Windows\Fonts\segoeui.ttf"]
SEGOE_LIGHT = [r"C:\Windows\Fonts\segoeuil.ttf", r"C:\Windows\Fonts\segoeui.ttf"]


def gradient(w, h, top, bottom):
    base = Image.new("RGB", (w, h), top)
    draw = ImageDraw.Draw(base)
    for y in range(h):
        t = y / max(1, h - 1)
        r = round(top[0] + (bottom[0] - top[0]) * t)
        g = round(top[1] + (bottom[1] - top[1]) * t)
        b = round(top[2] + (bottom[2] - top[2]) * t)
        draw.line([(0, y), (w, y)], fill=(r, g, b))
    return base


def rounded_mask(w, h, radius):
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=radius, fill=255)
    return m


def _rrect(draw, box, radius, **kw):
    x0, y0, x1, y1 = box
    max_r = max(0, (min(x1 - x0, y1 - y0) - 2) // 2)
    draw.rounded_rectangle(box, radius=min(radius, max_r), **kw)


def draw_flag(draw, cx, cy, s):
    """Draw a white checkered finish flag centred around (cx, cy), size ~s."""
    # Pole.
    pole_w = max(2, int(s * 0.09))
    pole_h = int(s * 1.15)
    pole_x = int(cx - s * 0.62)
    pole_top = int(cy - s * 0.62)
    _rrect(
        draw,
        [pole_x, pole_top, pole_x + pole_w, pole_top + pole_h],
        radius=pole_w // 2,
        fill=WHITE,
    )

    # Checkered flag body: 3 cols x 3 rows alternating white / translucent.
    cols, rows = 3, 3
    fw = int(s * 1.05)
    fh = int(s * 0.72)
    fx = pole_x + pole_w
    fy = pole_top
    cw = fw / cols
    ch = fh / rows
    # Backing panel (semi-transparent white so gradient shows through dark cells).
    draw.rectangle([fx, fy, fx + fw, fy + fh], fill=(255, 255, 255, 70))
    for r in range(rows):
        for c in range(cols):
            if (r + c) % 2 == 0:
                x0 = fx + c * cw
                y0 = fy + r * ch
                draw.rectangle([x0, y0, x0 + cw, y0 + ch], fill=WHITE)

    # Motion swoosh (speed lines) trailing the flag.
    line_w = max(2, int(s * 0.07))
    for i, yoff in enumerate((0.15, 0.42, 0.69)):
        y = int(fy + fh * yoff)
        x1 = fx + fw + int(s * 0.06)
        x2 = x1 + int(s * (0.42 - i * 0.10))
        draw.line([(x1, y), (x2, y)], fill=(255, 255, 255, 210), width=line_w)


def make_badge(size, radius_ratio=0.20):
    w = h = size * SS
    grad = gradient(w, h, TOP, BOTTOM).convert("RGBA")
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    draw_flag(d, w * 0.52, h * 0.5, w * 0.34)
    grad = Image.alpha_composite(grad, layer)
    mask = rounded_mask(w, h, int(w * radius_ratio))
    grad.putalpha(mask)
    return grad.resize((size, size), Image.LANCZOS)


def save(img, name):
    img.save(os.path.join(ASSETS, name))
    print("wrote", name, img.size)


# --- Square logos -----------------------------------------------------------
master = make_badge(256)
save(master, "WhatSprintIsItWidget.png")
for name, sz in [
    ("Square44x44Logo.png", 44),
    ("Square71x71Logo.png", 71),
    ("Square150x150Logo.png", 150),
    ("Square310x310Logo.png", 310),
    ("StoreLogo.png", 50),
]:
    save(make_badge(sz), name)

# --- Wide tile: badge + wordmark -------------------------------------------
def make_wide(w=310, h=150):
    W, H = w * SS, h * SS
    grad = gradient(W, H, TOP, BOTTOM).convert("RGBA")
    d = ImageDraw.Draw(grad)
    draw_flag(d, W * 0.16, H * 0.5, H * 0.30)
    f_bold = _font(SEGOE_BOLD, int(H * 0.24))
    f_light = _font(SEGOE_LIGHT, int(H * 0.13))
    d.text((W * 0.30, H * 0.30), "Sprint", font=f_bold, fill=WHITE)
    d.text((W * 0.305, H * 0.58), "Azure DevOps", font=f_light, fill=(255, 255, 255, 220))
    return grad.resize((w, h), Image.LANCZOS)

save(make_wide(), "Wide310x150Logo.png")

# --- Store screenshot / picker preview (mirrors the Adaptive Card) ----------
def make_screenshot(w=320, h=176):
    W, H = w * SS, h * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Card background (light theme) with subtle shadow.
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ds = ImageDraw.Draw(shadow)
    pad = int(W * 0.04)
    ds.rounded_rectangle([pad, pad, W - pad, H - pad], radius=int(H * 0.10),
                         fill=(0, 0, 0, 60))
    shadow = shadow.filter(ImageFilter.GaussianBlur(int(W * 0.02)))
    img = Image.alpha_composite(img, shadow)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([pad, pad, W - pad, H - pad], radius=int(H * 0.10),
                        fill=(255, 255, 255, 255))

    fg = (0x20, 0x20, 0x20)
    subtle = (0x60, 0x60, 0x60)
    accent = (0x00, 0x78, 0xD4)
    f_head = _font(SEGOE_SEMI, int(H * 0.11))
    f_small = _font(SEGOE_LIGHT, int(H * 0.095))
    f_big = _font(SEGOE_BOLD, int(H * 0.30))

    # Header.
    hdr = "Azure DevOps"
    tw = d.textlength(hdr, font=f_head)
    d.text(((W - tw) / 2, H * 0.13), hdr, font=f_head, fill=subtle)

    # Two columns: sprint / week.
    def column(cx, label, value):
        lw = d.textlength(label, font=f_small)
        d.text((cx - lw / 2, H * 0.30), label, font=f_small, fill=subtle)
        vw = d.textlength(value, font=f_big)
        d.text((cx - vw / 2, H * 0.40), value, font=f_big, fill=fg)

    column(W * 0.32, "sprint", "277")
    column(W * 0.68, "week", "3")

    # Action pill.
    btn = "Open whatsprintis.it"
    f_btn = _font(SEGOE_SEMI, int(H * 0.085))
    bw = d.textlength(btn, font=f_btn)
    bx0 = (W - bw) / 2 - W * 0.03
    bx1 = (W + bw) / 2 + W * 0.03
    by0 = H * 0.76
    by1 = H * 0.90
    _rrect(d, [bx0, by0, bx1, by1], radius=int((by1 - by0) / 2),
           outline=accent, width=max(1, int(SS * 1.2)))
    d.text(((W - bw) / 2, by0 + (by1 - by0) * 0.22), btn, font=f_btn, fill=accent)

    return img.resize((w, h), Image.LANCZOS)

save(make_screenshot(), "WhatSprintIsItWidgetScreenshot.png")

# --- Scale-qualified variants (MSIX packaging expects these) -----------------
SCALES = [100, 125, 150, 200, 400]
SQUARE_SPECS = {
    "Square44x44Logo": 44,
    "Square71x71Logo": 71,
    "Square150x150Logo": 150,
    "Square310x310Logo": 310,
    "StoreLogo": 50,
}
for base_name, dip in SQUARE_SPECS.items():
    for scale in SCALES:
        px = max(1, round(dip * scale / 100))
        save(make_badge(px), f"{base_name}.scale-{scale}.png")

for scale in SCALES:
    w = round(310 * scale / 100)
    h = round(150 * scale / 100)
    save(make_wide(w, h), f"Wide310x150Logo.scale-{scale}.png")

# --- .ico for general use ---------------------------------------------------
master.save(os.path.join(ASSETS, "WhatSprintIsItWidget.ico"),
            sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("wrote WhatSprintIsItWidget.ico")
print("done ->", ASSETS)
