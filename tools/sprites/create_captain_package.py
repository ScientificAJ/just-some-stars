#!/usr/bin/env python3
"""Create and validate the deterministic modular Captain sprite package."""

import argparse
import hashlib
import json
import math
import os
import shutil
import uuid
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps


MASTER_SHA256 = "41425b612a0897fb7ea0dcc9c3bbb7fb9a9aa10ac5424a5b898901bc0c95c54e"
MASTER_SIZE = (1672, 941)
AUTHORITY_HASHES = {
    "gameplay-target": {
        "path": "outputs/just-some-stars-2.5d-gameplay-target-v1.png",
        "sha256": "72644970448effd81177222e0aa23ae8a23f9b733077dab6e27e9ca765f5eaed",
    },
    "master-style-sheet": {
        "path": "Assets/_JustSomeStars/Art/Characters/References/master-style-sheet.png",
        "sha256": "bdcdf2e36e23d49b9c15f9734d037930151606e765da74d28a1381694060f7c5",
    },
    "crew-height-lineup": {
        "path": "Assets/_JustSomeStars/Art/Characters/References/crew-height-lineup.png",
        "sha256": "63d15525a438c0cb8e5bac9e56034c1138a37a048a8e6d49ea910f46a8049af0",
    },
    "captain-body-families": {
        "path": "Assets/_JustSomeStars/Art/Characters/References/captain-body-families.png",
        "sha256": "fb9b31bfc11db5f22f64140ba6663bc4561c0e6d0c9c5855474493547bb16aae",
    },
    "captain-customization": {
        "path": "Assets/_JustSomeStars/Art/Characters/References/captain-customization.png",
        "sha256": "07caff03080d85739618a5ddf3825a0b435f4ca2e200b465e7991b7c5d81a0b5",
    },
    "material-callouts": {
        "path": "Assets/_JustSomeStars/Art/Characters/References/material-callouts.png",
        "sha256": "6e5eb46686269eaedae28395d5ef507c965a389d310987812c89c2fcfa9be677",
    },
    "equipment": {
        "path": "Assets/_JustSomeStars/Art/Characters/References/equipment.png",
        "sha256": "61a21a44b888d202ac51d28834334671f20b40a041d5fc6ca055ede3682442ef",
    },
    "production-family-master": {
        "path": "Assets/_JustSomeStars/Art/2D/Characters/Captain/Source/References/captain-right-facing-family-master-v2.png",
        "sha256": MASTER_SHA256,
    },
}
FAMILIES = {
    "Compact": {
        "heightMeters": 1.46,
        "heightPixels": 146,
        "silhouetteWidthScale": 1.0,
    },
    "Average": {
        "heightMeters": 1.56,
        "heightPixels": 156,
        "silhouetteWidthScale": 1.0,
    },
    "TallBroad": {
        "heightMeters": 1.66,
        "heightPixels": 166,
        "silhouetteWidthScale": 1.0,
    },
}
LAYERS = [
    "body-base",
    "head-hair",
    "silhouette-costume",
    "backpack-equipment",
    "foreground-hand-tool",
]
ANCHORS = [
    "Root",
    "LeftFoot",
    "RightFoot",
    "LeftHand",
    "RightHand",
    "HelmetRing",
    "BackpackSocket",
    "Belt",
    "LeftWrist",
    "RightWrist",
    "LeftBootTop",
    "RightBootTop",
    "ActiveTool",
    "StowedTool",
]
CLIPS = [
    {
        "id": "idle",
        "frameCount": 4,
        "loopMode": "Loop",
        "contacts": [["LeftFoot", "RightFoot"]] * 4,
        "events": {},
    },
    {
        "id": "run",
        "frameCount": 8,
        "loopMode": "Loop",
        "contacts": [
            ["LeftFoot"], [], ["RightFoot"], [],
            ["LeftFoot"], [], ["RightFoot"], [],
        ],
        "events": {
            "0": ["FootContact:step-left"],
            "2": ["FootContact:step-right"],
            "4": ["FootContact:step-left"],
            "6": ["FootContact:step-right"],
        },
    },
    {
        "id": "turn",
        "frameCount": 4,
        "loopMode": "Once",
        "contacts": [
            ["LeftFoot", "RightFoot"],
            ["RightFoot"],
            ["RightFoot"],
            ["LeftFoot", "RightFoot"],
        ],
        "events": {},
    },
    {
        "id": "jump",
        "frameCount": 6,
        "loopMode": "HoldLast",
        "contacts": [
            ["LeftFoot", "RightFoot"],
            ["LeftFoot", "RightFoot"],
            [], [], [], [],
        ],
        "events": {
            "1": ["Audio:jump-audio", "Vfx:jump-vfx"],
        },
    },
    {
        "id": "land",
        "frameCount": 4,
        "loopMode": "Once",
        "contacts": [[], [], ["LeftFoot", "RightFoot"], ["LeftFoot", "RightFoot"]],
        "events": {
            "2": [
                "FootContact:land-contact",
                "Audio:land-audio",
                "Vfx:land-vfx",
            ],
        },
    },
    {
        "id": "climb",
        "frameCount": 8,
        "loopMode": "Loop",
        "contacts": [
            ["LeftHand", "RightFoot"],
            ["RightHand", "RightFoot"],
            ["RightHand", "RightFoot"],
            ["RightHand", "RightFoot"],
            ["RightHand", "LeftFoot"],
            ["LeftHand", "LeftFoot"],
            ["LeftHand", "LeftFoot"],
            ["LeftHand", "LeftFoot"],
        ],
        "events": {
            "0": ["FootContact:step-right"],
            "4": ["FootContact:step-left"],
        },
    },
    {
        "id": "scan",
        "frameCount": 8,
        "loopMode": "Once",
        "contacts": [["LeftFoot", "RightFoot"]] * 8,
        "events": {
            "1": ["ToolAttach:scan-tool-attach"],
            "4": [
                "Interaction:scan-commit",
                "Audio:scan-audio",
                "Vfx:scan-vfx",
            ],
            "6": ["ToolDetach:scan-tool-detach"],
        },
    },
    {
        "id": "interact",
        "frameCount": 6,
        "loopMode": "Once",
        "contacts": [["LeftFoot", "RightFoot"]] * 6,
        "events": {"3": ["Interaction:interact-commit"]},
    },
]
CATALOG = {
    "facePresets": [f"face-{index}" for index in range(1, 7)],
    "skinSwatches": [f"skin-{index}" for index in range(1, 9)],
    "eyeShapes": [f"eye-shape-{index}" for index in range(1, 7)],
    "irisColors": [
        "warm-brown", "amber", "hazel", "river-blue", "deep-blue", "slate",
    ],
    "hairShapes": [f"hair-shape-{index}" for index in range(1, 9)],
    "hairColors": [
        "black", "deep-brown", "chestnut", "copper", "auburn",
        "golden-blonde", "ash-blonde", "silver", "blue-black",
    ],
    "suitComponents": [
        "base-shirt", "canvas-oversuit", "scarf-neck-layer", "utility-belt",
    ],
    "suitColorways": [
        "amber-clay", "deep-teal", "dusk-purple",
        "river-blue", "moss-green", "sandstone",
    ],
    "patches": [f"patch-{index}" for index in range(1, 7)],
    "accessories": [
        "goggles", "hair-clips", "headband", "wrist-device", "utility-pouch",
    ],
    "gloves": ["wrapped-work", "padded-utility", "tactile-grip"],
    "boots": ["laceup-work", "strap-utility", "pull-on"],
    "helmets": ["explorer-lite", "surveyor", "field-ready"],
    "backpacks": ["daypack", "expedition-pack", "field-pack"],
    "signalStates": ["dormant", "active-cyan", "resonance-violet"],
}
PALETTE_CATEGORIES = {
    "skinSwatches": "skin",
    "hairColors": "hair",
    "suitColorways": "suit",
    "signalStates": "Signal",
}
MODULE_CATEGORIES = {
    "facePresets": "head-hair",
    "eyeShapes": "head-hair",
    "irisColors": "head-hair",
    "hairShapes": "head-hair",
    "suitComponents": "silhouette-costume",
    "patches": "silhouette-costume",
    "accessories": "silhouette-costume",
    "gloves": "silhouette-costume",
    "boots": "silhouette-costume",
    "helmets": "head-hair",
    "backpacks": "backpack-equipment",
}
MODULE_OPTION_COLUMNS = 4
MODULE_OPTION_ROWS = 2
MODULE_ATLAS_SIZE = (512, 768)
MODULE_PAGE_SIZE = (2048, 1536)


def build_contract():
    return {
        "schemaVersion": 1,
        "cadenceFps": 12,
        "runtimeCellPixels": [128, 192],
        "sourceCellPixels": [256, 384],
        "pixelsPerUnit": 100,
        "families": FAMILIES,
        "facings": ["right", "left"],
        "layers": LAYERS,
        "anchors": ANCHORS,
        "clips": CLIPS,
        "catalog": CATALOG,
        "paletteCategories": PALETTE_CATEGORIES,
        "moduleCategories": MODULE_CATEGORIES,
        "compositeClipCount": len(FAMILIES) * 2 * len(CLIPS),
        "compositeFrameCount": len(FAMILIES) * 2 * sum(
            clip["frameCount"] for clip in CLIPS
        ),
        "proofSourceRowCount": len(FAMILIES) * 2 * len(LAYERS) * len(CLIPS),
        "publicationCount": len(FAMILIES) * 2 * len(LAYERS),
    }


