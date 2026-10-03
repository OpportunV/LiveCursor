"""Generates the Live Cursor demo cursor sets (frames and .cursorset files).

Usage: python generate.py [output_root]
Default output root: Assets/LiveCursorSamples/Demo/Cursors (relative to the repository root).
Requires Pillow.
"""
import json
import math
import os
import shutil
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

CANVAS = 128
SS = 4
HI = CANVAS * SS
OUTLINE = 3
TRANSITION_FRAMES = 8
TRANSITION_FRAME_MS = 30

PALETTES = {
    "Twinkle": {
        "body": (250, 250, 255, 255),
        "outline": (24, 22, 40, 255),
        "accent": (255, 196, 64, 255),
        "accent_light": (255, 248, 220, 255),
        "danger": (232, 64, 64, 255),
        "shadow": (0, 0, 0, 95),
    },
    "Midnight": {
        "body": (38, 42, 72, 255),
        "outline": (205, 222, 255, 255),
        "accent": (80, 220, 255, 255),
        "accent_light": (225, 250, 255, 255),
        "danger": (255, 92, 124, 255),
        "shadow": (0, 0, 0, 80),
    },
}


def hi(v):
    return v * SS


def with_alpha(color, alpha):
    return color[:3] + (int(color[3] * alpha),)


