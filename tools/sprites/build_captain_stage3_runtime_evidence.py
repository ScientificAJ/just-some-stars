#!/usr/bin/env python3
"""Build deterministic Stage 3 motion reviews from Unity runtime frames."""

from __future__ import annotations

from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "Builds/VisualEvidence/task12-stage3-final"
SPRITE_TOOLS = ROOT / "tools/sprites"
if str(SPRITE_TOOLS) not in sys.path:
    sys.path.insert(0, str(SPRITE_TOOLS))

import create_captain_package as captain_package

MASTER = (
    ROOT / "Assets/_JustSomeStars/Art/2D/Characters/Captain/Source/References"
    / "captain-right-facing-family-master-v2.png"
)
LABEL_FONT = ImageFont.truetype(
    str(ROOT / "Assets/TextMesh Pro/Fonts/LiberationSans.ttf"),
    20,
)
CAPTURES = (
    ("compact-run", "COMPACT / RUN / LAUNCH"),
    ("average-custom-scan", "AVERAGE / SCAN / ALTERNATE"),
    ("tallbroad-climb", "TALL BROAD / CLIMB / LAUNCH"),
)


def _load(path: Path) -> Image.Image:
    with Image.open(path) as opened:
        image = opened.convert("RGB")
    if image.size != (1280, 720):
        raise RuntimeError(f"Expected 1280x720 runtime evidence: {path}")
    return image


def _contact_sheet(frames: list[Image.Image], title: str) -> Image.Image:
    sheet = Image.new("RGB", (1280, 400), (6, 12, 28))
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 12), title, fill=(211, 235, 255), font=ImageFont.load_default())
    for index, frame in enumerate(frames):
        thumb = frame.resize((320, 180), Image.Resampling.LANCZOS)
        x = (index % 4) * 320
        y = 40 + (index // 4) * 180
        sheet.paste(thumb, (x, y))
        draw.text(
            (x + 8, y + 8),
            f"F{index:02d}",
            fill=(226, 242, 255),
            font=ImageFont.load_default(),
        )
    return sheet


def _family_lineup() -> Image.Image:
    with Image.open(MASTER) as opened:
        master = opened.convert("RGBA")
    preflight = captain_package.preflight_master(MASTER)
    sheet = Image.new("RGB", (1024, 512), (6, 15, 35))
    draw = ImageDraw.Draw(sheet)
    centers = (170, 512, 854)
    labels = (
        ("Compact", "COMPACT / 1.46 m"),
        ("Average", "AVERAGE / 1.56 m"),
        ("TallBroad", "TALL BROAD / 1.66 m"),
    )
    floor_y = 430
    for center_x, (family, label) in zip(centers, labels):
        prepared, _ = captain_package._prepare_family(
            master,
            preflight,
            family,
        )
        bounds = prepared.getchannel("A").getbbox()
        if bounds is None:
            raise RuntimeError(f"Captain family has no visible pixels: {family}")
        sprite = prepared.crop(bounds)
        sheet.paste(
            sprite,
            (center_x - sprite.width // 2, floor_y - sprite.height),
            sprite,
        )
        label_box = draw.textbbox((0, 0), label, font=LABEL_FONT)
        label_width = label_box[2] - label_box[0]
        draw.text(
            (center_x - label_width // 2, 468),
            label,
            fill=(229, 239, 255),
            font=LABEL_FONT,
        )
    draw.line((30, floor_y, 994, floor_y), fill=(99, 221, 255), width=2)
    return sheet


def main() -> int:
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    stills: list[Image.Image] = []
    for stem, title in CAPTURES:
        stills.append(_load(EVIDENCE / f"{stem}.png"))
        sequence = EVIDENCE / f"{stem}-sequence"
        frames = [_load(sequence / f"captain-frame-{index:02d}.png")
                  for index in range(8)]
        frames[0].save(
            EVIDENCE / f"{stem}.webp",
            format="WEBP",
            save_all=True,
            append_images=frames[1:],
            duration=83,
            loop=0,
            lossless=True,
            method=6,
        )
        _contact_sheet(frames, title).save(
            EVIDENCE / f"{stem}-sequence-contact.png",
            format="PNG",
            optimize=True,
        )

    montage = Image.new("RGB", (1280, 2160), (0, 0, 0))
    for index, still in enumerate(stills):
        montage.paste(still, (0, index * 720))
    montage.save(
        EVIDENCE / "stage3-final-three-captures.png",
        format="PNG",
        optimize=True,
    )
    _family_lineup().save(
        EVIDENCE / "family-lineup.png",
        format="PNG",
        optimize=True,
    )
    print("Captain Stage 3 runtime motion evidence rebuilt: 3 captures, 24 frames")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