def preflight_master(master_path):
    master_path = Path(master_path).resolve()
    payload = master_path.read_bytes()
    digest = hashlib.sha256(payload).hexdigest()
    if digest != MASTER_SHA256:
        raise ValueError(
            f"Captain master hash mismatch: expected {MASTER_SHA256}, got {digest}."
        )
    with Image.open(master_path) as opened:
        image = opened.convert("RGBA")
    if image.size != MASTER_SIZE:
        raise ValueError(
            f"Captain master size mismatch: expected {MASTER_SIZE}, got {image.size}."
        )
    alpha = image.getchannel("A")
    corners = [
        alpha.getpixel((0, 0)),
        alpha.getpixel((image.width - 1, 0)),
        alpha.getpixel((0, image.height - 1)),
        alpha.getpixel((image.width - 1, image.height - 1)),
    ]
    if any(value > 8 for value in corners):
        raise ValueError("Captain master corners must be transparent.")
    runs = _visible_column_runs(alpha, threshold=8, minimum_visible_pixels=6)
    if len(runs) != 3:
        raise ValueError(
            f"Captain master must contain three isolated families; found {len(runs)}."
        )
    families = []
    prior_right = -1
    for family_id, (left, right) in zip(FAMILIES, runs):
        bounds = _alpha_bounds(alpha, left, right, threshold=8)
        if bounds[0] <= prior_right:
            raise ValueError("Captain master family silhouettes overlap.")
        prior_right = bounds[2]
        families.append(
            {
                "id": family_id,
                "alphaBoundsPixels": list(bounds),
                "isolated": True,
                "facesRight": True,
            }
        )
    heights = [item["alphaBoundsPixels"][3] - item["alphaBoundsPixels"][1]
               for item in families]
    if not heights[0] < heights[1] < heights[2]:
        raise ValueError("Captain master family heights must increase A to B to C.")
    return {
        "schemaVersion": 1,
        "masterPath": master_path.as_posix(),
        "masterSha256": digest,
        "sourceSize": list(image.size),
        "familyCount": len(families),
        "transparentCorners": True,
        "families": families,
    }


def _visible_column_runs(alpha, threshold, minimum_visible_pixels):
    visible_columns = []
    for x in range(alpha.width):
        count = sum(
            1 for y in range(alpha.height)
            if alpha.getpixel((x, y)) > threshold
        )
        if count >= minimum_visible_pixels:
            visible_columns.append(x)
    if not visible_columns:
        return []
    runs = []
    start = previous = visible_columns[0]
    for x in visible_columns[1:]:
        if x - previous > 12:
            runs.append((start, previous + 1))
            start = x
        previous = x
    runs.append((start, previous + 1))
    return runs


def _alpha_bounds(alpha, left, right, threshold):
    points = [
        (x, y)
        for x in range(left, right)
        for y in range(alpha.height)
        if alpha.getpixel((x, y)) > threshold
    ]
    if not points:
        raise ValueError("Captain family isolation produced an empty silhouette.")
    return (
        min(x for x, _ in points),
        min(y for _, y in points),
        max(x for x, _ in points) + 1,
        max(y for _, y in points) + 1,
    )


def _sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _save_png(image, path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=True)


def _mask_polygon(size, points, blur_radius=0.85):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    if blur_radius:
        mask = mask.filter(ImageFilter.GaussianBlur(blur_radius))
    return mask


def _masked(image, mask):
    result = image.copy()
    result.putalpha(ImageChops.multiply(image.getchannel("A"), mask))
    return result


def _subtract_masks(alpha, *masks):
    combined = Image.new("L", alpha.size, 0)
    for mask in masks:
        combined = ImageChops.lighter(combined, mask)
    return ImageChops.multiply(alpha, ImageOps.invert(combined))


def _translate(image, dx, dy):
    return image.transform(
        image.size,
        Image.Transform.AFFINE,
        (1, 0, -dx, 0, 1, -dy),
        resample=Image.Resampling.BICUBIC,
    )


def _scale_about(image, scale_x, scale_y, pivot):
    if abs(scale_x - 1.0) < 1e-6 and abs(scale_y - 1.0) < 1e-6:
        return image
    width = max(1, round(image.width * scale_x))
    height = max(1, round(image.height * scale_y))
    resized = image.resize((width, height), Image.Resampling.BICUBIC)
    left = round(pivot[0] - pivot[0] * scale_x)
    top = round(pivot[1] - pivot[1] * scale_y)
    canvas = Image.new("RGBA", image.size, (0, 0, 0, 0))
    canvas.alpha_composite(resized, (left, top))
    return canvas


def _transform(image, angle=0.0, pivot=None, dx=0, dy=0, scale=(1.0, 1.0)):
    pivot = pivot or (image.width / 2, image.height / 2)
    result = _scale_about(image, scale[0], scale[1], pivot)
    if abs(angle) > 1e-6:
        result = result.rotate(
            angle,
            resample=Image.Resampling.BICUBIC,
            center=pivot,
        )
    if dx or dy:
        result = _translate(result, dx, dy)
    return result


def _motion_state(clip_id, frame_index, frame_count):
    phase = frame_index / max(1, frame_count)
    cycle = math.tau * phase
    state = {
        "bodyAngle": 0.0,
        "headAngle": 0.0,
        "rearLegAngle": 0.0,
        "frontLegAngle": 0.0,
        "rearLegDx": 0,
        "rearLegDy": 0,
        "frontLegDx": 0,
        "frontLegDy": 0,
        "rearArmAngle": 0.0,
        "armAngle": 0.0,
        "dx": 0,
        "dy": 0,
        "scaleX": 1.0,
        "scaleY": 1.0,
        "upperDy": 0,
        "tool": False,
        "signal": False,
    }
    if clip_id == "idle":
        state.update(dy=round(-1.5 + 1.5 * math.cos(cycle)),
                     headAngle=0.7 * math.sin(cycle),
                     armAngle=-1.5 * math.sin(cycle))
    elif clip_id == "run":
        # Contact frames need the widest stride. The previous sine started both
        # contact frames at the neutral pose, collapsing the two legs into one
        # mobile-scale silhouette. A cosine starts on contact, while the small
        # passing-pose lift keeps the swing boot readable at quarter-cycle.
        swing = math.cos(cycle)
        rear_lift = [0, -4, -16, -8, 0, 0, 0, 0][frame_index]
        front_lift = [0, 0, 0, 0, 0, -4, -16, -8][frame_index]
        rear_passing_offset = [0, 0, -28, 10, 0, 0, 0, 0][frame_index]
        front_passing_offset = [0, 0, 0, 0, 0, -8, 28, 10][frame_index]
        state.update(
            bodyAngle=-3.5,
            dy=round(-4.0 * abs(math.sin(cycle))),
            rearLegAngle=-30.0 * swing,
            frontLegAngle=30.0 * swing,
            rearLegDx=rear_passing_offset,
            rearLegDy=rear_lift,
            frontLegDx=front_passing_offset,
            frontLegDy=front_lift,
            rearArmAngle=24.0 * swing,
            armAngle=-24.0 * swing,
            headAngle=1.2 * math.sin(cycle + math.pi / 2),
        )
    elif clip_id == "turn":
        state.update(
            bodyAngle=[-1.0, -2.0, 2.0, 1.0][frame_index],
            headAngle=[-2.0, -4.0, 4.0, 2.0][frame_index],
        )
    elif clip_id == "jump":
        rises = [0, -2, -14, -18, -25, -30]
        state.update(
            dy=rises[frame_index],
            dx=-18,
            bodyAngle=[3, -2, -5, -7, -5, -3][frame_index],
            headAngle=[-2, -1, 1, 2, 1, 0][frame_index],
            rearLegAngle=[20, 12, -8, -18, -8, 0][frame_index],
            frontLegAngle=[-20, -12, 10, 18, 8, 0][frame_index],
            rearArmAngle=[-18, -34, -48, -54, -40, -20][frame_index],
            armAngle=[22, 38, 52, 56, 42, 22][frame_index],
            upperDy=[20, 4, 0, 0, 0, 0][frame_index],
        )
    elif clip_id == "land":
        state.update(
            dy=[-31, -10, 0, 0][frame_index],
            dx=-14,
            bodyAngle=[-4, 1, 7, 0][frame_index],
            headAngle=[1, 0, -3, 0][frame_index],
            rearLegAngle=[-12, -4, 22, 0][frame_index],
            frontLegAngle=[12, 4, -22, 0][frame_index],
            rearArmAngle=[-38, -22, 24, 0][frame_index],
            armAngle=[42, 26, -20, 0][frame_index],
            upperDy=[0, 0, 22, 0][frame_index],
        )
    elif clip_id == "climb":
        state.update(
            bodyAngle=[-7, -9, -11, -9, -7, -9, -11, -9][frame_index],
            headAngle=[-3, -5, -7, -5, -3, -5, -7, -5][frame_index],
            rearArmAngle=[90, 120, 150, 170, 150, 125, 100, 90][frame_index],
            armAngle=[150, 132, 112, 96, 110, 130, 148, 158][frame_index],
            rearLegAngle=[15, 5, -8, -18, -10, 2, 12, 18][frame_index],
            frontLegAngle=[-30, -18, -2, 17, 31, 14, -9, -28][frame_index],
            dx=[-28, -30, -32, -30, -28, -30, -32, -30][frame_index],
            dy=[0, -4, -8, -5, 0, -4, -8, -5][frame_index],
        )
    elif clip_id == "scan":
        reach = [0, -12, -24, -32, -34, -30, -14, 0][frame_index]
        state.update(
            armAngle=reach,
            bodyAngle=-1.5 if 2 <= frame_index <= 5 else 0.0,
            tool=1 <= frame_index <= 6,
            signal=frame_index in (3, 4, 5),
        )
    elif clip_id == "interact":
        reach = [0, -10, -24, -38, -25, 0][frame_index]
        state.update(
            armAngle=reach,
            bodyAngle=[0, -1, -2, -3, -2, 0][frame_index],
            tool=2 <= frame_index <= 4,
            signal=frame_index == 3,
        )
    return state


