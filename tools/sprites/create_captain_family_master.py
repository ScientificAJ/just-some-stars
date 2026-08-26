#!/usr/bin/env python3
"""Author the three distinct Captain body-family silhouettes from the source lineup."""

from __future__ import annotations

import argparse
import hashlib
import os
import tempfile
from pathlib import Path

from PIL import Image


SOURCE_SHA256 = "657e04466a480284b716521a37bd6b2217d52832e0c647d523ebb6a3a1f9790c"
FAMILIES = (
    ("Compact", (254, 164, 462, 895), ((0.00, 1.10), (0.22, 1.14), (0.48, 1.22), (0.70, 1.17), (1.00, 1.13))),
    ("Average", (643, 92, 859, 894), ((0.00, 1.00), (1.00, 1.00))),
    ("TallBroad", (1042, 28, 1270, 895), ((0.00, 1.08), (0.20, 1.20), (0.46, 1.38), (0.67, 1.26), (1.00, 1.18))),
)


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _scale_at(position: float, keys: tuple[tuple[float, float], ...]) -> float:
    for index in range(len(keys) - 1):
        start_position, start_scale = keys[index]
        end_position, end_scale = keys[index + 1]
        if position <= end_position:
            amount = (position - start_position) / max(
                0.000001, end_position - start_position
            )
            amount = max(0.0, min(1.0, amount))
            amount = amount * amount * (3.0 - 2.0 * amount)
            return start_scale + (end_scale - start_scale) * amount
    return keys[-1][1]


def _morph(crop: Image.Image, keys: tuple[tuple[float, float], ...]) -> Image.Image:
    maximum_scale = max(scale for _, scale in keys)
    output_width = round(crop.width * maximum_scale)
    output = Image.new("RGBA", (output_width, crop.height), (0, 0, 0, 0))
    for y in range(crop.height):
        normalized_y = y / max(1, crop.height - 1)
        row_width = max(1, round(crop.width * _scale_at(normalized_y, keys)))
        row = crop.crop((0, y, crop.width, y + 1)).resize(
            (row_width, 1), Image.Resampling.LANCZOS
        )
        output.alpha_composite(row, ((output_width - row_width) // 2, y))
    return output


def build(source: Path, output: Path) -> None:
    if _sha256(source) != SOURCE_SHA256:
        raise RuntimeError("Captain family source does not match its locked hash")
    with Image.open(source) as opened:
        source_image = opened.convert("RGBA")
    canvas = Image.new("RGBA", source_image.size, (0, 0, 0, 0))
    for _, bounds, keys in FAMILIES:
        left, top, right, bottom = bounds
        crop = source_image.crop(bounds)
        morphed = _morph(crop, keys)
        center_x = (left + right) // 2
        canvas.alpha_composite(morphed, (center_x - morphed.width // 2, top))
    output.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=output.name + ".", suffix=".tmp", dir=output.parent
    )
    os.close(descriptor)
    temporary = Path(temporary_name)
    try:
        canvas.save(temporary, format="PNG", optimize=True)
        os.replace(temporary, output)
    finally:
        if temporary.exists():
            temporary.unlink()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    arguments = parser.parse_args()
    build(arguments.source.resolve(), arguments.output.resolve())
    print(f"Captain family master: {arguments.output} {_sha256(arguments.output.resolve())}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
