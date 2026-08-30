#!/usr/bin/env python3
"""Build deterministic Task 17 layered 2D ship sprites from the approved master."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance


ROOT = Path(__file__).resolve().parents[2]
SHIP_ROOT = ROOT / "Assets/_JustSomeStars/Art/2D/Ship/PlayerShip"
MASTER_PATH = SHIP_ROOT / "PlayerShipMaster.png"
OUTPUT_SIZE = (768, 512)


def polygon_mask(size: tuple[int, int], polygons: list[list[tuple[int, int]]]) -> Image.Image:
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    for polygon in polygons:
        draw.polygon(polygon, fill=255)
    return mask


def ellipse_mask(size: tuple[int, int], bounds: tuple[int, int, int, int]) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).ellipse(bounds, fill=255)
    return mask


def extract(source: Image.Image, mask: Image.Image) -> Image.Image:
    result = Image.new("RGBA", source.size, (0, 0, 0, 0))
    result.paste(source, (0, 0), ImageChops.multiply(source.getchannel("A"), mask))
    return result


def save_scaled(image: Image.Image, path: Path) -> None:
    image.resize(OUTPUT_SIZE, Image.Resampling.LANCZOS).save(path, optimize=True)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def composite_frames(frames: list[Image.Image], path: Path) -> None:
    atlas = Image.new(
        "RGBA",
        (OUTPUT_SIZE[0] * len(frames), OUTPUT_SIZE[1]),
        (0, 0, 0, 0),
    )
    for index, frame in enumerate(frames):
        atlas.alpha_composite(
            frame.resize(OUTPUT_SIZE, Image.Resampling.LANCZOS),
            (index * OUTPUT_SIZE[0], 0),
        )
    atlas.save(path, optimize=True)


def rotate_about(image: Image.Image, degrees: float, pivot: tuple[int, int]) -> Image.Image:
    return image.rotate(
        degrees,
        resample=Image.Resampling.BICUBIC,
        center=pivot,
        expand=False,
    )


def main() -> None:
    if not MASTER_PATH.is_file():
        raise SystemExit(f"Missing approved ship master: {MASTER_PATH}")

    source = Image.open(MASTER_PATH).convert("RGBA")
    size = source.size
    if size != (1535, 1024):
        raise SystemExit(f"Unexpected ship master dimensions: {size}")

    engine_mask = polygon_mask(size, [[
        (58, 525), (244, 520), (276, 722), (75, 775),
    ]])
    landing_mask = polygon_mask(size, [
        [(165, 676), (617, 628), (623, 881), (156, 881)],
        [(1018, 670), (1488, 650), (1507, 890), (1013, 889)],
        [(476, 713), (651, 708), (654, 858), (467, 858)],
        [(1054, 704), (1194, 700), (1199, 851), (1047, 851)],
    ])
    door_mask = ellipse_mask(size, (724, 430, 1012, 719))
    cockpit_mask = polygon_mask(size, [[
        (247, 349), (572, 344), (589, 604), (238, 605),
    ]])
    cosmetic_mask = polygon_mask(size, [[
        (524, 78), (695, 75), (698, 329), (520, 329),
    ]])

    named_masks = {
        "PlayerShipEngine.png": engine_mask,
        "PlayerShipLandingGear.png": landing_mask,
        "PlayerShipDoorClosed.png": door_mask,
        "PlayerShipCockpitSeat.png": cockpit_mask,
        "PlayerShipCosmeticFlag.png": cosmetic_mask,
    }
    combined = Image.new("L", size, 0)
    for mask in named_masks.values():
        combined = ImageChops.lighter(combined, mask)

    hull_alpha = ImageChops.subtract(source.getchannel("A"), combined)
    hull = source.copy()
    hull.putalpha(hull_alpha)
    save_scaled(hull, SHIP_ROOT / "PlayerShipHull.png")

    extracted: dict[str, Image.Image] = {}
    for filename, mask in named_masks.items():
        layer = extract(source, mask)
        extracted[filename] = layer
        save_scaled(layer, SHIP_ROOT / filename)

    engine = extracted["PlayerShipEngine.png"]
    engine_frames = []
    for brightness, alpha in ((0.76, 0.72), (1.0, 0.88), (1.22, 1.0), (0.92, 0.82)):
        frame = ImageEnhance.Brightness(engine).enhance(brightness)
        channel = frame.getchannel("A").point(lambda value, a=alpha: int(value * a))
        frame.putalpha(channel)
        engine_frames.append(frame)
    composite_frames(engine_frames, SHIP_ROOT / "PlayerShipEngineAtlas.png")

    landing = extracted["PlayerShipLandingGear.png"]
    landing_frames = []
    for offset_y in (-92, -42, 0):
        frame = Image.new("RGBA", size, (0, 0, 0, 0))
        frame.alpha_composite(landing, (0, offset_y))
        landing_frames.append(frame)
    composite_frames(landing_frames, SHIP_ROOT / "PlayerShipLandingAtlas.png")

    door = extracted["PlayerShipDoorClosed.png"]
    door_frames = [
        door,
        rotate_about(door, -22.5, (730, 575)),
        rotate_about(door, -45.0, (730, 575)),
    ]
    composite_frames(door_frames, SHIP_ROOT / "PlayerShipDoorAtlas.png")
    save_scaled(door_frames[-1], SHIP_ROOT / "PlayerShipDoorOpen.png")

    damage = Image.new("RGBA", size, (0, 0, 0, 0))
    damage_draw = ImageDraw.Draw(damage)
    for points in (
        [(955, 405), (1001, 432), (971, 463), (1025, 492)],
        [(1060, 535), (1117, 507), (1094, 563), (1153, 586)],
        [(650, 674), (711, 641), (697, 698), (759, 716)],
    ):
        damage_draw.line(points, fill=(82, 25, 22, 205), width=12, joint="curve")
        damage_draw.line(points, fill=(244, 116, 62, 180), width=4, joint="curve")
    save_scaled(damage, SHIP_ROOT / "PlayerShipDamageOverlay.png")

    prediction = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    prediction_draw = ImageDraw.Draw(prediction)
    prediction_draw.ellipse((5, 5, 26, 26), fill=(66, 226, 255, 92))
    prediction_draw.ellipse((9, 9, 22, 22), fill=(171, 247, 255, 220))
    prediction.save(SHIP_ROOT / "FlightPredictionPoint.png", optimize=True)

    hud = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    hud_draw = ImageDraw.Draw(hud)
    hud_draw.ellipse((5, 5, 122, 122), fill=(10, 28, 62, 126),
                     outline=(123, 225, 255, 230), width=4)
    hud_draw.ellipse((18, 18, 109, 109), outline=(105, 153, 255, 155), width=2)
    hud.save(SHIP_ROOT / "FlightHudControl.png", optimize=True)

    generated = [
        "PlayerShipHull.png",
        *named_masks.keys(),
        "PlayerShipDoorOpen.png",
        "PlayerShipDamageOverlay.png",
        "PlayerShipEngineAtlas.png",
        "PlayerShipLandingAtlas.png",
        "PlayerShipDoorAtlas.png",
        "FlightPredictionPoint.png",
        "FlightHudControl.png",
    ]
    manifest = {
        "schemaVersion": 1,
        "source": "PlayerShipMaster.png",
        "sourceSha256": sha256(MASTER_PATH),
        "pixelsPerUnit": 256,
        "canvas": list(OUTPUT_SIZE),
        "visualContract": {
            "construction": "patched-child-built-observatory-ship",
            "signalTechnology": "precise-cyan-emission-nodes",
            "shipping3DDependency": False,
        },
        "layers": {
            "hull": {"file": "PlayerShipHull.png", "pivot": [0.50, 0.34]},
            "engine": {"file": "PlayerShipEngineAtlas.png", "pivot": [0.105, 0.385], "frames": 4},
            "landing": {"file": "PlayerShipLandingAtlas.png", "pivot": [0.50, 0.145], "frames": 3},
            "door": {"file": "PlayerShipDoorAtlas.png", "pivot": [0.566, 0.444], "frames": 3},
            "cockpitSeat": {"file": "PlayerShipCockpitSeat.png", "pivot": [0.270, 0.515]},
            "damage": {"file": "PlayerShipDamageOverlay.png", "pivot": [0.680, 0.490]},
            "cosmetic": {"file": "PlayerShipCosmeticFlag.png", "pivot": [0.397, 0.775]},
        },
        "outputs": {},
    }
    for filename in generated:
        path = SHIP_ROOT / filename
        manifest["outputs"][filename] = {
            "sha256": sha256(path),
            "bytes": path.stat().st_size,
        }
    (SHIP_ROOT / "PlayerShipLayers.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