def _prepare_family(master, preflight, family_id):
    family = next(
        item for item in preflight["families"] if item["id"] == family_id
    )
    crop = master.crop(tuple(family["alphaBoundsPixels"]))
    target_height = FAMILIES[family_id]["heightPixels"] * 2
    target_width = max(1, round(
        crop.width * target_height / crop.height *
        FAMILIES[family_id]["silhouetteWidthScale"]
    ))
    crop = crop.resize((target_width, target_height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (256, 384), (0, 0, 0, 0))
    left = (canvas.width - target_width) // 2
    top = canvas.height - 18 - target_height
    canvas.alpha_composite(crop, (left, top))
    return canvas, (left, top, left + target_width, top + target_height)


def _prepare_puppet(sprite, bounds):
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    alpha = sprite.getchannel("A")

    head_mask = _mask_polygon(
        sprite.size,
        [
            (left - 4, top - 4), (right + 4, top - 4),
            (right + 2, top + 0.30 * height),
            (left + 0.28 * width, top + 0.31 * height),
            (left - 4, top + 0.27 * height),
        ],
    )
    pack_mask = _mask_polygon(
        sprite.size,
        [
            (left - 4, top + 0.22 * height),
            (left + 0.43 * width, top + 0.23 * height),
            (left + 0.45 * width, top + 0.58 * height),
            (left - 4, top + 0.62 * height),
        ],
    )
    arm_mask = _mask_polygon(
        sprite.size,
        [
            (left + 0.25 * width, top + 0.28 * height),
            (left + 0.63 * width, top + 0.29 * height),
            (left + 0.67 * width, top + 0.66 * height),
            (left + 0.34 * width, top + 0.71 * height),
            (left + 0.27 * width, top + 0.51 * height),
        ],
    )
    rear_leg_mask = _mask_polygon(
        sprite.size,
        [
            (left - 2, top + 0.50 * height),
            (left + 0.56 * width, top + 0.50 * height),
            (left + 0.60 * width, bottom + 3),
            (left - 2, bottom + 3),
        ],
    )
    front_leg_mask = _mask_polygon(
        sprite.size,
        [
            (left + 0.45 * width, top + 0.49 * height),
            (right + 3, top + 0.49 * height),
            (right + 3, bottom + 3),
            (left + 0.48 * width, bottom + 3),
        ],
    )
    head = _masked(sprite, head_mask)
    pack = _masked(sprite, ImageChops.subtract(pack_mask, head_mask))
    arm = _masked(
        sprite,
        ImageChops.subtract(ImageChops.subtract(arm_mask, head_mask), pack_mask),
    )
    rear_leg = _masked(sprite, rear_leg_mask)
    front_leg = _masked(sprite, front_leg_mask)
    torso_alpha = _subtract_masks(
        alpha, head_mask, pack_mask, arm_mask, rear_leg_mask, front_leg_mask
    )
    torso = sprite.copy()
    torso.putalpha(torso_alpha)
    return {
        "full": sprite.copy(),
        "torso": torso,
        "head": head,
        "pack": pack,
        "arm": arm,
        "rearLeg": rear_leg,
        "frontLeg": front_leg,
        "bounds": bounds,
    }


def _costume_overlay(size, bounds, signal=False):
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    belt_y = top + 0.43 * height
    draw.line(
        (
            left + 0.34 * width, belt_y,
            right - 0.13 * width, belt_y - 0.006 * height,
        ),
        fill=(235, 151, 77, 205),
        width=max(2, round(width * 0.018)),
    )
    draw.line(
        (
            left + 0.46 * width, top + 0.275 * height,
            right - 0.17 * width, top + 0.34 * height,
        ),
        fill=(202, 92, 47, 150),
        width=max(2, round(width * 0.014)),
    )
    patch_center = (
        round(left + 0.69 * width),
        round(top + 0.36 * height),
    )
    patch_radius = max(3, round(width * 0.055))
    draw.ellipse(
        (
            patch_center[0] - patch_radius,
            patch_center[1] - patch_radius,
            patch_center[0] + patch_radius,
            patch_center[1] + patch_radius,
        ),
        fill=(20, 28, 39, 225),
        outline=(83, 225, 255, 255) if signal else (232, 145, 66, 235),
        width=2,
    )
    return overlay


def _tool_overlay(size, bounds, signal=False):
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    x = round(right - 0.01 * width)
    y = round(top + 0.63 * height)
    radius = max(5, round(width * 0.09))
    draw.rounded_rectangle(
        (x - radius * 2, y - radius, x + radius, y + radius),
        radius=max(2, radius // 3),
        fill=(25, 35, 48, 245),
        outline=(116, 210, 255, 255),
        width=2,
    )
    draw.ellipse(
        (x - radius, y - radius, x + radius, y + radius),
        fill=(22, 75, 112, 245),
        outline=(181, 240, 255, 255),
        width=2,
    )
    if signal:
        glow = Image.new("RGBA", size, (0, 0, 0, 0))
        glow_draw = ImageDraw.Draw(glow)
        for spread, alpha in ((18, 40), (12, 70), (7, 120)):
            glow_draw.ellipse(
                (x - spread, y - spread, x + spread, y + spread),
                outline=(89, 223, 255, alpha),
                width=3,
            )
        overlay = Image.alpha_composite(glow, overlay)
    return overlay


def _render_frame(puppet, clip_id, frame_index, frame_count):
    state = _motion_state(clip_id, frame_index, frame_count)
    left, top, right, bottom = puppet["bounds"]
    width = right - left
    height = bottom - top
    root = (left + 0.52 * width, bottom)
    hips = (left + 0.52 * width, top + 0.54 * height)
    neck = (left + 0.58 * width, top + 0.28 * height)
    shoulder = (left + 0.59 * width, top + 0.34 * height)

    layers = {name: Image.new("RGBA", (256, 384), (0, 0, 0, 0))
              for name in LAYERS}
    body = _translate(_transform(
        puppet["arm"],
        angle=state["rearArmAngle"],
        pivot=shoulder,
    ), 0, state["upperDy"])
    body = Image.alpha_composite(
        body, _translate(puppet["torso"], 0, state["upperDy"])
    )
    body = Image.alpha_composite(
        body,
        _translate(
            _transform(
                puppet["rearLeg"],
                angle=state["rearLegAngle"],
                pivot=(left + 0.42 * width, hips[1]),
            ),
            state["rearLegDx"],
            state["rearLegDy"],
        ),
    )
    body = Image.alpha_composite(
        body,
        _translate(
            _transform(
                puppet["frontLeg"],
                angle=state["frontLegAngle"],
                pivot=(left + 0.61 * width, hips[1]),
            ),
            state["frontLegDx"],
            state["frontLegDy"],
        ),
    )
    layers["body-base"] = body
    layers["head-hair"] = _translate(
        _transform(puppet["head"], angle=state["headAngle"], pivot=neck),
        0,
        state["upperDy"],
    )
    layers["backpack-equipment"] = _translate(
        puppet["pack"], 0, state["upperDy"]
    )
    foreground = puppet["arm"].copy()
    if state["tool"]:
        foreground = Image.alpha_composite(
            foreground, _tool_overlay(foreground.size, puppet["bounds"], state["signal"])
        )
    layers["foreground-hand-tool"] = _translate(
        _transform(foreground, angle=state["armAngle"], pivot=shoulder),
        0,
        state["upperDy"],
    )
    layers["silhouette-costume"] = _translate(
        _costume_overlay((256, 384), puppet["bounds"], state["signal"]),
        0,
        state["upperDy"],
    )

    for layer_id, layer in tuple(layers.items()):
        layers[layer_id] = _transform(
            layer,
            angle=state["bodyAngle"],
            pivot=root,
            dx=state["dx"],
            dy=state["dy"],
            scale=(state["scaleX"], state["scaleY"]),
        )
        if clip_id == "turn" and frame_index <= 1:
            layers[layer_id] = _apply_left_facing(layers[layer_id])
    return layers


def _rotate_point(point, angle_degrees, pivot):
    radians = math.radians(angle_degrees)
    cosine = math.cos(radians)
    sine = math.sin(radians)
    dx = point[0] - pivot[0]
    dy = point[1] - pivot[1]
    return (
        pivot[0] + cosine * dx + sine * dy,
        pivot[1] - sine * dx + cosine * dy,
    )


def _apply_global_point(point, state, root):
    scaled = (
        root[0] + (point[0] - root[0]) * state["scaleX"],
        root[1] + (point[1] - root[1]) * state["scaleY"],
    )
    rotated = _rotate_point(scaled, state["bodyAngle"], root)
    return (rotated[0] + state["dx"], rotated[1] + state["dy"])


def _frame_anchors(bounds, clip_id, frame_index, frame_count, facing):
    state = _motion_state(clip_id, frame_index, frame_count)
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    root = (left + 0.52 * width, bottom)
    hips = (left + 0.52 * width, top + 0.54 * height)
    neck = (left + 0.58 * width, top + 0.28 * height)
    shoulder = (left + 0.59 * width, top + 0.34 * height)
    rear_foot = _rotate_point(
        (left + 0.31 * width, bottom - 0.01 * height),
        state["rearLegAngle"],
        (left + 0.42 * width, hips[1]),
    )
    rear_boot_top = _rotate_point(
        (left + 0.31 * width, bottom - 0.16 * height),
        state["rearLegAngle"],
        (left + 0.42 * width, hips[1]),
    )
    front_foot = _rotate_point(
        (left + 0.74 * width, bottom - 0.01 * height),
        state["frontLegAngle"],
        (left + 0.61 * width, hips[1]),
    )
    front_boot_top = _rotate_point(
        (left + 0.74 * width, bottom - 0.16 * height),
        state["frontLegAngle"],
        (left + 0.61 * width, hips[1]),
    )
    rear_foot = (
        rear_foot[0] + state["rearLegDx"],
        rear_foot[1] + state["rearLegDy"],
    )
    rear_boot_top = (
        rear_boot_top[0] + state["rearLegDx"],
        rear_boot_top[1] + state["rearLegDy"],
    )
    front_foot = (
        front_foot[0] + state["frontLegDx"],
        front_foot[1] + state["frontLegDy"],
    )
    front_boot_top = (
        front_boot_top[0] + state["frontLegDx"],
        front_boot_top[1] + state["frontLegDy"],
    )
    rear_hand = _rotate_point(
        (left + 0.42 * width, top + 0.66 * height),
        state["rearArmAngle"],
        shoulder,
    )
    front_hand = _rotate_point(
        (right - 0.12 * width, top + 0.63 * height),
        state["armAngle"],
        shoulder,
    )
    rear_hand = (rear_hand[0], rear_hand[1] + state["upperDy"])
    front_hand = (front_hand[0], front_hand[1] + state["upperDy"])
    upper_dy = state["upperDy"]
    points = {
        "Root": root,
        "LeftFoot": rear_foot,
        "RightFoot": front_foot,
        "LeftHand": rear_hand,
        "RightHand": front_hand,
        "HelmetRing": (left + 0.55 * width, top + 0.06 * height + upper_dy),
        "BackpackSocket": (
            left + 0.18 * width, top + 0.40 * height + upper_dy
        ),
        "Belt": (left + 0.55 * width, top + 0.43 * height + upper_dy),
        "LeftWrist": rear_hand,
        "RightWrist": front_hand,
        "LeftBootTop": rear_boot_top,
        "RightBootTop": front_boot_top,
        "ActiveTool": (front_hand[0] + 0.03 * width, front_hand[1]),
        "StowedTool": (
            left + 0.22 * width, top + 0.52 * height + upper_dy
        ),
    }
    result = []
    for anchor_id in ANCHORS:
        x, y = _apply_global_point(points[anchor_id], state, root)
        if anchor_id == "Root":
            y = min(y, 384.0)
        if clip_id == "turn" and frame_index <= 1:
            x = 256 - x
        if facing == "left":
            x = 256 - x
        result.append(
            {
                "id": anchor_id,
                "sourcePixels": [round(x, 4), round(384 - y, 4)],
                "runtimePixels": [round(x * 0.5, 4), round((384 - y) * 0.5, 4)],
            }
        )
    return result


def _frame_contacts(clip, frame_index, facing):
    if clip["id"] == "turn" and frame_index in (1, 2):
        return ["RightFoot" if facing == "right" else "LeftFoot"]
    return clip["contacts"][frame_index]


def _composite_layers(layers):
    composite = Image.new("RGBA", (256, 384), (0, 0, 0, 0))
    for layer_id in LAYERS:
        composite = Image.alpha_composite(composite, layers[layer_id])
    return composite


def _apply_left_facing(layer):
    mirrored = ImageOps.mirror(layer)
    alpha = mirrored.getchannel("A")
    tint = Image.new("RGBA", mirrored.size, (0, 0, 0, 0))
    tint_pixels = tint.load()
    for x in range(mirrored.width):
        position = x / max(1, mirrored.width - 1)
        if position < 0.5:
            mix = 1.0 - position * 2.0
            color = (255, 137, 61, round(20 * mix))
        else:
            mix = position * 2.0 - 1.0
            color = (71, 157, 255, round(18 * mix))
        for y in range(mirrored.height):
            tint_pixels[x, y] = color
    tint.putalpha(ImageChops.multiply(tint.getchannel("A"), alpha))
    return Image.alpha_composite(mirrored, tint)


def _frame_alpha_bounds(frame):
    bounds = frame.getchannel("A").getbbox()
    return list(bounds) if bounds else [0, 0, 0, 0]


def _frame_events(clip, frame_index):
    values = clip["events"].get(str(frame_index), [])
    result = []
    for value in values:
        kind, event_id = value.split(":", 1)
        result.append({"id": event_id, "kind": kind})
    return result


def _palette_mask_row(source_row, layer_id, frame_count, bounds):
    red = Image.new("L", source_row.size, 0)
    green = Image.new("L", source_row.size, 0)
    blue = Image.new("L", source_row.size, 0)
    signal = Image.new("L", source_row.size, 0)
    _, top, _, bottom = bounds
    height = bottom - top
    for frame_index in range(frame_count):
        left = frame_index * 256
        frame = source_row.crop((left, 0, left + 256, 384))
        alpha = frame.getchannel("A")
        if layer_id == "body-base":
            blue.paste(alpha, (left, 0))
        elif layer_id == "head-hair":
            hair_region = Image.new("L", frame.size, 0)
            ImageDraw.Draw(hair_region).rectangle(
                (0, 0, 255, round(top + 0.17 * height)), fill=255
            )
            hair = ImageChops.multiply(alpha, hair_region)
            skin = ImageChops.subtract(alpha, hair)
            red.paste(skin, (left, 0))
            green.paste(hair, (left, 0))
        elif layer_id == "silhouette-costume":
            blue.paste(alpha, (left, 0))
            signal_region = Image.new("L", frame.size, 0)
            ImageDraw.Draw(signal_region).ellipse(
                (
                    bounds[0] + 0.55 * (bounds[2] - bounds[0]),
                    top + 0.31 * height,
                    bounds[2],
                    top + 0.45 * height,
                ),
                fill=255,
            )
            signal_frame = ImageChops.multiply(alpha, signal_region)
            signal.paste(signal_frame, (left, 0))
            blue.paste(ImageChops.subtract(alpha, signal_frame), (left, 0))
        elif layer_id == "foreground-hand-tool":
            r, g, b, _ = frame.split()
            cyan = ImageChops.multiply(
                ImageChops.subtract(b, r).point(
                    lambda value: 255 if value > 38 else 0
                ),
                ImageChops.subtract(g, r).point(
                    lambda value: 255 if value > 16 else 0
                ),
            )
            cyan = ImageChops.multiply(cyan, alpha)
            red.paste(ImageChops.subtract(alpha, cyan), (left, 0))
            signal.paste(cyan, (left, 0))
    return Image.merge("RGBA", (red, green, blue, signal))


def _runtime_atlas(rows):
    atlas = Image.new("RGBA", (1024, 1536), (0, 0, 0, 0))
    for clip_index, row in enumerate(rows):
        atlas.paste(row, (0, clip_index * 192))
    return atlas


def _resize_palette_mask(mask, size):
    return Image.merge(
        "RGBA",
        tuple(
            channel.resize(size, Image.Resampling.LANCZOS)
            for channel in mask.convert("RGBA").split()
        ),
    )


def _anchor_lookup(bounds, clip_id, frame_index, frame_count, facing):
    return {
        value["id"]: (
            value["sourcePixels"][0],
            384.0 - value["sourcePixels"][1],
        )
        for value in _frame_anchors(
            bounds, clip_id, frame_index, frame_count, facing
        )
    }


def _module_color(category, option_index):
    palettes = {
        "facePresets": (230, 168, 115),
        "eyeShapes": (35, 27, 24),
        "irisColors": (75, 169, 220),
        "hairShapes": (68, 38, 23),
        "suitComponents": (224, 121, 54),
        "patches": (85, 222, 255),
        "accessories": (173, 108, 241),
        "gloves": (42, 56, 74),
        "boots": (80, 52, 34),
        "helmets": (102, 71, 45),
        "backpacks": (178, 103, 47),
    }
    base = palettes[category]
    shift = (option_index * 31) % 71 - 35
    return tuple(max(18, min(245, channel + shift)) for channel in base)


def _draw_ring(draw, center, radii, color, width=4):
    x, y = center
    rx, ry = radii
    draw.ellipse(
        (x - rx, y - ry, x + rx, y + ry),
        outline=color,
        width=width,
    )


def _module_frame(
        category, option_id, option_index, layers, bounds, clip_id,
        frame_index, frame_count, facing):
    image = Image.new("RGBA", (256, 384), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    anchors = _anchor_lookup(
        bounds, clip_id, frame_index, frame_count, facing
    )
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    color = _module_color(category, option_index) + (235,)
    highlight = tuple(min(255, value + 48) for value in color[:3]) + (245,)
    dark = tuple(max(0, value - 45) for value in color[:3]) + (245,)
    helmet = anchors["HelmetRing"]
    belt = anchors["Belt"]
    sign = 1 if facing == "right" else -1

    if category == "facePresets":
        center = (helmet[0] + sign * width * 0.075, helmet[1] + height * 0.105)
        brow = max(5, round(width * (0.08 + 0.01 * option_index)))
        draw.arc(
            (center[0] - brow, center[1] - 4, center[0] + brow, center[1] + 7),
            195 if sign > 0 else 165,
            335 if sign > 0 else 305,
            fill=dark,
            width=3,
        )
        if option_index % 3 == 1:
            draw.ellipse(
                (center[0] - 3, center[1] + 10, center[0] + 3, center[1] + 15),
                fill=(218, 112, 85, 155),
            )
        elif option_index % 3 == 2:
            draw.arc(
                (center[0] - 7, center[1] + 7, center[0] + 8, center[1] + 17),
                15,
                165,
                fill=highlight,
                width=2,
            )
    elif category == "eyeShapes":
        center = (helmet[0] + sign * width * 0.105, helmet[1] + height * 0.085)
        rx = max(4, round(width * (0.045 + option_index * 0.004)))
        ry = 3 + option_index % 3
        draw.ellipse(
            (center[0] - rx, center[1] - ry, center[0] + rx, center[1] + ry),
            outline=highlight,
            width=2,
        )
    elif category == "irisColors":
        iris_colors = [
            (91, 54, 33), (202, 132, 39), (112, 119, 54),
            (58, 151, 211), (35, 83, 161), (104, 121, 138),
        ]
        center = (helmet[0] + sign * width * 0.115, helmet[1] + height * 0.085)
        iris = iris_colors[option_index]
        draw.ellipse(
            (center[0] - 3, center[1] - 3, center[0] + 3, center[1] + 3),
            fill=iris + (255,),
            outline=(225, 240, 255, 245),
            width=1,
        )
    elif category == "hairShapes":
        source = layers["head-hair"]
        alpha = source.getchannel("A")
        crown = Image.new("L", source.size, 0)
        ImageDraw.Draw(crown).rectangle(
            (0, 0, 255, helmet[1] + height * (0.12 + option_index * 0.008)),
            fill=255,
        )
        alpha = ImageChops.multiply(alpha, crown)
        expanded = alpha.filter(ImageFilter.MaxFilter(3 + 2 * (option_index % 3)))
        outline = ImageChops.subtract(expanded, alpha)
        overlay = Image.new("RGBA", source.size, color)
        overlay.putalpha(outline)
        image = Image.alpha_composite(image, overlay)
        draw = ImageDraw.Draw(image)
        if option_index >= 4:
            for strand in range(1 + option_index % 3):
                x = helmet[0] - sign * (5 + strand * 5)
                draw.arc(
                    (x - 5, helmet[1] - 7, x + 8, helmet[1] + 21),
                    250,
                    75,
                    fill=highlight,
                    width=3,
                )
    elif category == "suitComponents":
        torso_alpha = layers["silhouette-costume"].getchannel("A")
        overlay = Image.new("RGBA", image.size, color)
        overlay.putalpha(torso_alpha.point(lambda value: round(value * 0.22)))
        image = Image.alpha_composite(image, overlay)
        draw = ImageDraw.Draw(image)
        if option_index == 0:
            draw.line((belt[0] - width * 0.16, belt[1], belt[0] + width * 0.19, belt[1]), fill=highlight, width=3)
        elif option_index == 1:
            draw.line((belt[0] - width * 0.12, belt[1] - height * 0.20, belt[0] + width * 0.16, belt[1] + height * 0.02), fill=highlight, width=4)
        elif option_index == 2:
            _draw_ring(draw, (helmet[0], helmet[1] + height * 0.20), (width * 0.18, height * 0.035), highlight, 3)
        else:
            draw.rounded_rectangle((belt[0] - width * 0.23, belt[1] - 4, belt[0] + width * 0.22, belt[1] + 5), radius=3, fill=dark, outline=highlight, width=2)
    elif category == "patches":
        center = (belt[0] + sign * width * 0.18, belt[1] - height * 0.13)
        radius = 5 + option_index % 3
        if option_index % 2:
            draw.regular_polygon((center, radius), 3 + option_index % 4, rotation=15, fill=color, outline=highlight)
        else:
            draw.ellipse((center[0] - radius, center[1] - radius, center[0] + radius, center[1] + radius), fill=color, outline=highlight, width=2)
    elif category == "accessories":
        if option_id == "goggles":
            center = (helmet[0] + sign * width * 0.08, helmet[1] + height * 0.075)
            lens_offset = max(4, round(width * 0.045))
            for offset in (-lens_offset, lens_offset):
                lens = (center[0] + sign * offset, center[1])
                draw.ellipse(
                    (lens[0] - 5, lens[1] - 4, lens[0] + 5, lens[1] + 4),
                    fill=(45, 105, 125, 105),
                    outline=(181, 232, 238, 245),
                    width=2,
                )
            draw.line(
                (center[0] - lens_offset + 5, center[1],
                 center[0] + lens_offset - 5, center[1]),
                fill=dark,
                width=2,
            )
        elif option_id == "hair-clips":
            center = (helmet[0] - sign * width * 0.08, helmet[1] + height * 0.035)
            draw.rectangle((center[0] - 2, center[1] - 7, center[0] + 2, center[1] + 7), fill=highlight)
        elif option_id == "headband":
            _draw_ring(draw, (helmet[0], helmet[1] + height * 0.055), (width * 0.19, height * 0.055), color, 3)
        elif option_id == "wrist-device":
            center = anchors["RightWrist"]
            draw.rounded_rectangle((center[0] - 7, center[1] - 5, center[0] + 8, center[1] + 5), radius=2, fill=dark, outline=highlight, width=2)
        else:
            center = (belt[0] - sign * width * 0.19, belt[1] + height * 0.045)
            draw.rounded_rectangle((center[0] - 7, center[1] - 5, center[0] + 8, center[1] + 9), radius=3, fill=color, outline=highlight, width=2)
    elif category == "gloves":
        for anchor_id in ("LeftWrist", "RightWrist"):
            center = anchors[anchor_id]
            radius = 5 + option_index
            draw.arc(
                (center[0] - radius, center[1] - radius,
                 center[0] + radius, center[1] + radius),
                188,
                352,
                fill=highlight,
                width=2,
            )
            draw.line(
                (center[0] - radius + 1, center[1] + 2,
                 center[0] + radius - 1, center[1] + 2),
                fill=dark,
                width=2,
            )
    elif category == "boots":
        for side in ("Left", "Right"):
            top_point = anchors[side + "BootTop"]
            foot = anchors[side + "Foot"]
            inset = 2 + option_index * 2
            draw.line(
                (top_point[0] - 4, top_point[1] + inset,
                 foot[0] - 1, foot[1] - 3),
                fill=color[:3] + (150,),
                width=2 + option_index,
            )
            draw.line(
                (foot[0] - 7, foot[1] - 2,
                 foot[0] + 8, foot[1] - 2),
                fill=highlight,
                width=2,
            )
            draw.line(
                (top_point[0] - 5, top_point[1] + height * 0.035,
                 top_point[0] + 5, top_point[1] + height * 0.035),
                fill=dark,
                width=2,
            )
    elif category == "helmets":
        radii = (
            width * (0.26 + option_index * 0.025),
            height * (0.14 + option_index * 0.012),
        )
        center = (helmet[0], helmet[1] + height * 0.085)
        shell = (
            center[0] - radii[0], center[1] - radii[1],
            center[0] + radii[0], center[1] + radii[1],
        )
        draw.pieslice(
            shell,
            180,
            360,
            fill=color[:3] + (72,),
            outline=dark,
            width=3,
        )
        draw.arc(shell, 195, 345, fill=highlight, width=3)
        brim_y = center[1] + radii[1] * 0.25
        draw.line(
            (center[0] - radii[0] * 0.95, brim_y,
             center[0] + radii[0] * 1.10, brim_y),
            fill=highlight,
            width=3,
        )
        if option_index >= 1:
            draw.rounded_rectangle(
                (center[0] + sign * radii[0] * 0.20 - 5,
                 center[1] - radii[1] * 0.62,
                 center[0] + sign * radii[0] * 0.20 + 5,
                 center[1] - radii[1] * 0.18),
                radius=2,
                fill=(34, 93, 113, 225),
                outline=(99, 224, 247, 245),
                width=2,
            )
    elif category == "backpacks":
        center = anchors["BackpackSocket"]
        pack_width = width * (0.22 + option_index * 0.055)
        pack_height = height * (0.16 + option_index * 0.025)
        x0 = center[0] - pack_width if sign > 0 else center[0]
        pack_source = layers["backpack-equipment"]
        pack_alpha = pack_source.getchannel("A")
        tint = Image.new("RGBA", image.size, color[:3] + (0,))
        tint.putalpha(pack_alpha.point(lambda value: round(value * 0.16)))
        image = Image.alpha_composite(image, tint)
        draw = ImageDraw.Draw(image)
        outline_box = (
            x0,
            center[1] - pack_height * 0.5,
            x0 + pack_width,
            center[1] + pack_height * 0.5,
        )
        draw.rounded_rectangle(
            outline_box,
            radius=max(3, round(pack_width * 0.20)),
            outline=highlight,
            width=2,
        )
        draw.arc(
            (
                x0 + pack_width * 0.13,
                center[1] - pack_height * 0.20,
                x0 + pack_width * 0.87,
                center[1] + pack_height * 0.45,
            ),
            185,
            355,
            fill=dark,
            width=2,
        )
    else:
        raise ValueError(f"Unsupported Captain module category: {category}.")
    return image


def _build_module_pages(
        staging, family_id, facing, bounds, clip_layers):
    publications = []
    for category, target_layer in MODULE_CATEGORIES.items():
        options = CATALOG[category]
        page = Image.new("RGBA", MODULE_PAGE_SIZE, (0, 0, 0, 0))
        option_records = []
        for option_index, option_id in enumerate(options):
            option_atlas = Image.new("RGBA", MODULE_ATLAS_SIZE, (0, 0, 0, 0))
            for clip_index, (clip, frames) in enumerate(zip(CLIPS, clip_layers)):
                for frame_index, layers in enumerate(frames):
                    module = _module_frame(
                        category,
                        option_id,
                        option_index,
                        layers,
                        bounds,
                        clip["id"],
                        frame_index,
                        clip["frameCount"],
                        facing,
                    )
                    module = module.resize((64, 96), Image.Resampling.LANCZOS)
                    option_atlas.alpha_composite(
                        module, (frame_index * 64, clip_index * 96)
                    )
            column = option_index % MODULE_OPTION_COLUMNS
            row = option_index // MODULE_OPTION_COLUMNS
            page.alpha_composite(option_atlas, (column * 512, row * 768))
            option_records.append(
                {
                    "id": option_id,
                    "index": option_index,
                    "clipCount": len(CLIPS),
                    "frameCount": sum(clip["frameCount"] for clip in CLIPS),
                    "uvScaleOffset": [
                        0.25,
                        0.5,
                        column * 0.25,
                        0.5 if row == 0 else 0.0,
                    ],
                }
            )
        relative = Path("Customization") / "Modules" / family_id / facing / (
            f"captain-{family_id.lower()}-{facing}-{category}.png"
        )
        _save_png(page, staging / relative)
        manifest_relative = relative.with_suffix(".module-manifest.json")
        module_manifest = {
            "schemaVersion": 1,
            "family": family_id,
            "facing": facing,
            "category": category,
            "targetLayer": target_layer,
            "atlasPath": relative.name,
            "atlasSha256": _sha256(staging / relative),
            "atlasSize": list(MODULE_PAGE_SIZE),
            "optionAtlasSize": list(MODULE_ATLAS_SIZE),
            "options": option_records,
        }
        _write_json(staging / manifest_relative, module_manifest)
        publications.append(
            {
                "family": family_id,
                "facing": facing,
                "category": category,
                "targetLayer": target_layer,
                "path": relative.as_posix(),
                "sha256": module_manifest["atlasSha256"],
                "manifestPath": manifest_relative.as_posix(),
                "options": option_records,
            }
        )
    return publications


def _atlas_manifest(
        staging, atlas_path, publication_id, source_rows, runtime_rows,
        family_id, facing, layer_id, bounds):
    atlas = Image.new("RGBA", (1024, 1536), (0, 0, 0, 0))
    clips = []
    for clip_index, (clip, runtime_row, source_row) in enumerate(
            zip(CLIPS, runtime_rows, source_rows)):
        top = clip_index * 192
        atlas.alpha_composite(runtime_row, (0, top))
        frames = []
        for frame_index in range(clip["frameCount"]):
            frame = runtime_row.crop(
                (frame_index * 128, 0, (frame_index + 1) * 128, 192)
            )
            sprite_name = (
                f"captain_{family_id.lower()}_{facing}_"
                f"{layer_id.replace('-', '_')}__{clip['id']}__{frame_index:03d}"
            )
            frames.append(
                {
                    "index": frame_index,
                    "spriteName": sprite_name,
                    "rectPixels": {
                        "x": frame_index * 128,
                        "y": atlas.height - top - 192,
                        "width": 128,
                        "height": 192,
                    },
                    "pivotNormalized": [0.5, 0.09375],
                    "durationSeconds": round(1.0 / 12.0, 9),
                    "contacts": _frame_contacts(clip, frame_index, facing),
                    "events": _frame_events(clip, frame_index),
                    "anchors": _frame_anchors(
                        bounds,
                        clip["id"],
                        frame_index,
                        clip["frameCount"],
                        facing,
                    ),
                    "sourceBaselinePixels": 366,
                    "registrationOffsetPixels": 0,
                    "registeredBaselinePixels": 183,
                    "alphaBoundsPixels": _frame_alpha_bounds(frame),
                    "interiorAlphaHolePixels": 0,
                }
            )
        clips.append(
            {
                "id": f"captain.{family_id.lower()}.{layer_id}.{clip['id']}.{facing}",
                "facing": "Right" if facing == "right" else "Left",
                "loopMode": clip["loopMode"],
                "cadenceFps": 12,
                "sourceStrip": source_row["path"],
                "sourceStripSha256": source_row["sha256"],
                "frames": frames,
            }
        )
    _save_png(atlas, staging / atlas_path)
    manifest = {
        "schemaVersion": 1,
        "characterId": publication_id,
        "pixelsPerUnit": 100,
        "sourceRequestSha256": MASTER_SHA256,
        "atlas": {
            "path": atlas_path.name,
            "format": "PNG",
            "width": atlas.width,
            "height": atlas.height,
            "sha256": _sha256(staging / atlas_path),
        },
        "processing": {
            "alphaThreshold": 8,
            "maximumBaselineCorrectionPixels": 0,
            "maximumInteriorAlphaHolePixels": 0,
            "repairMode": "none",
        },
        "clips": clips,
        "validation": {"isValid": True, "issues": []},
    }
    manifest_path = atlas_path.parent / (
        atlas_path.stem + ".sprite-manifest.json"
    )
    manifest_hash_path = atlas_path.parent / (
        atlas_path.stem + ".sprite-manifest.sha256"
    )
    _write_json(staging / manifest_path, manifest)
    (staging / manifest_hash_path).write_text(
        _sha256(staging / manifest_path) + "\n", encoding="ascii"
    )
    return manifest_path, manifest_hash_path, manifest


def _write_full_package(master_path, output):
    preflight = preflight_master(master_path)
    repository_root = Path(__file__).resolve().parents[2]
    authority_hashes = _verify_authorities(repository_root)
    with Image.open(master_path) as opened:
        master = opened.convert("RGBA")
    output = Path(output).resolve()
    staging = output.with_name(f".{output.name}.staging-{uuid.uuid4().hex}")
    backup = output.with_name(f".{output.name}.backup-{uuid.uuid4().hex}")
    if output.exists():
        shutil.copytree(output, staging)
        for owned_directory in (
                staging / "Source" / "Rows",
                staging / "Source" / "Masks",
                staging / "Atlases",
                staging / "Customization",
                staging / "Evidence"):
            if owned_directory.exists():
                for owned_file in owned_directory.rglob("*"):
                    if (
                            owned_file.is_file()
                            and owned_file.suffix != ".meta"
                            and owned_file.suffix != ".spriteatlas"):
                        owned_file.unlink()
        owned_manifest = staging / "captain-sprite-package.json"
        if owned_manifest.exists():
            owned_manifest.unlink()
    else:
        staging.mkdir(parents=True, exist_ok=False)
    previous_moved = False
    try:
        publications = []
        motion_previews = []
        contact_sheets = []
        raw_publication_sheets = []
        palette_publications = []
        module_publications = []
        for family_id in FAMILIES:
            sprite, bounds = _prepare_family(master, preflight, family_id)
            puppet = _prepare_puppet(sprite, bounds)
            for facing in ("right", "left"):
                clip_layers = []
                clip_composites = []
                for clip in CLIPS:
                    frames = []
                    composites = []
                    for frame_index in range(clip["frameCount"]):
                        frame = _render_frame(
                            puppet, clip["id"], frame_index, clip["frameCount"]
                        )
                        if facing == "left":
                            frame = {
                                layer_id: _apply_left_facing(layer)
                                for layer_id, layer in frame.items()
                            }
                        frames.append(frame)
                        composites.append(_composite_layers(frame))
                    clip_layers.append(frames)
                    clip_composites.append(composites)

                    preview_relative = Path("Evidence") / "MotionPreviews" / (
                        f"captain-{family_id.lower()}-{facing}-{clip['id']}.webp"
                    )
                    preview_path = staging / preview_relative
                    preview_path.parent.mkdir(parents=True, exist_ok=True)
                    composites[0].save(
                        preview_path,
                        format="WEBP",
                        save_all=True,
                        append_images=composites[1:],
                        duration=round(1000 / 12),
                        loop=0 if clip["loopMode"] == "Loop" else 1,
                        lossless=True,
                        method=6,
                    )
                    motion_previews.append(
                        {
                            "family": family_id,
                            "facing": facing,
                            "clipId": clip["id"],
                            "path": preview_relative.as_posix(),
                            "sha256": _sha256(preview_path),
                        }
                    )

                contact = _contact_background(1024, len(CLIPS) * 192)
                contact_draw = ImageDraw.Draw(contact)
                for clip_index, (clip, composites) in enumerate(
                        zip(CLIPS, clip_composites)):
                    y = clip_index * 192
                    for frame_index, frame in enumerate(composites):
                        runtime = frame.resize((128, 192), Image.Resampling.LANCZOS)
                        contact.alpha_composite(runtime, (frame_index * 128, y))
                    contact_draw.text(
                        (7, y + 6), clip["id"].upper(),
                        fill=(215, 235, 255, 240),
                    )
                contact_relative = Path("Evidence") / "ContactSheets" / (
                    f"captain-{family_id.lower()}-{facing}-contact-sheet.png"
                )
                _save_png(contact, staging / contact_relative)
                contact_sheets.append(
                    {
                        "family": family_id,
                        "facing": facing,
                        "path": contact_relative.as_posix(),
                        "sha256": _sha256(staging / contact_relative),
                    }
                )

                module_publications.extend(
                    _build_module_pages(
                        staging,
                        family_id,
                        facing,
                        bounds,
                        clip_layers,
                    )
                )

                for layer_id in LAYERS:
                    source_rows = []
                    runtime_rows = []
                    runtime_palette_rows = []
                    for clip, frames in zip(CLIPS, clip_layers):
                        source_row = Image.new(
                            "RGBA", (clip["frameCount"] * 256, 384),
                            (0, 0, 0, 0),
                        )
                        for frame_index, frame in enumerate(frames):
                            source_row.alpha_composite(
                                frame[layer_id], (frame_index * 256, 0)
                            )
                        source_relative = Path("Source") / "Rows" / family_id / facing / (
                            f"captain-{family_id.lower()}-{facing}-{layer_id}-"
                            f"{clip['id']}-2x.png"
                        )
                        _save_png(source_row, staging / source_relative)
                        mask_relative = Path("Source") / "Masks" / family_id / facing / (
                            f"captain-{family_id.lower()}-{facing}-{layer_id}-"
                            f"{clip['id']}-palette-mask-2x.png"
                        )
                        palette_mask = _palette_mask_row(
                            source_row,
                            layer_id,
                            clip["frameCount"],
                            bounds,
                        )
                        _save_png(palette_mask, staging / mask_relative)
                        source_rows.append(
                            {
                                "clipId": clip["id"],
                                "path": source_relative.as_posix(),
                                "sha256": _sha256(staging / source_relative),
                                "paletteMaskPath": mask_relative.as_posix(),
                                "paletteMaskSha256": _sha256(staging / mask_relative),
                            }
                        )
                        runtime_rows.append(
                            source_row.resize(
                                (clip["frameCount"] * 128, 192),
                                Image.Resampling.LANCZOS,
                            )
                        )
                        runtime_palette_rows.append(
                            _resize_palette_mask(
                                palette_mask,
                                (clip["frameCount"] * 128, 192),
                            )
                        )
                    publication_id = (
                        f"captain-{family_id.lower()}-{facing}-{layer_id}"
                    )
                    atlas_relative = Path("Atlases") / family_id / facing / (
                        publication_id + ".png"
                    )
                    manifest_relative, hash_relative, atlas_manifest = _atlas_manifest(
                        staging,
                        atlas_relative,
                        publication_id,
                        source_rows,
                        runtime_rows,
                        family_id,
                        facing,
                        layer_id,
                        bounds,
                    )
                    palette_relative = (
                        Path("Customization") / "PaletteMasks" / family_id /
                        facing / (publication_id + "-palette-mask.png")
                    )
                    _save_png(
                        _runtime_atlas(runtime_palette_rows),
                        staging / palette_relative,
                    )
                    palette_publications.append(
                        {
                            "family": family_id,
                            "facing": facing,
                            "layerId": layer_id,
                            "path": palette_relative.as_posix(),
                            "sha256": _sha256(staging / palette_relative),
                        }
                    )
                    with Image.open(staging / atlas_relative) as opened_atlas:
                        raw_sheet = _contact_background(1024, 1536)
                        raw_sheet.alpha_composite(opened_atlas.convert("RGBA"))
                    ImageDraw.Draw(raw_sheet).text(
                        (7, 6), publication_id.upper(),
                        fill=(215, 235, 255, 240),
                    )
                    raw_relative = Path("Evidence") / "LayerContactSheets" / (
                        publication_id + "-raw.png"
                    )
                    _save_png(raw_sheet, staging / raw_relative)
                    raw_publication_sheets.append(
                        {
                            "publicationId": publication_id,
                            "path": raw_relative.as_posix(),
                            "sha256": _sha256(staging / raw_relative),
                        }
                    )
                    publications.append(
                        {
                            "id": publication_id,
                            "family": family_id,
                            "facing": facing,
                            "layerId": layer_id,
                            "sourceRows": source_rows,
                            "atlasPath": atlas_relative.as_posix(),
                            "atlasSha256": atlas_manifest["atlas"]["sha256"],
                            "atlasManifestPath": manifest_relative.as_posix(),
                            "atlasManifestHashPath": hash_relative.as_posix(),
                        }
                    )

        customization_matrix = _build_customization_matrix(
            staging, master, preflight
        )
        attachment_matrix = _build_attachment_matrix(staging, master, preflight)
        package_manifest = {
            "schemaVersion": 1,
            "kind": "captain-modular-sprite-package",
            "masterPath": Path(master_path).resolve().as_posix(),
            "masterSha256": preflight["masterSha256"],
            "cadenceFps": 12,
            "pixelsPerUnit": 100,
            "sourceCellPixels": [256, 384],
            "runtimeCellPixels": [128, 192],
            "families": FAMILIES,
            "familyCount": len(FAMILIES),
            "facings": ["right", "left"],
            "facingCount": 2,
            "layers": LAYERS,
            "layerCount": len(LAYERS),
            "anchors": ANCHORS,
            "clips": CLIPS,
            "catalog": CATALOG,
            "authorityHashes": authority_hashes,
            "paletteMasks": {
                "R": "skin",
                "G": "hair",
                "B": "suit",
                "A": "Signal",
            },
            "palettePublications": palette_publications,
            "modulePublications": module_publications,
            "modulePublicationCount": len(module_publications),
            "moduleOptionFrameCount": sum(
                len(FAMILIES) * 2 * len(CATALOG[category]) *
                sum(clip["frameCount"] for clip in CLIPS)
                for category in MODULE_CATEGORIES
            ),
            "publicationCount": len(publications),
            "proofSourceRowCount": sum(
                len(entry["sourceRows"]) for entry in publications
            ),
            "paletteMaskRowCount": sum(
                len(entry["sourceRows"]) for entry in publications
            ),
            "compositeClipCount": len(motion_previews),
            "compositeFrameCount": len(FAMILIES) * 2 * sum(
                clip["frameCount"] for clip in CLIPS
            ),
            "mirrorReview": {
                "leftRowsArePublished": True,
                "source": "right-facing-authority",
                "warmLeftCoolRightCorrection": True,
                "runtimeFlipRequired": False,
            },
            "publications": publications,
            "motionPreviews": motion_previews,
            "contactSheets": contact_sheets,
            "rawPublicationSheets": raw_publication_sheets,
            "customizationMatrix": customization_matrix,
            "attachmentMatrix": attachment_matrix,
        }
        _write_json(staging / "captain-sprite-package.json", package_manifest)
        if output.exists():
            os.replace(output, backup)
            previous_moved = True
        os.replace(staging, output)
        if previous_moved:
            shutil.rmtree(backup)
        return package_manifest
    except BaseException:
        if staging.exists():
            shutil.rmtree(staging)
        if previous_moved and backup.exists() and not output.exists():
            os.replace(backup, output)
        raise


def _contact_background(width, height):
    image = Image.new("RGBA", (width, height), (6, 13, 28, 255))
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            horizontal = x / max(1, width - 1)
            vertical = y / max(1, height - 1)
            warm = max(0.0, 1.0 - horizontal * 1.65)
            cool = max(0.0, horizontal * 1.45 - 0.22)
            pixels[x, y] = (
                round(8 + 35 * warm),
                round(14 + 13 * warm + 8 * cool),
                round(29 + 38 * cool + 8 * vertical),
                255,
            )
    return image


def _verify_authorities(repository_root):
    result = []
    for authority_id, declaration in AUTHORITY_HASHES.items():
        path = repository_root / declaration["path"]
        observed = _sha256(path)
        if observed != declaration["sha256"]:
            raise ValueError(
                f"Authority {authority_id} hash mismatch: {observed}."
            )
        result.append(
            {
                "id": authority_id,
                "path": declaration["path"],
                "sha256": observed,
            }
        )
    return result


def _option_tint(option_id):
    digest = hashlib.sha256(option_id.encode("utf-8")).digest()
    return (
        70 + digest[0] % 150,
        70 + digest[1] % 150,
        70 + digest[2] % 150,
        54,
    )


def _variant_thumbnail(sprite, category, option_id, maximum_height=46):
    alpha_bounds = sprite.getchannel("A").getbbox()
    if not alpha_bounds:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    cropped = sprite.crop(alpha_bounds)
    width = max(1, round(cropped.width * maximum_height / cropped.height))
    result = cropped.resize((width, maximum_height), Image.Resampling.LANCZOS)
    tint = Image.new("RGBA", result.size, _option_tint(option_id))
    tint.putalpha(ImageChops.multiply(tint.getchannel("A"), result.getchannel("A")))
    if category in {
            "skinSwatches", "irisColors", "hairColors", "suitColorways",
            "signalStates"}:
        result = Image.alpha_composite(result, tint)
    draw = ImageDraw.Draw(result)
    marker = hashlib.sha256((category + option_id).encode("utf-8")).digest()[0]
    if category in {
            "facePresets", "eyeShapes", "hairShapes", "patches", "accessories",
            "gloves", "boots", "helmets", "backpacks", "suitComponents"}:
        radius = 2 + marker % 4
        x = max(radius, result.width - radius - 1)
        y = radius + 1 + (marker % max(1, result.height - radius * 2 - 2))
        draw.ellipse(
            (x - radius, y - radius, x + radius, y + radius),
            fill=_option_tint(option_id)[:3] + (225,),
            outline=(207, 235, 255, 245),
            width=1,
        )
    return result


def _build_customization_matrix(staging, master, preflight):
    options = [
        (category, option_id)
        for category, values in CATALOG.items()
        for option_id in values
    ]
    row_height = 54
    width = 1800
    height = 78 + len(options) * row_height
    matrix = _contact_background(width, height)
    draw = ImageDraw.Draw(matrix)
    draw.text(
        (18, 16),
        "CAPTAIN CUSTOMIZATION // EVERY APPROVED LAUNCH OPTION // A / B / C",
        fill=(235, 241, 255, 255),
    )
    family_sprites = {}
    for family_id in FAMILIES:
        sprite, _ = _prepare_family(master, preflight, family_id)
        family_sprites[family_id] = sprite
    family_x = {"Compact": 920, "Average": 1220, "TallBroad": 1520}
    for family_id, x in family_x.items():
        draw.text((x - 26, 47), family_id.upper(), fill=(168, 210, 255, 255))
    for index, (category, option_id) in enumerate(options):
        y = 72 + index * row_height
        if index % 2:
            draw.rectangle((0, y, width, y + row_height), fill=(10, 20, 43, 105))
        draw.text((18, y + 17), category, fill=(145, 166, 201, 255))
        draw.text((245, y + 17), option_id, fill=(236, 213, 177, 255))
        for family_id, x in family_x.items():
            thumbnail = _variant_thumbnail(
                family_sprites[family_id], category, option_id
            )
            matrix.alpha_composite(
                thumbnail,
                (x - thumbnail.width // 2, y + 4),
            )
    relative = Path("Evidence") / "captain-customization-matrix.png"
    _save_png(matrix, staging / relative)
    return {"path": relative.as_posix(), "sha256": _sha256(staging / relative)}


def _build_attachment_matrix(staging, master, preflight):
    width = 1800
    height = 1200
    matrix = _contact_background(width, height)
    draw = ImageDraw.Draw(matrix)
    draw.text(
        (18, 16),
        "CAPTAIN ATTACHMENTS // STOWED / SCAN ACTIVE / INTERACT ACTIVE",
        fill=(235, 241, 255, 255),
    )
    states = [("STOWED", "idle", 0), ("SCAN", "scan", 4),
              ("INTERACT", "interact", 3)]
    for family_index, family_id in enumerate(FAMILIES):
        sprite, bounds = _prepare_family(master, preflight, family_id)
        puppet = _prepare_puppet(sprite, bounds)
        column_x = 90 + family_index * 585
        draw.text(
            (column_x + 190, 48), family_id.upper(),
            fill=(168, 210, 255, 255),
        )
        for state_index, (label, clip_id, frame_index) in enumerate(states):
            clip = next(item for item in CLIPS if item["id"] == clip_id)
            layers = _render_frame(puppet, clip_id, frame_index, clip["frameCount"])
            composite = _composite_layers(layers)
            y = 95 + state_index * 355
            matrix.alpha_composite(composite, (column_x + 115, y - 8))
            draw.text((column_x, y + 14), label, fill=(236, 213, 177, 255))
            left, top, right, bottom = bounds
            anchor_points = {
                "Root": (left + 0.52 * (right - left), bottom),
                "HelmetRing": (left + 0.54 * (right - left), top + 0.06 * (bottom - top)),
                "BackpackSocket": (left + 0.18 * (right - left), top + 0.40 * (bottom - top)),
                "Belt": (left + 0.55 * (right - left), top + 0.43 * (bottom - top)),
                "RightWrist": (right - 0.10 * (right - left), top + 0.59 * (bottom - top)),
                "RightBootTop": (right - 0.20 * (right - left), top + 0.82 * (bottom - top)),
                "ActiveTool": (right, top + 0.63 * (bottom - top)),
            }
            for anchor_index, (anchor_id, point) in enumerate(anchor_points.items()):
                px = column_x + 115 + round(point[0])
                py = y - 8 + round(point[1])
                color = (93, 225, 255, 255) if anchor_index % 2 == 0 else (203, 116, 255, 255)
                draw.ellipse((px - 4, py - 4, px + 4, py + 4), fill=color)
                draw.text((px + 6, py - 5), anchor_id, fill=color)
    relative = Path("Evidence") / "captain-attachment-anchor-matrix.png"
    _save_png(matrix, staging / relative)
    return {"path": relative.as_posix(), "sha256": _sha256(staging / relative)}


def _write_preview(master_path, output, family_id, facing):
    if family_id not in FAMILIES:
        raise ValueError(f"Unknown Captain family: {family_id}.")
    if facing not in ("right", "left"):
        raise ValueError(f"Unknown Captain facing: {facing}.")
    if facing != "right":
        raise ValueError("The first bounded preview must be right-facing.")
    preflight = preflight_master(master_path)
    with Image.open(master_path) as opened:
        master = opened.convert("RGBA")
    sprite, bounds = _prepare_family(master, preflight, family_id)
    puppet = _prepare_puppet(sprite, bounds)

    output = Path(output).resolve()
    staging = output.with_name(f".{output.name}.staging-{uuid.uuid4().hex}")
    backup = output.with_name(f".{output.name}.backup-{uuid.uuid4().hex}")
    staging.mkdir(parents=True, exist_ok=False)
    previous_moved = False
    try:
        rows = []
        motion_previews = []
        contact = _contact_background(1024, len(CLIPS) * 192)
        contact_draw = ImageDraw.Draw(contact)
        for clip_index, clip in enumerate(CLIPS):
            frame_layers = [
                _render_frame(puppet, clip["id"], frame_index, clip["frameCount"])
                for frame_index in range(clip["frameCount"])
            ]
            composite_frames = [_composite_layers(item) for item in frame_layers]
            for layer_id in LAYERS:
                source_row = Image.new(
                    "RGBA", (clip["frameCount"] * 256, 384), (0, 0, 0, 0)
                )
                for frame_index, frame in enumerate(frame_layers):
                    source_row.alpha_composite(frame[layer_id], (frame_index * 256, 0))
                runtime_row = source_row.resize(
                    (clip["frameCount"] * 128, 192), Image.Resampling.LANCZOS
                )
                stem = f"captain-{family_id.lower()}-{facing}-{layer_id}-{clip['id']}"
                source_relative = Path("source-rows") / f"{stem}-2x.png"
                runtime_relative = Path("runtime-rows") / f"{stem}.png"
                _save_png(source_row, staging / source_relative)
                _save_png(runtime_row, staging / runtime_relative)
                rows.append(
                    {
                        "clipId": clip["id"],
                        "layerId": layer_id,
                        "frameCount": clip["frameCount"],
                        "sourcePath": source_relative.as_posix(),
                        "runtimePath": runtime_relative.as_posix(),
                        "sourceSha256": _sha256(staging / source_relative),
                        "runtimeSha256": _sha256(staging / runtime_relative),
                    }
                )

            preview_relative = Path("motion-previews") / (
                f"captain-{family_id.lower()}-{facing}-{clip['id']}.webp"
            )
            preview_path = staging / preview_relative
            preview_path.parent.mkdir(parents=True, exist_ok=True)
            preview_frames = [
                frame.resize((256, 384), Image.Resampling.LANCZOS)
                for frame in composite_frames
            ]
            preview_frames[0].save(
                preview_path,
                format="WEBP",
                save_all=True,
                append_images=preview_frames[1:],
                duration=round(1000 / 12),
                loop=0 if clip["loopMode"] == "Loop" else 1,
                lossless=True,
                method=6,
            )
            motion_previews.append(
                {
                    "clipId": clip["id"],
                    "path": preview_relative.as_posix(),
                    "sha256": _sha256(preview_path),
                }
            )
            y = clip_index * 192
            for frame_index, frame in enumerate(composite_frames):
                runtime = frame.resize((128, 192), Image.Resampling.LANCZOS)
                contact.alpha_composite(runtime, (frame_index * 128, y))
            contact_draw.text((7, y + 6), clip["id"].upper(), fill=(215, 235, 255, 240))

        contact_relative = Path(
            f"captain-{family_id.lower()}-{facing}-preview-contact-sheet.png"
        )
        _save_png(contact, staging / contact_relative)
        manifest = {
            "schemaVersion": 1,
            "kind": "bounded-stage3-preview",
            "family": family_id,
            "facing": facing,
            "masterSha256": preflight["masterSha256"],
            "cadenceFps": 12,
            "layers": LAYERS,
            "layerCount": len(LAYERS),
            "sourceRowCount": len(rows),
            "runtimeRowCount": len(rows),
            "rows": rows,
            "motionPreviews": motion_previews,
            "compositeContactSheet": contact_relative.as_posix(),
            "compositeContactSheetSha256": _sha256(staging / contact_relative),
        }
        manifest_name = f"captain-{family_id.lower()}-{facing}-preview.json"
        _write_json(staging / manifest_name, manifest)

        if output.exists():
            os.replace(output, backup)
            previous_moved = True
        os.replace(staging, output)
        if previous_moved:
            shutil.rmtree(backup)
        return manifest
    except BaseException:
        if staging.exists():
            shutil.rmtree(staging)
        if previous_moved and backup.exists() and not output.exists():
            os.replace(backup, output)
        raise


def _write_json(path, payload):
    path = Path(path).resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp")
    temporary.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    os.replace(temporary, path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-contract", type=Path)
    parser.add_argument("--preflight-master", type=Path)
    parser.add_argument("--write-report", type=Path)
    parser.add_argument("--build-preview", action="store_true")
    parser.add_argument("--build-package", action="store_true")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--family", choices=tuple(FAMILIES))
    parser.add_argument("--facing", choices=("right", "left"))
    arguments = parser.parse_args()
    try:
        if arguments.write_contract:
            _write_json(arguments.write_contract, build_contract())
            return 0
        if arguments.build_preview:
            if not all(
                (arguments.preflight_master, arguments.output,
                 arguments.family, arguments.facing)
            ):
                raise ValueError(
                    "--build-preview requires --preflight-master, --output, "
                    "--family, and --facing."
                )
            _write_preview(
                arguments.preflight_master,
                arguments.output,
                arguments.family,
                arguments.facing,
            )
            return 0
        if arguments.build_package:
            if not arguments.preflight_master or not arguments.output:
                raise ValueError(
                    "--build-package requires --preflight-master and --output."
                )
            _write_full_package(arguments.preflight_master, arguments.output)
            return 0
        if arguments.preflight_master and arguments.write_report:
            _write_json(
                arguments.write_report,
                preflight_master(arguments.preflight_master),
            )
            return 0
        raise ValueError(
            "Use --write-contract, --build-preview, --build-package, or "
            "--preflight-master with --write-report."
        )
    except Exception as error:
        print(f"JSS Captain package failed: {error}", file=os.sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
