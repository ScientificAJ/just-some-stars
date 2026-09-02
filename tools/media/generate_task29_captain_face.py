#!/usr/bin/env python3
"""Publish the Captain expression and speech-overlay atlas from its locked sheet."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps


ROOT = Path(__file__).resolve().parents[2]
REFERENCE = ROOT / "Assets/_JustSomeStars/Art/Characters/References/expressions.png"
REFERENCE_SHA256 = "cda3afe4e215237313dc18ab190eef6080387eb22a4c1677bc13a307b3cea655"
CHARACTER_ROOT = ROOT / "Assets/_JustSomeStars/Art/2D/Characters/Captain"
ATLAS = CHARACTER_ROOT / "Atlases/neutral/captain-face-speech.png"
MANIFEST = ATLAS.with_suffix(".sprite-manifest.json")
MANIFEST_HASH = ATLAS.with_suffix(".sprite-manifest.sha256")
EXPRESSION_ROW = CHARACTER_ROOT / "Source/Rows/neutral/captain-expressions-2x.png"
SPEECH_ROW = CHARACTER_ROOT / "Source/Rows/neutral/captain-speech-2x.png"
EXPRESSIONS = (
    "neutral", "happy", "curious", "worried", "afraid",
    "surprised", "determined", "sad", "blink", "speaking",
)
SPEECH_SHAPES = 6


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def alpha_bounds(image: Image.Image) -> list[int]:
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        return [0, 0, 0, 0]
    left, top, right, bottom = bounds
    return [left, image.height - bottom, right - left, bottom - top]


def speech_variants() -> list[Image.Image]:
    dark = (48, 20, 22, 255)
    warm = (180, 92, 76, 255)
    result: list[Image.Image] = []
    for index in range(SPEECH_SHAPES):
        frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        draw = ImageDraw.Draw(frame)
        if index == 0:
            draw.ellipse((44, 80, 84, 98), fill=dark, outline=warm, width=3)
        elif index == 1:
            draw.arc((43, 77, 85, 96), 20, 160, fill=warm, width=4)
        elif index == 2:
            draw.ellipse((54, 78, 74, 101), fill=dark, outline=warm, width=3)
        elif index == 3:
            draw.rounded_rectangle(
                (46, 84, 82, 94), radius=5, fill=dark, outline=warm, width=3)
        elif index == 4:
            draw.ellipse((56, 75, 72, 103), fill=dark, outline=warm, width=3)
        else:
            polygon = (43, 88, 55, 82, 65, 87, 75, 82, 86, 88,
                       75, 96, 64, 93, 53, 96)
            draw.polygon(polygon, fill=dark)
            draw.line(polygon[:10], fill=warm, width=3, joint="curve")
        result.append(frame)
    return result


def build(stage: Path) -> dict[Path, Path]:
    if sha256(REFERENCE) != REFERENCE_SHA256:
        raise RuntimeError("Captain expression authority hash changed.")
    sheet = Image.open(REFERENCE).convert("RGB")
    images: list[Image.Image] = []
    for index in range(len(EXPRESSIONS)):
        left = 136 + index * 125
        crop = sheet.crop((left, 100, left + 125, 233))
        images.append(ImageOps.fit(
            crop, (128, 128), Image.Resampling.LANCZOS).convert("RGBA"))
    images.extend(speech_variants())

    atlas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    expression_strip = Image.new("RGBA", (2560, 256), (0, 0, 0, 0))
    speech_strip = Image.new("RGBA", (1536, 256), (0, 0, 0, 0))
    for index, image in enumerate(images):
        atlas.alpha_composite(image, ((index % 4) * 128, (index // 4) * 128))
        if index < len(EXPRESSIONS):
            expression_strip.alpha_composite(
                image.resize((256, 256), Image.Resampling.LANCZOS),
                (index * 256, 0))
        else:
            speech_strip.alpha_composite(
                image.resize((256, 256), Image.Resampling.LANCZOS),
                ((index - len(EXPRESSIONS)) * 256, 0))

    staged_atlas = stage / "captain-face-speech.png"
    staged_expression = stage / "captain-expressions-2x.png"
    staged_speech = stage / "captain-speech-2x.png"
    atlas.save(staged_atlas, format="PNG", optimize=False, compress_level=9)
    expression_strip.save(
        staged_expression, format="PNG", optimize=False, compress_level=9)
    speech_strip.save(staged_speech, format="PNG", optimize=False, compress_level=9)

    clips = []
    for index, image in enumerate(images):
        stable_id = (
            f"captain.expression.{EXPRESSIONS[index]}"
            if index < len(EXPRESSIONS)
            else f"captain.speech.{index - len(EXPRESSIONS)}"
        )
        source = staged_expression if index < len(EXPRESSIONS) else staged_speech
        x = (index % 4) * 128
        top = (index // 4) * 128
        clips.append({
            "id": stable_id,
            "facing": "Neutral",
            "loopMode": "HoldLast",
            "cadenceFps": 12,
            "sourceStrip": (
                "Source/Rows/neutral/captain-expressions-2x.png"
                if index < len(EXPRESSIONS)
                else "Source/Rows/neutral/captain-speech-2x.png"
            ),
            "sourceStripSha256": sha256(source),
            "frames": [{
                "index": 0,
                "spriteName": f"captain__face_speech__{index:03d}",
                "rectPixels": {
                    "x": x,
                    "y": 512 - top - 128,
                    "width": 128,
                    "height": 128,
                },
                "pivotNormalized": [0.5, 0.5],
                "durationSeconds": round(1.0 / 12.0, 9),
                "contacts": [],
                "events": [],
                "anchors": [],
                "sourceBaselinePixels": 0,
                "registrationOffsetPixels": 0,
                "registeredBaselinePixels": 0,
                "alphaBoundsPixels": alpha_bounds(image),
                "interiorAlphaHolePixels": 0,
            }],
        })
    document = {
        "schemaVersion": 1,
        "characterId": "captain",
        "pixelsPerUnit": 100,
        "sourceRequestSha256": REFERENCE_SHA256,
        "atlas": {
            "path": ATLAS.name,
            "format": "PNG",
            "width": 512,
            "height": 512,
            "sha256": sha256(staged_atlas),
        },
        "clips": clips,
        "validation": {"isValid": True, "issues": []},
    }
    staged_manifest = stage / MANIFEST.name
    staged_manifest.write_text(
        json.dumps(document, indent=2, sort_keys=True) + "\n",
        encoding="utf-8")
    staged_hash = stage / MANIFEST_HASH.name
    staged_hash.write_text(sha256(staged_manifest) + "\n", encoding="ascii")
    return {
        staged_atlas: ATLAS,
        staged_manifest: MANIFEST,
        staged_hash: MANIFEST_HASH,
        staged_expression: EXPRESSION_ROW,
        staged_speech: SPEECH_ROW,
    }


def validate() -> None:
    if not all(path.is_file() for path in (
            ATLAS, MANIFEST, MANIFEST_HASH, EXPRESSION_ROW, SPEECH_ROW)):
        raise RuntimeError("Captain facial publication is incomplete.")
    document = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if document.get("characterId") != "captain" or \
            document.get("sourceRequestSha256") != REFERENCE_SHA256 or \
            document.get("atlas", {}).get("sha256") != sha256(ATLAS) or \
            len(document.get("clips", [])) != 16 or \
            MANIFEST_HASH.read_text(encoding="ascii").strip() != sha256(MANIFEST):
        raise RuntimeError("Captain facial publication failed provenance validation.")


def publish() -> None:
    with tempfile.TemporaryDirectory(prefix="jss-task29-face-") as temporary:
        staged = build(Path(temporary))
        originals = {
            destination: destination.read_bytes() if destination.exists() else None
            for destination in staged.values()
        }
        try:
            for source, destination in staged.items():
                destination.parent.mkdir(parents=True, exist_ok=True)
                replacement = destination.with_suffix(destination.suffix + ".tmp")
                replacement.write_bytes(source.read_bytes())
                os.replace(replacement, destination)
            validate()
        except BaseException:
            for destination, payload in originals.items():
                if payload is None:
                    destination.unlink(missing_ok=True)
                else:
                    replacement = destination.with_suffix(destination.suffix + ".restore")
                    replacement.write_bytes(payload)
                    os.replace(replacement, destination)
            raise


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    if args.validate_only:
        validate()
    else:
        publish()
    print("Task 29 Captain face publication is complete and validated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
