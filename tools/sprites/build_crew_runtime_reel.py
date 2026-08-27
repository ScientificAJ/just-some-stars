#!/usr/bin/env python3
"""Encode the verified Unity crew capture into a reviewable motion reel."""

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


CHARACTER_LABELS = ("MIRA", "JUNO", "KAI", "BEA", "ORI")
CHARACTER_X = (152, 360, 568, 776, 984)


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def load_font(path, size):
    if path.is_file():
        return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def build(evidence_root, font_path):
    evidence_root = Path(evidence_root).resolve()
    manifest_path = evidence_root / "runtime-capture-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    frame_paths = [
        evidence_root / "RuntimeFrames" / f"frame-{index:04d}.png"
        for index in range(manifest["frameCount"])
    ]
    missing = [str(path) for path in frame_paths if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"Missing Unity runtime frames: {missing}.")
    clips_by_frame = {}
    for clip in manifest["clips"]:
        for index in range(
            clip["firstFrame"],
            clip["firstFrame"] + clip["frameCount"],
        ):
            clips_by_frame[index] = clip["id"]

    title_font = load_font(Path(font_path), 24)
    label_font = load_font(Path(font_path), 16)
    frames = []
    for index, path in enumerate(frame_paths):
        frame = Image.open(path).convert("RGB")
        draw = ImageDraw.Draw(frame, "RGBA")
        draw.rounded_rectangle((24, 20, 278, 60), radius=10, fill=(3, 12, 24, 205))
        draw.text(
            (40, 28),
            clips_by_frame[index].upper(),
            font=title_font,
            fill=(115, 224, 255, 255),
        )
        for x, label in zip(CHARACTER_X, CHARACTER_LABELS):
            box = draw.textbbox((0, 0), label, font=label_font)
            width = box[2] - box[0]
            draw.text(
                (x - width / 2, 664),
                label,
                font=label_font,
                fill=(223, 236, 246, 230),
            )
        frames.append(frame)

    reel_path = evidence_root / "crew-runtime-motion-reel.webp"
    temporary = reel_path.with_suffix(".webp.staging")
    frames[0].save(
        temporary,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=round(1000 / manifest["framesPerSecond"]),
        loop=0,
        lossless=True,
        quality=100,
        method=2,
    )
    temporary.replace(reel_path)
    lineup_paths = {
        "right": evidence_root / "crew-same-scale-lineup.png",
        "left": evidence_root / "crew-same-scale-lineup-left.png",
    }
    manifest["runtimeReel"] = {
        "path": reel_path.name,
        "sha256": sha256(reel_path),
        "frameCount": len(frames),
        "framesPerSecond": manifest["framesPerSecond"],
        "durationSeconds": round(len(frames) / manifest["framesPerSecond"], 4),
        "source": "Unity Camera.Render through five SpriteAtlasAnimator instances",
    }
    manifest["sameScaleLineups"] = {
        facing: {
            "path": path.name,
            "sha256": sha256(path),
        }
        for facing, path in lineup_paths.items()
    }
    staging_manifest = manifest_path.with_suffix(".json.staging")
    staging_manifest.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    staging_manifest.replace(manifest_path)
    print(
        f"Encoded {len(frames)} Unity frames to {reel_path} "
        f"({manifest['runtimeReel']['sha256']})."
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence-root", required=True)
    parser.add_argument(
        "--font",
        default="Assets/TextMesh Pro/Fonts/LiberationSans.ttf",
    )
    arguments = parser.parse_args()
    build(arguments.evidence_root, arguments.font)


if __name__ == "__main__":
    main()
