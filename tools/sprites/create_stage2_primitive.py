#!/usr/bin/env python3
"""Draw the deterministic painterly primitive used by the Stage 2 round trip."""

import argparse
import json
import math
import random
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw


FRAME_WIDTH = 128
FRAME_HEIGHT = 192
SCALE = 4
MARKER_RGBA = (0, 224, 255, 255)


def build_fixture(root):
    root = Path(root).resolve()
    source = root / "source"
    source.mkdir(parents=True, exist_ok=True)
    idle_path = source / "primitive-stage2-idle.png"
    run_path = source / "primitive-stage2-run.png"
    _render_strip(idle_path, 4, running=False)
    _render_strip(run_path, 8, running=True)
    request = {
        "schemaVersion": 1,
        "characterId": "primitive-stage2",
        "pixelsPerUnit": 128,
        "atlasColumns": 8,
        "frameWidth": FRAME_WIDTH,
        "frameHeight": FRAME_HEIGHT,
        "alphaThreshold": 8,
        "maximumInteriorAlphaHolePixels": 0,
        "maximumBaselineCorrectionPixels": 3,
        "facingMarker": {"rgba": list(MARKER_RGBA), "minimumPixels": 6},
        "repair": {"mode": "complete-rows-only"},
        "clips": [
            {
                "id": "primitive.idle.right",
                "sourceStrip": "source/primitive-stage2-idle.png",
                "frameCount": 4,
                "facing": "Right",
                "cadenceFps": 12,
                "loopMode": "Loop",
                "pivotPixels": [64, 16],
                "contacts": [["LeftFoot", "RightFoot"]] * 4,
                "events": [[], [], [], []],
            },
            {
                "id": "primitive.run.right",
                "sourceStrip": "source/primitive-stage2-run.png",
                "frameCount": 8,
                "facing": "Right",
                "cadenceFps": 12,
                "loopMode": "Loop",
                "pivotPixels": [64, 16],
                "contacts": [
                    ["LeftFoot"], [], ["RightFoot"], [],
                    ["LeftFoot"], [], ["RightFoot"], [],
                ],
                "events": [
                    [{"id": "step-left", "kind": "FootContact"}],
                    [],
                    [{"id": "step-right", "kind": "FootContact"}],
                    [],
                    [{"id": "step-left", "kind": "FootContact"}],
                    [],
                    [{"id": "step-right", "kind": "FootContact"}],
                    [],
                ],
            },
        ],
    }
    request_path = root / "primitive-stage2-request.json"
    request_path.write_text(
        json.dumps(request, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    return request_path


def _render_strip(path, frame_count, running):
    strip = Image.new("RGBA", (FRAME_WIDTH * frame_count, FRAME_HEIGHT))
    for index in range(frame_count):
        frame = _render_frame(index, frame_count, running)
        strip.alpha_composite(frame, (index * FRAME_WIDTH, 0))
    strip.save(path, format="PNG", optimize=False, compress_level=9)


def _render_frame(index, frame_count, running):
    canvas = Image.new(
        "RGBA",
        (FRAME_WIDTH * SCALE, FRAME_HEIGHT * SCALE),
    )
    draw = ImageDraw.Draw(canvas)
    phase = 2.0 * math.pi * index / frame_count
    bounce = -2.0 * abs(math.sin(phase)) if running else math.sin(phase) * 1.2
    lean = 3.0 if running else 0.0

    def p(points):
        return [(round(x * SCALE), round((y + bounce) * SCALE)) for x, y in points]

    def box(values):
        x0, y0, x1, y1 = values
        return tuple(round(value * SCALE) for value in (x0, y0 + bounce, x1, y1 + bounce))

    outline = (43, 31, 55, 255)
    deep_blue = (34, 65, 98, 255)
    suit_blue = (52, 105, 139, 255)
    canvas_tan = (214, 151, 84, 255)
    warm_light = (246, 192, 121, 255)
    signal = (84, 218, 255, 255)

    draw.rounded_rectangle(box((31, 67, 49, 125)), 8 * SCALE,
                           fill=(43, 71, 95, 255), outline=outline, width=2 * SCALE)
    draw.polygon(p([(43 + lean, 59), (78 + lean, 57), (91 + lean, 124),
                    (75 + lean, 143), (46 + lean, 136)]),
                 fill=suit_blue, outline=outline)
    draw.polygon(p([(47 + lean, 65), (76 + lean, 63), (81 + lean, 87),
                    (45 + lean, 91)]), fill=canvas_tan)
    draw.ellipse(box((45 + lean, 20, 84 + lean, 62)),
                 fill=warm_light, outline=outline, width=2 * SCALE)
    draw.pieslice(box((50 + lean, 28, 88 + lean, 58)), 195, 350,
                  fill=(27, 70, 104, 255), outline=outline, width=2 * SCALE)
    draw.ellipse(box((70 + lean, 37, 84 + lean, 49)),
                 fill=signal, outline=(30, 83, 118, 255), width=SCALE)

    arm_wave = math.sin(phase) if running else math.sin(phase) * 0.15
    draw.line(p([(82 + lean, 72), (94 + lean, 98 - arm_wave * 13),
                 (104 + lean, 119 - arm_wave * 18)]),
              fill=outline, width=11 * SCALE, joint="curve")
    draw.line(p([(82 + lean, 72), (94 + lean, 98 - arm_wave * 13),
                 (104 + lean, 119 - arm_wave * 18)]),
              fill=canvas_tan, width=7 * SCALE, joint="curve")
    draw.line(p([(46 + lean, 74), (35 + lean, 100 + arm_wave * 12),
                 (29 + lean, 122 + arm_wave * 15)]),
              fill=outline, width=11 * SCALE, joint="curve")
    draw.line(p([(46 + lean, 74), (35 + lean, 100 + arm_wave * 12),
                 (29 + lean, 122 + arm_wave * 15)]),
              fill=canvas_tan, width=7 * SCALE, joint="curve")
    if running:
        cycle = index % 4
        if cycle in (0, 1):
            left_foot = (45, 174)
            right_foot = (91, 160 if cycle == 0 else 154)
        else:
            left_foot = (38, 164 if cycle == 2 else 156)
            right_foot = (83, 174)
    else:
        left_foot = (50, 174)
        right_foot = (78, 174)
    hip_left = (55 + lean, 132)
    hip_right = (72 + lean, 132)
    for hip, foot, color in (
        (hip_left, left_foot, deep_blue),
        (hip_right, right_foot, suit_blue),
    ):
        knee = ((hip[0] + foot[0]) * 0.5 + 3, (hip[1] + foot[1]) * 0.5)
        draw.line(p([hip, knee, foot]), fill=outline, width=15 * SCALE, joint="curve")
        draw.line(p([hip, knee, foot]), fill=color, width=10 * SCALE, joint="curve")
        draw.rounded_rectangle(
            box((foot[0] - 6, foot[1] - 4, foot[0] + 10, foot[1] + 2)),
            2 * SCALE,
            fill=(43, 48, 64, 255),
            outline=outline,
            width=SCALE,
        )

    draw.ellipse(box((56 + lean, 88, 73 + lean, 105)),
                 fill=(61, 181, 224, 255), outline=outline, width=SCALE)
    rng = random.Random(1200 + (100 if running else 0))
    for _ in range(10):
        x = rng.uniform(49 + lean, 79 + lean)
        y = rng.uniform(68 + bounce, 128 + bounce)
        radius = rng.choice((0.7, 1.0, 1.3)) * SCALE
        draw.ellipse(
            (x * SCALE - radius, y * SCALE - radius,
             x * SCALE + radius, y * SCALE + radius),
            fill=(244, 184, 106, 255),
        )

    frame = canvas.resize(
        (FRAME_WIDTH, FRAME_HEIGHT),
        Image.Resampling.LANCZOS,
    )
    _seal_tiny_alpha_holes(frame, alpha_threshold=8, maximum_pixels=4)
    marker_draw = ImageDraw.Draw(frame)
    marker_draw.rectangle((89, 45 + round(bounce), 92, 48 + round(bounce)),
                          fill=MARKER_RGBA)
    return frame


def _seal_tiny_alpha_holes(frame, alpha_threshold, maximum_pixels):
    alpha = frame.getchannel("A")
    alpha_pixels = alpha.load()
    visible = [
        (x, y)
        for y in range(frame.height)
        for x in range(frame.width)
        if alpha_pixels[x, y] > alpha_threshold
    ]
    if not visible:
        raise RuntimeError("Generated primitive frame has no visible pixels.")
    min_x = min(x for x, _ in visible)
    max_x = max(x for x, _ in visible)
    min_y = min(y for _, y in visible)
    max_y = max(y for _, y in visible)
    transparent = {
        (x, y)
        for y in range(min_y, max_y + 1)
        for x in range(min_x, max_x + 1)
        if alpha_pixels[x, y] <= alpha_threshold
    }
    outside = set()
    queue = deque()
    for x in range(min_x, max_x + 1):
        queue.extend(((x, min_y), (x, max_y)))
    for y in range(min_y, max_y + 1):
        queue.extend(((min_x, y), (max_x, y)))
    while queue:
        point = queue.popleft()
        if point not in transparent or point in outside:
            continue
        outside.add(point)
        x, y = point
        for neighbor in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if min_x <= neighbor[0] <= max_x and min_y <= neighbor[1] <= max_y:
                queue.append(neighbor)
    holes = sorted(transparent - outside, key=lambda point: (point[1], point[0]))
    if len(holes) > maximum_pixels:
        raise RuntimeError(
            f"Generated primitive contains {len(holes)} interior alpha-hole pixels; "
            f"maximum repairable count is {maximum_pixels}."
        )
    pixels = frame.load()
    for x, y in holes:
        candidates = []
        for radius in range(1, 9):
            for ny in range(max(0, y - radius), min(frame.height, y + radius + 1)):
                for nx in range(max(0, x - radius), min(frame.width, x + radius + 1)):
                    if alpha_pixels[nx, ny] >= 250:
                        candidates.append((abs(nx - x) + abs(ny - y), ny, nx))
            if candidates:
                break
        if not candidates:
            raise RuntimeError("Could not repair a tiny generated alpha hole.")
        _, source_y, source_x = min(candidates)
        red, green, blue, _ = pixels[source_x, source_y]
        pixels[x, y] = (red, green, blue, 255)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True, type=Path)
    arguments = parser.parse_args()
    request = build_fixture(arguments.root)
    print(request, flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
