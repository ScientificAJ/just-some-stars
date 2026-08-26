#!/usr/bin/env python3
"""Assemble registered coherent rows into one deterministic PNG atlas."""

import hashlib
import json
import re
from pathlib import Path

from PIL import Image


def assemble_atlas(request_path, request, clip_rows, staging_root):
    character_id = request["characterId"]
    frame_width = request["frameWidth"]
    frame_height = request["frameHeight"]
    columns = request["atlasColumns"]
    if columns < max(len(row["frames"]) for row in clip_rows):
        raise ValueError("atlasColumns cannot fit the longest complete row.")
    atlas_width = columns * frame_width
    atlas_height = len(clip_rows) * frame_height
    atlas = Image.new("RGBA", (atlas_width, atlas_height))
    clips = []
    sprite_names = set()
    for row_index, row in enumerate(clip_rows):
        clip = row["clip"]
        frames = []
        for frame_index, (frame, diagnostic) in enumerate(
            zip(row["frames"], row["diagnostics"])
        ):
            sprite_name = _sprite_name(character_id, clip["id"], frame_index)
            if sprite_name in sprite_names:
                raise ValueError(
                    f"Duplicate sprite name after normalization: {sprite_name}."
                )
            sprite_names.add(sprite_name)
            x = frame_index * frame_width
            y_top = row_index * frame_height
            atlas.alpha_composite(frame, (x, y_top))
            frames.append(
                {
                    "index": frame_index,
                    "spriteName": sprite_name,
                    "rectPixels": {
                        "x": x,
                        "y": atlas_height - y_top - frame_height,
                        "width": frame_width,
                        "height": frame_height,
                    },
                    "pivotNormalized": [
                        clip["pivotPixels"][0] / frame_width,
                        clip["pivotPixels"][1] / frame_height,
                    ],
                    "durationSeconds": round(1.0 / clip["cadenceFps"], 9),
                    "contacts": clip["contacts"][frame_index],
                    "events": clip["events"][frame_index],
                    **diagnostic,
                }
            )
        clips.append(
            {
                "id": clip["id"],
                "facing": clip["facing"],
                "loopMode": clip["loopMode"],
                "cadenceFps": clip["cadenceFps"],
                "sourceStrip": clip["sourceStrip"],
                "sourceStripSha256": _sha256(row["sourcePath"]),
                "frames": frames,
            }
        )

    atlas_path = staging_root / f"{character_id}.png"
    atlas.save(atlas_path, format="PNG", optimize=False, compress_level=9)
    manifest = {
        "schemaVersion": 1,
        "characterId": character_id,
        "pixelsPerUnit": request["pixelsPerUnit"],
        "sourceRequestSha256": _sha256(request_path),
        "atlas": {
            "path": atlas_path.name,
            "format": "PNG",
            "width": atlas_width,
            "height": atlas_height,
            "sha256": _sha256(atlas_path),
        },
        "processing": {
            "alphaThreshold": request["alphaThreshold"],
            "maximumBaselineCorrectionPixels":
                request["maximumBaselineCorrectionPixels"],
            "maximumInteriorAlphaHolePixels":
                request.get("maximumInteriorAlphaHolePixels", 0),
            "repairMode": request["repair"]["mode"],
        },
        "clips": clips,
        "validation": {"isValid": True, "issues": []},
    }
    manifest_path = staging_root / f"{character_id}.sprite-manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (staging_root / f"{character_id}.sprite-manifest.sha256").write_text(
        _sha256(manifest_path) + "\n",
        encoding="ascii",
    )
    return atlas_path, manifest_path, manifest


def _sprite_name(character_id, clip_id, frame_index):
    normalized = re.sub(r"[^A-Za-z0-9]+", "_", clip_id).strip("_")
    return f"{character_id}__{normalized}__{frame_index:03d}"


def _sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()