class Canvas:
    """Draws outlined shape groups at SS times the canvas resolution."""

    def __init__(self, palette):
        self.palette = palette
        self.layers = []

    def group(self):
        layer = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
        self.layers.append(layer)
        return ImageDraw.Draw(layer)

    def render(self, transform=None):
        result = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
        layers = [transform(layer) if transform else layer for layer in self.layers]
        if not layers:
            return result

        union = Image.new("L", (HI, HI), 0)
        for layer in layers:
            union = ImageChops.lighter(union, layer.getchannel("A"))

        shadow_mask = dilate(union, hi(OUTLINE)).filter(ImageFilter.GaussianBlur(hi(2.5)))
        shadow = Image.new("RGBA", (HI, HI), self.palette["shadow"][:3] + (0,))
        shadow.putalpha(shadow_mask.point(lambda a: a * self.palette["shadow"][3] // 255))
        result.alpha_composite(shadow, (hi(2), hi(3)))

        for layer in layers:
            alpha = layer.getchannel("A")
            outline_mask = dilate(alpha.point(lambda a: 255 if a > 8 else 0), hi(OUTLINE))
            outline = Image.new("RGBA", (HI, HI), self.palette["outline"])
            outline.putalpha(outline_mask)
            result.alpha_composite(outline)
            result.alpha_composite(layer)

        return result


def dilate(mask, radius):
    out = mask.copy()
    for r in (radius, radius * 0.66, radius * 0.33):
        for i in range(16):
            angle = 2 * math.pi * i / 16
            dx = int(round(r * math.cos(angle)))
            dy = int(round(r * math.sin(angle)))
            shifted = Image.new("L", mask.size, 0)
            shifted.paste(mask, (dx, dy))
            out = ImageChops.lighter(out, shifted)
    return out


def rrect(draw, box, radius, fill):
    draw.rounded_rectangle([hi(box[0]), hi(box[1]), hi(box[2]), hi(box[3])], radius=hi(radius), fill=fill)


def capsule(draw, a, b, width, fill):
    draw.line([hi(a[0]), hi(a[1]), hi(b[0]), hi(b[1])], fill=fill, width=int(hi(width)))
    r = width / 2
    for x, y in (a, b):
        draw.ellipse([hi(x - r), hi(y - r), hi(x + r), hi(y + r)], fill=fill)


def disc(draw, center, radius, fill):
    x, y = center
    draw.ellipse([hi(x - radius), hi(y - radius), hi(x + radius), hi(y + radius)], fill=fill)


def star_points(center, radius, inner, angle):
    cx, cy = center
    points = []
    for i in range(8):
        r = radius if i % 2 == 0 else inner
        a = math.radians(angle - 90 + i * 45)
        points.append((hi(cx + r * math.cos(a)), hi(cy + r * math.sin(a))))
    return points


def scale_about(point, scale):
    px, py = hi(point[0]), hi(point[1])

    def apply(layer):
        if scale == 1:
            return layer
        return layer.transform((HI, HI), Image.AFFINE,
                               (1 / scale, 0, px - px / scale, 0, 1 / scale, py - py / scale),
                               resample=Image.BICUBIC)

    return apply


# States: each returns (frames as hi-res images, hotspot, frame duration ms).

ARROW = [(0, 0), (0, 80), (19, 63), (32, 92), (44, 87), (31, 59), (56, 59)]
ARROW_TIP = (12, 10)


def state_default(p):
    sparkle = [(0.45, 0, 0.4), (0.75, 10, 0.75), (1.0, 20, 1.0), (0.85, 30, 0.85),
               (0.6, 40, 0.6), (0.4, 50, 0.35), (0.3, 60, 0.25), (0.35, 70, 0.3)]
    frames = []
    for scale, angle, glow in sparkle:
        c = Canvas(p)
        d = c.group()
        d.polygon([(hi(x + ARROW_TIP[0]), hi(y + ARROW_TIP[1])) for x, y in ARROW], fill=p["body"])
        image = c.render()
        halo = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
        disc(ImageDraw.Draw(halo), (86, 96), 12 * glow, with_alpha(p["accent"], 0.55 * glow))
        image.alpha_composite(halo.filter(ImageFilter.GaussianBlur(hi(4))))
        s = ImageDraw.Draw(image)
        s.polygon(star_points((86, 96), 15 * scale, 3.4 * scale, angle), fill=p["accent"])
        s.polygon(star_points((86, 96), 7 * scale, 1.8 * scale, angle), fill=p["accent_light"])
        frames.append(image)
    return frames, ARROW_TIP, 110


POINTER_TIP = (47, 11)


def draw_pointer(p, dy=0):
    c = Canvas(p)
    d = c.group()
    rrect(d, (40, 10 + dy, 54, 64 + dy), 7, p["body"])
    rrect(d, (34, 50 + dy, 86, 100 + dy), 15, p["body"])
    rrect(d, (52, 46 + dy, 66, 66 + dy), 7, p["body"])
    rrect(d, (64, 50 + dy, 78, 70 + dy), 7, p["body"])
    rrect(d, (74, 56 + dy, 87, 74 + dy), 6, p["body"])
    capsule(d, (27, 64 + dy), (41, 84 + dy), 13, p["body"])
    cuff = c.group()
    rrect(cuff, (38, 98 + dy, 82, 110 + dy), 4, p["accent"])
    return c


def state_pointer(p):
    frames = []
    for dy, ripple in [(0, 0), (1, 0), (2, 0.6), (2, 1.0), (1, 0.6), (0, 0)]:
        image = draw_pointer(p, dy).render()
        if ripple:
            ring = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
            r = 6 + 8 * ripple
            ImageDraw.Draw(ring).ellipse(
                [hi(POINTER_TIP[0] - r), hi(POINTER_TIP[1] - r), hi(POINTER_TIP[0] + r), hi(POINTER_TIP[1] + r)],
                outline=with_alpha(p["accent"], 1.1 - ripple * 0.6), width=hi(2))
            image.alpha_composite(ring)
        frames.append(image)
    return frames, POINTER_TIP, 90


TEXT_CENTER = (64, 64)


def state_text(p):
    frames = []
    for alpha in [1, 1, 1, 1, 0.7, 0.35, 0.7, 1]:
        c = Canvas(p)
        d = c.group()
        rrect(d, (61, 34, 67, 94), 2, p["body"])
        rrect(d, (50, 30, 78, 37), 3, p["body"])
        rrect(d, (50, 91, 78, 98), 3, p["body"])
        image = c.render()
        if alpha < 1:
            faded = image.getchannel("A").point(lambda a: int(a * alpha))
            image.putalpha(faded)
        frames.append(image)
    return frames, TEXT_CENTER, 100


def state_busy(p):
    frames = []
    for f in range(8):
        c = Canvas(p)
        for i in range(8):
            age = (f - i) % 8
            angle = 2 * math.pi * i / 8 - math.pi / 2
            center = (64 + 26 * math.cos(angle), 64 + 26 * math.sin(angle))
            d = c.group()
            color = p["accent"] if age == 0 else with_alpha(p["body"], 1 - age * 0.1)
            disc(d, center, 7.5 - age * 0.45, color)
        frames.append(c.render())
    return frames, (64, 64), 70


def state_blocked(p):
    frames = []
    for scale in [1, 1.03, 1.06, 1.03, 1, 0.98]:
        c = Canvas(p)
        d = c.group()
        d.ellipse([hi(34), hi(34), hi(94), hi(94)], outline=p["danger"], width=hi(10))
        capsule(d, (44, 44), (84, 84), 10, p["danger"])
        frames.append(c.render(scale_about((64, 64), scale)))
    return frames, (64, 64), 110


GRAB_POINT = (62, 72)


def draw_grab(p, wiggle):
    c = Canvas(p)
    d = c.group()
    rrect(d, (34, 56, 90, 104), 16, p["body"])
    for i, (x, top) in enumerate([(36, 28), (50, 20), (64, 22), (78, 32)]):
        offset = 3 * math.sin(wiggle + i * 0.9)
        rrect(d, (x, top + offset, x + 12, 70), 6, p["body"])
    capsule(d, (37, 82), (21, 60), 13, p["body"])
    cuff = c.group()
    rrect(cuff, (38, 100, 86, 112), 4, p["accent"])
    return c


def state_grab(p):
    frames = [draw_grab(p, 2 * math.pi * f / 6).render() for f in range(6)]
    return frames, GRAB_POINT, 110


def draw_grabbing(p):
    c = Canvas(p)
    d = c.group()
    rrect(d, (34, 52, 90, 104), 16, p["body"])
    for x in (36, 50, 64, 78):
        rrect(d, (x, 42, x + 13, 66), 6, p["body"])
    for x in (49, 63, 77):
        d.line([hi(x), hi(46), hi(x), hi(60)], fill=p["outline"], width=int(hi(1.5)))
    thumb = c.group()
    rrect(thumb, (30, 68, 66, 82), 7, p["body"])
    cuff = c.group()
    rrect(cuff, (38, 100, 86, 112), 4, p["accent"])
    return c


def state_grabbing(p):
    frames = [draw_grabbing(p).render(scale_about(GRAB_POINT, s)) for s in [1, 0.97, 0.95, 0.97]]
    return frames, GRAB_POINT, 120


def state_crosshair(p):
    frames = []
    for f in range(8):
        radius = 21 + 3 * math.sin(2 * math.pi * f / 8)
        c = Canvas(p)
        d = c.group()
        d.ellipse([hi(64 - radius), hi(64 - radius), hi(64 + radius), hi(64 + radius)], outline=p["body"],
                  width=hi(4))
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            capsule(d, (64 + dx * 9, 64 + dy * 9), (64 + dx * 36, 64 + dy * 36), 4, p["body"])
        dot = c.group()
        disc(dot, (64, 64), 3.5, p["accent"])
        frames.append(c.render())
    return frames, (64, 64), 90


STATES = [
    ("Default", state_default, "folder"),
    ("Pointer", state_pointer, "folder"),
    ("Text", state_text, "folder"),
    ("Busy", state_busy, "sheet"),
    ("Blocked", state_blocked, "folder"),
    ("Grab", state_grab, "folder"),
    ("Grabbing", state_grabbing, "folder"),
    ("Crosshair", state_crosshair, "sheet"),
]

TRANSITIONS = [
    ("Default", "Pointer"),
    ("Default", "Text"),
    ("Default", "Busy"),
    ("Default", "Blocked"),
    ("Default", "Crosshair"),
    ("Default", "Grab"),
    ("Pointer", "Grab"),
    ("Grab", "Grabbing"),
]


def smoothstep(t):
    return t * t * (3 - 2 * t)


def place(image, source_anchor, target_anchor, scale, alpha):
    sx, sy = hi(source_anchor[0]), hi(source_anchor[1])
    tx, ty = hi(target_anchor[0]), hi(target_anchor[1])
    moved = image.transform((HI, HI), Image.AFFINE,
                            (1 / scale, 0, sx - tx / scale, 0, 1 / scale, sy - ty / scale),
                            resample=Image.BICUBIC)
    moved.putalpha(moved.getchannel("A").point(lambda a: int(a * alpha)))
    return moved


def transition_frames(start, start_hotspot, end, end_hotspot):
    frames = [start]
    last = TRANSITION_FRAMES - 1
    for f in range(1, last):
        t = f / last
        e = smoothstep(t)
        hotspot = (start_hotspot[0] + (end_hotspot[0] - start_hotspot[0]) * t,
                   start_hotspot[1] + (end_hotspot[1] - start_hotspot[1]) * t)
        frame = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
        frame.alpha_composite(place(start, start_hotspot, hotspot, 1 - 0.4 * e, 1 - e))
        frame.alpha_composite(place(end, end_hotspot, hotspot, 0.6 + 0.4 * e, e))
        frames.append(frame)
    frames.append(end)
    return frames


def downsample(image):
    return image.resize((CANVAS, CANVAS), Image.LANCZOS)


def save_sequence(folder, frames):
    os.makedirs(folder, exist_ok=True)
    for i, frame in enumerate(frames):
        frame.save(os.path.join(folder, f"Frame_{i:02d}.png"))


def save_sheet(path, frames, columns):
    rows = math.ceil(len(frames) / columns)
    sheet = Image.new("RGBA", (CANVAS * columns, CANVAS * rows), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        sheet.paste(frame, ((i % columns) * CANVAS, (i // columns) * CANVAS))
    sheet.save(path)
    return rows


def generate(root, skin, palette):
    folder = os.path.join(root, skin)
    for child in ("Idle", "Transitions"):
        shutil.rmtree(os.path.join(folder, child), ignore_errors=True)
    os.makedirs(os.path.join(folder, "Idle"), exist_ok=True)

    hi_first = {}
    hotspots = {}
    states = []
    for name, draw, layout in STATES:
        frames, hotspot, duration = draw(palette)
        hi_first[name] = frames[0]
        hotspots[name] = hotspot
        small = [downsample(frame) for frame in frames]
        if layout == "sheet":
            columns = 4
            rows = save_sheet(os.path.join(folder, "Idle", f"{name}_{columns}x{math.ceil(len(small) / columns)}.png"),
                              small, columns)
            frames_definition = {"sheet": f"Idle/{name}_{columns}x{rows}.png", "columns": columns, "rows": rows,
                                 "count": len(small)}
        else:
            save_sequence(os.path.join(folder, "Idle", name), small)
            frames_definition = {"folder": f"Idle/{name}"}
        state = {"name": name, "frames": frames_definition, "frameDurationMs": duration}
        if tuple(hotspot) != ARROW_TIP:
            state["hotspot"] = [int(round(hotspot[0])), int(round(hotspot[1]))]
        states.append(state)

    transitions = []
    for start, end in TRANSITIONS:
        frames = transition_frames(hi_first[start], hotspots[start], hi_first[end], hotspots[end])
        name = f"{start}To{end}"
        save_sequence(os.path.join(folder, "Transitions", name), [downsample(frame) for frame in frames])
        transitions.append({
            "from": start,
            "to": end,
            "frames": {"folder": f"Transitions/{name}"},
            "frameDurationMs": TRANSITION_FRAME_MS,
            "includesEndpoints": True,
            "reversible": True,
        })

    definition = {
        "sizes": [32, 48, 64],
        "hotspot": list(ARROW_TIP),
        "states": states,
        "transitions": transitions,
        "code": {"className": "DemoCursorStates", "namespace": "Opportunv.LiveCursor.Samples",
                 "path": "../DemoCursorStates.cs"},
    }
    with open(os.path.join(folder, f"{skin}.cursorset"), "w", encoding="utf-8", newline="\n") as file:
        json.dump(definition, file, indent=2)
        file.write("\n")


def main():
    repo = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(repo, "Assets", "LiveCursorSamples", "Demo", "Cursors")
    for skin, palette in PALETTES.items():
        generate(root, skin, palette)
        print(f"{skin}: done")


if __name__ == "__main__":
    main()
