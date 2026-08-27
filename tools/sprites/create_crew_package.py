#!/usr/bin/env python3
"""Build the five bespoke crew/Ori sprite publications deterministically."""

import argparse
import hashlib
import json
import math
import os
import shutil
import sys
import uuid
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageOps

SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

from create_captain_package import (  # noqa: E402
    CLIPS as CAPTAIN_CLIPS,
    _apply_left_facing,
    _frame_anchors as captain_frame_anchors,
    _motion_state,
    _prepare_puppet,
    _transform,
    _translate,
)


SCHEMA_VERSION = 1
CADENCE_FPS = 12
SOURCE_CELL = (256, 384)
RUNTIME_CELL = (128, 192)
PIXELS_PER_UNIT = 100
OWNER_FILE = ".jss-crew-sprite-owner.json"
EXPRESSIONS = [
    "neutral", "happy", "curious", "worried", "afraid",
    "surprised", "determined", "sad", "blink", "speaking",
]
SPEECH_SHAPES = 6
HUMAN_ANCHORS = [
    "Root", "LeftFoot", "RightFoot", "LeftHand", "RightHand",
    "HelmetRing", "BackpackSocket", "Belt", "LeftWrist", "RightWrist",
    "LeftBootTop", "RightBootTop", "ActiveTool", "StowedTool",
]
ORI_ANCHORS = [
    "Root", "LeftWheelContact", "RightWheelContact", "HeadRotationRing",
    "OpticalEye", "SignalAntenna", "LeftArmBay", "RightArmBay",
    "LeftGripper", "RightGripper", "SecondaryScanner", "ServicePanel",
    "ActiveTool", "StowedTool",
]
REFERENCE_HASHES = {
    "expressions.png": "cda3afe4e215237313dc18ab190eef6080387eb22a4c1677bc13a307b3cea655",
    "equipment.png": "61a21a44b888d202ac51d28834334671f20b40a041d5fc6ca055ede3682442ef",
    "master-style-sheet.png": "bdcdf2e36e23d49b9c15f9734d037930151606e765da74d28a1381694060f7c5",
    "crew-height-lineup.png": "63d15525a438c0cb8e5bac9e56034c1138a37a048a8e6d49ea910f46a8049af0",
}
CHARACTERS = {
    "Mira": {
        "id": "mira",
        "heightMeters": 1.54,
        "rigKind": "FlattenedHuman",
        "referenceSha256": "7d7ebbbe494ba1899e5d0f4549e5f363ac58a00e75fadf8c25524989524787a5",
        "masterSha256": "379baba132c7a3d5b21cefe4f9ba4a80180d649cb547e4577982b7a53f621ff7",
        "toolSha256": "135c75a2e4df30e27b6ec4183c7c4e5138b2c911c2113a2fbdcb5c9d099ab9f6",
        "rightMotionSha256": "aa62d8df4f936cb1bbed52009167b94cf39713302e8db0e0868d661c724bfc75",
        "leftMotionSha256": "32adaa52f0d9d3b1b4c47e99bdfad5dc68029ef7d9d2e753ffbf49f1017f30db",
        "climbSourceSha256": {
            "right": "f19894b175a131a3f398f67c0e38f3b678745f6e3ab9af1211574fb631ce591a",
            "left": "42384b12e6aff59da84a32c28e3da78a60ddfda49fcb1e1d54fa72d22edb8628",
        },
        "interactionSourceSha256": {
            "right": "6247f44570e5e634c7eed37b536a6ef39aa942ba4efc17527232d14d174aef80",
            "left": "6a8665f0ede300c1b5491658c9fec9373c285dc4d91405b6cd77ea5f230e297c",
        },
        "interactSource": "scan",
        "roleMotionId": "calm-deliberate-observer",
        "roleEquipment": ["spectrum-viewer", "atmosphere-sampler", "violet"],
        "motion": {"stride": 0.82, "arm": 0.72, "bob": 0.65, "tilt": -0.5},
        "expressionRow": (233, 368),
        "toolSize": 76,
        "accent": (162, 117, 231, 255),
    },
    "Juno": {
        "id": "juno",
        "heightMeters": 1.48,
        "rigKind": "FlattenedHuman",
        "referenceSha256": "b46e416d4ac0901efdf9dbcd5eff244e8f930f64e117fd5a8c753053930da722",
        "masterSha256": "6bf80f7cbfa0447a86156c31ebd1cfd71f4a2040b9586f8d18830609efcef8a2",
        "toolSha256": "ed4af510817191acc3624fe311e6cd904d3560556d54d16c192940dd2819a184",
        "rightMotionSha256": "c3ced9865f18b5026c2a78fdff87fd6a2060c4ea9e0714ae9dbc09e261d9d9a5",
        "leftMotionSha256": "492331d4298f85aa63c5300f0596e9d307dd33fe9a40d22833cfe2fd35aafe46",
        "climbSourceSha256": {
            "right": "e9452488a2c28d8a3613d9ca076b284a9fdab6f87edcad798f2f43fc445e6367",
            "left": "e04c4e58963297a965133e930c095ae05f90bb25eb826610b4c18e5ffe5a54dd",
        },
        "interactionSourceSha256": {
            "right": "a5d3e9a7eff3eab673ec88798e7bdebe252bb5e9589692fdb0059fa8607e05f1",
            "left": "8869b2c7deddad40c720566fe923ea5ca8e9620d400f3eae9aebb1104685864b",
        },
        "interactSource": "scan",
        "roleMotionId": "compact-energetic-builder",
        "roleEquipment": ["wrench", "diagnostic-driver", "repair-pack"],
        "motion": {"stride": 1.08, "arm": 1.12, "bob": 1.12, "tilt": -2.0},
        "expressionRow": (365, 500),
        "toolSize": 64,
        "accent": (238, 113, 42, 255),
    },
    "Kai": {
        "id": "kai",
        "heightMeters": 1.62,
        "rigKind": "FlattenedHuman",
        "referenceSha256": "d53aa52b5cdefaa63988394a94f92e4a3471fd0d48c66f69aa4eeb2227ee8396",
        "masterSha256": "768eac3296e51ff08c103a3edae468d1a7ac5d80e30fec3ffc18de19711204c0",
        "toolSha256": "8381f6f4483ef1d23a7c7494731e0aeca22ca979bc9d9d0ede3570824ce35e18",
        "rightMotionSha256": "53185cfab70d31a1ab61d6f59e03ee61e1c50479384b2c113b3ed7b89d4cffc3",
        "leftMotionSha256": "12f59aea4207cd80b81cc10961a6e6e1e32f86e57a7c75f0d8fbbe7c0bca7a35",
        "climbSourceSha256": {
            "right": "c81ca26b237e47a7b8a04c1bc67339d2388c4c51dcba1c2b4927bdff703a0c32",
            "left": "93f85da6dfdc865ee176777019e3ce40a0940d23544141d3b83951954551ade3",
        },
        "interactSource": "interact",
        "roleMotionId": "long-momentum-navigator",
        "roleEquipment": ["route-controller", "heading-display", "pilot-pack"],
        "motion": {"stride": 1.20, "arm": 1.06, "bob": 0.92, "tilt": -3.0},
        "expressionRow": (496, 629),
        "toolSize": 68,
        "accent": (49, 187, 199, 255),
    },
    "Bea": {
        "id": "bea",
        "heightMeters": 1.51,
        "rigKind": "FlattenedHuman",
        "referenceSha256": "28927ab81d62f07deb7fc7a327d19e4fe60f4fd054f98a1a6cccc481eb4ba0e0",
        "masterSha256": "b2329b417429864f91eb0d387db02179e602fc68fba6b9ac8c3f8f3f75e5cdd9",
        "toolSha256": "6977bd49ad772a5d5a153c52d7e79b1bb72206b70c1ef7f11aaba3181ec51d56",
        "rightMotionSha256": "4b075f4e72c8ed93e7cb5de56a1f203f5790789cd2de9b661f6959b9aad4564b",
        "leftMotionSha256": "f4a13cb69ab56da381cd351c1ecd41a01320e340d67c46e9565207bfb402547d",
        "climbSourceSha256": {
            "right": "d0bee46dfcf9986566db7d0c6f8ace6605ae5b9e4b853b8491f8283131931080",
            "left": "2f22a5e7f5718113a26d4d0f908889999f814658ef73050986dd0bc359bb01f0",
        },
        "scanSourceSha256": {
            "right": "e681bb26519e9d34bdf2a074f5cf692fc8ef3d224dfef1b46d216819ede1e7fa",
            "left": "c558d91521201a2a671426f6b0de7144c4d875971f01585842c914bf827729a0",
        },
        "interactSource": "interact",
        "roleMotionId": "careful-recording-chronicler",
        "roleEquipment": ["optical-camera", "atlas-tablet", "archive-pack"],
        "motion": {"stride": 0.90, "arm": 0.66, "bob": 0.78, "tilt": -1.0},
        "expressionRow": (628, 758),
        "toolSize": 72,
        "accent": (95, 151, 202, 255),
    },
    "Ori": {
        "id": "ori",
        "heightMeters": 0.68,
        "rigKind": "Mechanical",
        "dimensionsMeters": [0.54, 0.68, 0.58],
        "referenceSha256": "16e0b936ff7b93b3ef28a1f4bab862c7a78981e2a3c765cb6ea09e05527b22ce",
        "masterSha256": "af81cf2213137f818c6dd71db512f5c8698d87dc3ab6b05907b84e5ab8f8dffb",
        "toolSha256": "345a662aaa7508e56fd26b82b5565b0d9db3678290a89c1cb91b942ab49347e9",
        "rightMotionSha256": "4daec52788ba7d79270da4738ace8521153c10728cf3fa83ae93dff227cb7fa9",
        "leftMotionSha256": "bdb6664e9d5963efd588dce3e69c9fd2fc1bc6b16cda3fd604dc6931ec3332d2",
        "climbSourceSha256": {
            "right": "c6c85abfb6df5a7c33c8bf70feadd0b72669528b0d7b2148a101c0be6a6e3d2b",
            "left": "0aa107b13b7cb99b618156749aa8d6d41cacc2ac454b37a66bebc669232dfc92",
        },
        "interactionSourceSha256": {
            "right": "e3968d34f8dab0023604d13e3c9e007bc8a30a4146708d3a35df335865321d9e",
            "left": "54d7825da1ced04ba841209cec28916c6d27807174e3db5566dbc5bdfbafcb94",
        },
        "interactSource": "scan",
        "roleMotionId": "four-wheel-survey-companion",
        "roleEquipment": ["secondary-scanner", "arm-bay", "four-wheel"],
        "expressionRow": (758, 898),
        "toolSize": 60,
        "accent": (91, 219, 255, 255),
    },
}
CLIP_COUNTS = {clip["id"]: clip["frameCount"] for clip in CAPTAIN_CLIPS}
LAND_CONTACT_START = {
    "Mira": 1,
    "Juno": 1,
    "Kai": 2,
    "Bea": 3,
    "Ori": 1,
}


def build_contract():
    characters = {}
    for name, source in CHARACTERS.items():
        characters[name] = {
            "id": source["id"],
            "heightMeters": source["heightMeters"],
            "rigKind": source["rigKind"],
            "referenceSha256": source["referenceSha256"],
            "roleMotionId": source["roleMotionId"],
            "roleEquipment": source["roleEquipment"],
            "anchors": ORI_ANCHORS if name == "Ori" else HUMAN_ANCHORS,
        }
        if name == "Ori":
            characters[name]["dimensionsMeters"] = source["dimensionsMeters"]
    return {
        "schemaVersion": SCHEMA_VERSION,
        "cadenceFps": CADENCE_FPS,
        "pixelsPerUnit": PIXELS_PER_UNIT,
        "sourceCellPixels": list(SOURCE_CELL),
        "runtimeCellPixels": list(RUNTIME_CELL),
        "facings": ["right", "left"],
        "clips": CLIP_COUNTS,
        "expressions": EXPRESSIONS,
        "speechShapes": SPEECH_SHAPES,
        "characters": characters,
    }


def _sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _write_json(path, payload):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def _save_png(image, path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=False, compress_level=9)


def _verify_authorities(reference_root, source_root):
    for filename, expected in REFERENCE_HASHES.items():
        actual = _sha256(reference_root / filename)
        if actual != expected:
            raise ValueError(
                f"Authority hash mismatch for {filename}: {actual}."
            )
    for name, identity in CHARACTERS.items():
        character_id = identity["id"]
        reference = reference_root / f"{character_id}.png"
        master = source_root / name / "Source" / f"{character_id}-right-master-v1.png"
        tool = source_root / name / "Source" / f"{character_id}-role-tool-v1.png"
        right_motion = (
            source_root / name / "Source" /
            f"{character_id}-right-motion-sheet-v1.png"
        )
        left_motion = (
            source_root / name / "Source" /
            f"{character_id}-left-motion-sheet-v1.png"
        )
        for path, expected in (
            (reference, identity["referenceSha256"]),
            (master, identity["masterSha256"]),
            (tool, identity["toolSha256"]),
            (right_motion, identity["rightMotionSha256"]),
            (left_motion, identity["leftMotionSha256"]),
        ):
            if not path.is_file() or _sha256(path) != expected:
                raise ValueError(f"Pinned source authority mismatch: {path}.")
        for facing, expected in identity.get("climbSourceSha256", {}).items():
            climb_source = (
                source_root / name / "Source" /
                f"{character_id}-{facing}-climb-actor-only-v2.png"
            )
            if not climb_source.is_file() or _sha256(climb_source) != expected:
                raise ValueError(
                    f"Pinned climb authority mismatch: {climb_source}."
                )
        for facing, expected in identity.get(
            "interactionSourceSha256", {}
        ).items():
            interaction_source = (
                source_root / name / "Source" /
                f"{character_id}-{facing}-interact-actor-only-v2.png"
            )
            if (
                not interaction_source.is_file()
                or _sha256(interaction_source) != expected
            ):
                raise ValueError(
                    "Pinned interaction authority mismatch: "
                    f"{interaction_source}."
                )
        for facing, expected in identity.get("scanSourceSha256", {}).items():
            scan_source = (
                source_root / name / "Source" /
                f"{character_id}-{facing}-scan-actor-only-v2.png"
            )
            if not scan_source.is_file() or _sha256(scan_source) != expected:
                raise ValueError(f"Pinned scan authority mismatch: {scan_source}.")


def _source_master(source_root, name, identity):
    character_id = identity["id"]
    source = Image.open(
        source_root / name / "Source" / f"{character_id}-right-master-v1.png"
    ).convert("RGBA")
    bounds = source.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"{name} master has no visible pixels.")
    crop = source.crop(bounds)
    target_height = round(identity["heightMeters"] * PIXELS_PER_UNIT * 2)
    target_width = max(1, round(crop.width * target_height / crop.height))
    crop = crop.resize((target_width, target_height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", SOURCE_CELL, (0, 0, 0, 0))
    left = (SOURCE_CELL[0] - target_width) // 2
    top = SOURCE_CELL[1] - 18 - target_height
    canvas.alpha_composite(crop, (left, top))
    return canvas, (left, top, left + target_width, top + target_height)


def _source_tool(source_root, name, identity):
    character_id = identity["id"]
    source = Image.open(
        source_root / name / "Source" / f"{character_id}-role-tool-v1.png"
    ).convert("RGBA")
    bounds = source.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"{name} tool has no visible pixels.")
    crop = source.crop(bounds)
    target = identity["toolSize"]
    scale = min(target / crop.width, target / crop.height)
    return crop.resize(
        (max(1, round(crop.width * scale)), max(1, round(crop.height * scale))),
        Image.Resampling.LANCZOS,
    )


def _remove_chroma_green(image):
    rgb = image.convert("RGB")
    red, green, blue = rgb.split()
    max_rb = ImageChops.lighter(red, blue)
    dominance = ImageChops.subtract(green, max_rb)
    alpha = ImageOps.invert(dominance).point(
        lambda value: 0 if value < 108 else min(255, (value - 108) * 3)
    )
    spill_ceiling = max_rb.point(lambda value: min(255, round(value * 1.22)))
    controlled_green = ImageChops.darker(green, spill_ceiling)
    return Image.merge("RGBA", (red, controlled_green, blue, alpha))


def _sample_sequence(items, count):
    if len(items) < count - 1:
        raise ValueError(
            f"Motion row has {len(items)} poses but requires {count}.")
    if count == 1:
        return [items[0]]
    # A single authored hold is valid for an eight-frame loop. It is preferable
    # to inventing or mirroring a pose that was not present in the approved
    # facing-specific storyboard.
    indices = [round(index * (len(items) - 1) / (count - 1))
               for index in range(count)]
    return [items[index] for index in indices]


def _projection_density(mask, axis):
    """Return foreground-pixel density rather than a boolean projection."""
    if axis == "y":
        return [
            sum(mask.crop((0, y, mask.width, y + 1)).tobytes())
            for y in range(mask.height)
        ]
    if axis == "x":
        return [
            sum(mask.crop((x, 0, x + 1, mask.height)).tobytes())
            for x in range(mask.width)
        ]
    raise ValueError(f"Unsupported projection axis: {axis}.")


def _smoothed(values, radius):
    prefix = [0]
    for value in values:
        prefix.append(prefix[-1] + value)
    return [
        prefix[min(len(values), index + radius + 1)]
        - prefix[max(0, index - radius)]
        for index in range(len(values))
    ]


def _authored_row_centers(mask, row_count=7):
    """Find storyboard rows even when adjacent poses overlap vertically."""
    density = _projection_density(mask, "y")
    score = _smoothed(density, max(3, mask.height // 80))
    separation = max(24, mask.height // 11)
    centers = []
    for candidate in sorted(
        range(mask.height),
        key=lambda index: score[index],
        reverse=True,
    ):
        if all(abs(candidate - existing) >= separation for existing in centers):
            centers.append(candidate)
            if len(centers) == row_count:
                break
    centers.sort()
    if len(centers) != row_count:
        raise ValueError(
            f"Motion sheet exposes {len(centers)} authored row centers; "
            f"expected {row_count}.")
    return centers, density


def _connected_components(mask, minimum_area):
    """Return exact 8-connected foreground components as scanline runs."""
    parents = []
    ranks = []
    rows = []
    previous = []

    def create_label():
        label = len(parents)
        parents.append(label)
        ranks.append(0)
        return label

    def find(label):
        while parents[label] != label:
            parents[label] = parents[parents[label]]
            label = parents[label]
        return label

    def union(left, right):
        left = find(left)
        right = find(right)
        if left == right:
            return
        if ranks[left] < ranks[right]:
            left, right = right, left
        parents[right] = left
        if ranks[left] == ranks[right]:
            ranks[left] += 1

    for y in range(mask.height):
        values = mask.crop((0, y, mask.width, y + 1)).tobytes()
        current = []
        x = 0
        while x < mask.width:
            while x < mask.width and values[x] == 0:
                x += 1
            if x >= mask.width:
                break
            start = x
            while x < mask.width and values[x] != 0:
                x += 1
            current.append((start, x, create_label()))

        for start, end, label in current:
            for previous_start, previous_end, previous_label in previous:
                if previous_end < start - 1:
                    continue
                if previous_start > end + 1:
                    break
                union(label, previous_label)
        rows.append(current)
        previous = current

    components = {}
    for y, runs in enumerate(rows):
        for start, end, label in runs:
            root = find(label)
            component = components.setdefault(root, {
                "bounds": [start, y, end, y + 1],
                "area": 0,
                "runs": [],
            })
            bounds = component["bounds"]
            bounds[0] = min(bounds[0], start)
            bounds[1] = min(bounds[1], y)
            bounds[2] = max(bounds[2], end)
            bounds[3] = max(bounds[3], y + 1)
            component["area"] += end - start
            component["runs"].append((y, start, end))
    return [
        component for component in components.values()
        if component["area"] >= minimum_area
    ]


def _component_pose(transparent, component):
    left, top, right, bottom = component["bounds"]
    crop = transparent.crop((left, top, right, bottom))
    component_mask = Image.new("L", crop.size, 0)
    draw = ImageDraw.Draw(component_mask)
    for y, start, end in component["runs"]:
        draw.line(
            (start - left, y - top, end - left - 1, y - top),
            fill=255,
        )
    crop.putalpha(ImageChops.multiply(crop.getchannel("A"), component_mask))
    return crop, (left, top, right, bottom)


def _extract_sheet_rows(sheet_path):
    keyed = Image.open(sheet_path).convert("RGB")
    transparent = _remove_chroma_green(keyed)
    mask = transparent.getchannel("A").point(
        lambda value: 1 if value >= 36 else 0)
    row_centers, _ = _authored_row_centers(mask)
    minimum_area = max(500, mask.width * mask.height // 5000)
    components = _connected_components(mask, minimum_area)
    grouped = [[] for _ in row_centers]
    for component in components:
        bounds = component["bounds"]
        center_y = (bounds[1] + bounds[3]) / 2
        row_index = min(
            range(len(row_centers)),
            key=lambda index: abs(center_y - row_centers[index]),
        )
        grouped[row_index].append(component)

    rows = []
    for row_index, components_in_row in enumerate(grouped):
        components_in_row.sort(
            key=lambda component: (
                component["bounds"][0] + component["bounds"][2]
            ) / 2)
        if not components_in_row:
            raise ValueError(
                f"{sheet_path} row {row_index + 1} has no authored poses.")
        row_bottom = max(
            component["bounds"][3] for component in components_in_row)
        rows.append([
            (*_component_pose(transparent, component), row_bottom)
            for component in components_in_row
        ])
    return rows


def _normalize_authored_pose(pose, scale, row_bottom):
    crop, bounds, _ = pose
    width = max(1, round(crop.width * scale))
    height = max(1, round(crop.height * scale))
    fit = min(1.0, 248 / width, 360 / height)
    width = max(1, round(width * fit))
    height = max(1, round(height * fit))
    resized = crop.resize((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", SOURCE_CELL, (0, 0, 0, 0))
    baseline_delta = round((bounds[3] - row_bottom) * scale * fit)
    left = (SOURCE_CELL[0] - width) // 2
    top = 366 + baseline_delta - height
    top = min(max(4, top), SOURCE_CELL[1] - height - 4)
    canvas.alpha_composite(resized, (left, top))
    return canvas


def _extract_actor_only_strip(source_path, expected_count):
    """Read one approved chroma pose row without scene geometry."""
    transparent = _remove_chroma_green(Image.open(source_path).convert("RGB"))
    mask = transparent.getchannel("A").point(
        lambda value: 1 if value >= 36 else 0
    )
    minimum_area = max(500, mask.width * mask.height // 20000)
    components = sorted(
        _connected_components(mask, minimum_area),
        key=lambda component: component["bounds"][0],
    )
    if len(components) != expected_count:
        raise ValueError(
            f"{source_path} exposes {len(components)} actor poses; "
            f"expected {expected_count}."
        )
    poses = []
    for component in components:
        crop, bounds = _component_pose(transparent, component)
        poses.append((crop, bounds, bounds[3]))
    return poses


def _translated_climb(frames, facing, vertical_offsets=None):
    if vertical_offsets is None:
        vertical_offsets = (0, -4, -12, -28, -46, -64, -56, -50)
    direction = 1 if facing == "right" else -1
    horizontal_offsets = (0, 0, direction, direction * 2, direction * 3,
                          direction * 5, direction * 7, direction * 8)
    translated = []
    for index, frame in enumerate(frames):
        bounds = frame.getchannel("A").getbbox()
        if bounds is None:
            raise ValueError(f"Climb frame {index} has no visible pixels.")
        safe_y = max(vertical_offsets[index], 4 - bounds[1])
        translated.append(_translate(
            frame,
            horizontal_offsets[index],
            safe_y,
        ))
    return translated


def _load_authored_motion_frames(source_root, name, identity, facing):
    character_id = identity["id"]
    sheet_path = (
        source_root / name / "Source" /
        f"{character_id}-{facing}-motion-sheet-v1.png"
    )
    rows = _extract_sheet_rows(sheet_path)
    idle_raw = _sample_sequence(rows[0], 4)
    jump_raw = _sample_sequence(rows[3], 6)
    idle_height = sorted(pose[0].height for pose in idle_raw)[len(idle_raw) // 2]
    scale = identity["heightMeters"] * PIXELS_PER_UNIT * 2 / idle_height
    authored = {
        "idle": idle_raw,
        "run": _sample_sequence(rows[1], 8),
        "turn": _sample_sequence(rows[2], 4),
        "jump": jump_raw,
        "scan": _sample_sequence(rows[5], 8),
        "interact": _sample_sequence(rows[6], 6),
    }
    authored["land"] = [jump_raw[3], jump_raw[4], jump_raw[5], idle_raw[0]]

    normalized = {
        clip_id: [
            _normalize_authored_pose(pose, scale, pose[2])
            for pose in poses
        ]
        for clip_id, poses in authored.items()
    }

    if "interactionSourceSha256" in identity:
        interaction_source = (
            source_root / name / "Source" /
            f"{character_id}-{facing}-interact-actor-only-v2.png"
        )
        interaction_raw = _extract_actor_only_strip(interaction_source, 6)
        interaction_height = interaction_raw[-1][0].height
        interaction_scale = (
            identity["heightMeters"] * PIXELS_PER_UNIT * 2 /
            interaction_height
        )
        if name == "Juno":
            interaction_scale *= 0.51
        normalized["interact"] = [
            _normalize_authored_pose(
                pose,
                interaction_scale,
                pose[2],
            )
            for pose in interaction_raw
        ]

    if "scanSourceSha256" in identity:
        scan_source = (
            source_root / name / "Source" /
            f"{character_id}-{facing}-scan-actor-only-v2.png"
        )
        scan_raw = _extract_actor_only_strip(scan_source, 8)
        scan_height = scan_raw[-1][0].height
        scan_scale = (
            identity["heightMeters"] * PIXELS_PER_UNIT * 2 / scan_height
        )
        normalized["scan"] = [
            _normalize_authored_pose(pose, scan_scale, pose[2])
            for pose in scan_raw
        ]

    climb_source = (
        source_root / name / "Source" /
        f"{character_id}-{facing}-climb-actor-only-v2.png"
    )
    climb_raw = _extract_actor_only_strip(climb_source, 8)
    if name == "Ori" and facing == "left":
        climb_raw = [
            climb_raw[index]
            for index in (7, 1, 0, 3, 4, 5, 6, 7)
        ]
    settled_height = climb_raw[-1][0].height
    climb_scale = (
        identity["heightMeters"] * PIXELS_PER_UNIT * 2 / settled_height
    )
    climb_frames = [
        _normalize_authored_pose(pose, climb_scale, pose[2])
        for pose in climb_raw
    ]
    generated_climb_offsets = (0, -4, -8, -12, -16, -20, -18, -16)
    normalized["climb"] = _translated_climb(
        climb_frames,
        facing,
        generated_climb_offsets,
    )
    return normalized


def _snap_to_visible_alpha(alpha, target_x, target_y, bounds):
    """Resolve a landmark only inside its declared semantic search region."""
    left, top, right, bottom = bounds
    target_x = min(max(round(target_x), left), right - 1)
    target_y = min(max(round(target_y), top), bottom - 1)
    pixels = alpha.load()
    if pixels[target_x, target_y] >= 36:
        return target_x, target_y
    maximum = max(right - left, bottom - top)
    for radius in range(1, maximum + 1):
        candidates = []
        x0 = max(left, target_x - radius)
        x1 = min(right - 1, target_x + radius)
        y0 = max(top, target_y - radius)
        y1 = min(bottom - 1, target_y + radius)
        for x in range(x0, x1 + 1):
            candidates.append((x, y0))
            if y1 != y0:
                candidates.append((x, y1))
        for y in range(y0 + 1, y1):
            candidates.append((x0, y))
            if x1 != x0:
                candidates.append((x1, y))
        visible = [
            (x, y) for x, y in candidates if pixels[x, y] >= 36
        ]
        if visible:
            return min(
                visible,
                key=lambda point: (
                    (point[0] - target_x) ** 2
                    + (point[1] - target_y) ** 2
                ),
            )
    raise ValueError("Authored pose has no visible anchor target.")


def _semantic_region(bounds, relative):
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    x0, y0, x1, y1 = relative
    absolute = (
        max(0, math.floor(left + x0 * width)),
        max(0, math.floor(top + y0 * height)),
        min(SOURCE_CELL[0], math.ceil(left + x1 * width)),
        min(SOURCE_CELL[1], math.ceil(top + y1 * height)),
    )
    if absolute[2] <= absolute[0] or absolute[3] <= absolute[1]:
        raise ValueError(f"Invalid semantic anchor region: {relative}.")
    normalized = [
        round(absolute[0] / SOURCE_CELL[0], 9),
        round(absolute[1] / SOURCE_CELL[1], 9),
        round((absolute[2] - 1) / SOURCE_CELL[0], 9),
        round((absolute[3] - 1) / SOURCE_CELL[1], 9),
    ]
    return absolute, normalized


def _visible_frame_anchors(frame, name, facing, clip_id, frame_index):
    bounds = frame.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"{name} {clip_id} frame is empty.")
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    front = 0.88 if facing == "right" else 0.12
    rear = 1.0 - front
    front_region = ((0.42, 0.18, 1.00, 0.80)
                    if facing == "right"
                    else (0.00, 0.18, 0.58, 0.80))
    rear_region = ((0.00, 0.18, 0.58, 0.82)
                   if facing == "right"
                   else (0.42, 0.18, 1.00, 0.82))
    if name == "Ori":
        tool_visible = clip_id == "scan"
        climb_gripper_visible = clip_id == "climb" and 2 <= frame_index <= 6
        interact_gripper_visible = clip_id == "interact"
        grippers_visible = climb_gripper_visible or interact_gripper_visible
        points = {
            "Root": ((0.50, 0.98), (0.20, 0.72, 0.80, 1.00), True),
            "LeftWheelContact": ((0.28, 0.98), (0.02, 0.68, 0.55, 1.00), True),
            "RightWheelContact": ((0.72, 0.98), (0.45, 0.68, 0.98, 1.00), True),
            "HeadRotationRing": ((0.50, 0.46), (0.18, 0.20, 0.82, 0.65), True),
            "OpticalEye": ((front, 0.30), front_region, True),
            "SignalAntenna": ((rear, 0.03), (0.00, 0.00, 1.00, 0.30), True),
            "LeftArmBay": ((0.28, 0.58), (0.00, 0.35, 0.58, 0.78), True),
            "RightArmBay": ((0.72, 0.58), (0.42, 0.35, 1.00, 0.78), True),
            "LeftGripper": (
                (0.18, 0.68),
                (0.00, 0.32, 0.58, 0.88),
                grippers_visible and facing == "left",
            ),
            "RightGripper": (
                (0.82, 0.68),
                (0.42, 0.32, 1.00, 0.88),
                grippers_visible and facing == "right",
            ),
            "SecondaryScanner": ((front, 0.57), front_region, tool_visible),
            "ServicePanel": ((0.52, 0.62), (0.18, 0.38, 0.82, 0.82), True),
            "ActiveTool": ((front, 0.58), front_region, tool_visible),
            "StowedTool": ((rear, 0.70), rear_region, not tool_visible),
        }
        order = ORI_ANCHORS
    else:
        active_tool_visible = (
            clip_id == "scan"
            or (clip_id == "interact" and name != "Mira")
        )
        left_side = 0.33 if facing == "right" else 0.67
        right_side = 0.67 if facing == "right" else 0.33
        left_lower = ((0.00, 0.56, 0.60, 1.00)
                      if facing == "right"
                      else (0.40, 0.56, 1.00, 1.00))
        right_lower = ((0.40, 0.56, 1.00, 1.00)
                       if facing == "right"
                       else (0.00, 0.56, 0.60, 1.00))
        left_upper = rear_region if facing == "right" else front_region
        right_upper = front_region if facing == "right" else rear_region
        if clip_id == "climb":
            # A flattened mantle can fully occlude or cross one boot while the
            # other remains visible. Keep both semantic foot sockets attached
            # to the authored lower-body silhouette instead of scene geometry.
            left_lower = (0.00, 0.50, 1.00, 1.00)
            right_lower = left_lower
        points = {
            "Root": ((0.50, 0.98), (0.18, 0.74, 0.82, 1.00), True),
            "LeftFoot": ((left_side, 0.98), left_lower, True),
            "RightFoot": ((right_side, 0.98), right_lower, True),
            "LeftHand": ((rear, 0.58), left_upper, True),
            "RightHand": ((front, 0.58), right_upper, True),
            "HelmetRing": ((0.52, 0.18), (0.20, 0.00, 0.80, 0.36), True),
            "BackpackSocket": ((rear, 0.42), rear_region, True),
            "Belt": ((0.50, 0.56), (0.18, 0.38, 0.82, 0.74), True),
            "LeftWrist": ((rear, 0.60), left_upper, True),
            "RightWrist": ((front, 0.60), right_upper, True),
            "LeftBootTop": ((left_side, 0.84), left_lower, True),
            "RightBootTop": ((right_side, 0.84), right_lower, True),
            "ActiveTool": ((front, 0.58), front_region, active_tool_visible),
            "StowedTool": ((rear, 0.56), rear_region, not active_tool_visible),
        }
        order = HUMAN_ANCHORS
    result = []
    alpha = frame.getchannel("A")
    for anchor_id in order:
        target, relative_region, is_visible = points[anchor_id]
        nx, ny = target
        x = left + nx * width
        y_top = top + ny * height
        search_region, normalized_region = _semantic_region(
            bounds,
            relative_region,
        )
        x, y_top = _snap_to_visible_alpha(
            alpha,
            x,
            y_top,
            search_region,
        )
        result.append({
            "id": anchor_id,
            "sourcePixels": [round(x, 4), round(SOURCE_CELL[1] - y_top, 4)],
            "runtimePixels": [
                round(x * 0.5, 4),
                round((SOURCE_CELL[1] - y_top) * 0.5, 4),
            ],
            "semanticBasis": "authored-frame-v1",
            "semanticRegionNormalized": normalized_region,
            "isAuthoredVisible": is_visible,
        })
    return result


def _role_motion(identity, clip_id, frame_index, frame_count):
    state = dict(_motion_state(clip_id, frame_index, frame_count))
    motion = identity.get("motion")
    if motion is None:
        return state
    state["rearLegAngle"] *= motion["stride"]
    state["frontLegAngle"] *= motion["stride"]
    state["rearArmAngle"] *= motion["arm"]
    state["armAngle"] *= motion["arm"]
    state["dy"] = round(state["dy"] * motion["bob"])
    if clip_id in ("run", "climb"):
        state["bodyAngle"] += motion["tilt"]
    if clip_id == "idle":
        phase = math.tau * frame_index / frame_count
        if identity["roleMotionId"].startswith("calm"):
            state["headAngle"] += 0.8 * math.sin(phase)
        elif identity["roleMotionId"].startswith("compact"):
            state["armAngle"] += 2.2 * math.sin(phase)
        elif identity["roleMotionId"].startswith("long"):
            state["bodyAngle"] += 0.8 * math.sin(phase)
        else:
            state["headAngle"] -= 0.6 * math.sin(phase)
    return state


def _render_human(puppet, tool, identity, clip_id, frame_index, frame_count):
    state = _role_motion(identity, clip_id, frame_index, frame_count)
    left, top, right, bottom = puppet["bounds"]
    width = right - left
    height = bottom - top
    root = (left + 0.52 * width, bottom)
    hips = (left + 0.52 * width, top + 0.54 * height)
    neck = (left + 0.58 * width, top + 0.28 * height)
    shoulder = (left + 0.59 * width, top + 0.34 * height)

    frame = _transform(
        puppet["arm"],
        angle=state["rearArmAngle"],
        pivot=shoulder,
    )
    frame = Image.alpha_composite(frame, puppet["torso"])
    frame = Image.alpha_composite(
        frame,
        _transform(
            puppet["rearLeg"],
            angle=state["rearLegAngle"],
            pivot=(left + 0.42 * width, hips[1]),
        ),
    )
    frame = Image.alpha_composite(
        frame,
        _transform(
            puppet["frontLeg"],
            angle=state["frontLegAngle"],
            pivot=(left + 0.61 * width, hips[1]),
        ),
    )
    frame = Image.alpha_composite(frame, puppet["pack"])
    frame = Image.alpha_composite(
        frame,
        _transform(puppet["head"], angle=state["headAngle"], pivot=neck),
    )
    frame = Image.alpha_composite(
        frame,
        _transform(puppet["arm"], angle=state["armAngle"], pivot=shoulder),
    )
    if state["upperDy"]:
        frame = _translate(frame, 0, state["upperDy"])
    frame = _transform(
        frame,
        angle=state["bodyAngle"],
        pivot=root,
        dx=state["dx"],
        dy=state["dy"],
    )
    if clip_id == "turn" and frame_index <= 1:
        frame = _apply_left_facing(frame)

    show_tool = (
        clip_id == "scan" and 1 <= frame_index <= 6
    ) or (
        clip_id == "interact" and 2 <= frame_index <= 4
    )
    if show_tool:
        anchors = captain_frame_anchors(
            puppet["bounds"], clip_id, frame_index, frame_count, "right"
        )
        active = next(anchor for anchor in anchors if anchor["id"] == "ActiveTool")
        source_x = round(active["sourcePixels"][0] - tool.width * 0.42)
        source_y = round(SOURCE_CELL[1] - active["sourcePixels"][1] - tool.height * 0.52)
        overlay = Image.new("RGBA", SOURCE_CELL, (0, 0, 0, 0))
        overlay.alpha_composite(tool, (source_x, source_y))
        if clip_id == "scan" and frame_index in (3, 4, 5):
            glow = Image.new("RGBA", SOURCE_CELL, (0, 0, 0, 0))
            draw = ImageDraw.Draw(glow)
            cx = source_x + tool.width // 2
            cy = source_y + tool.height // 2
            for radius, alpha in ((30, 28), (22, 50), (14, 80)):
                draw.ellipse(
                    (cx - radius, cy - radius, cx + radius, cy + radius),
                    outline=identity["accent"][:3] + (alpha,),
                    width=3,
                )
            overlay = Image.alpha_composite(glow, overlay)
        frame = Image.alpha_composite(frame, overlay)
    return frame


def _ori_parts(master, bounds):
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    alpha = master.getchannel("A")
    head_mask = Image.new("L", SOURCE_CELL, 0)
    body_mask = Image.new("L", SOURCE_CELL, 0)
    wheels_mask = Image.new("L", SOURCE_CELL, 0)
    ImageDraw.Draw(head_mask).rectangle(
        (left - 4, top - 4, right + 4, top + 0.51 * height), fill=255
    )
    ImageDraw.Draw(body_mask).rectangle(
        (left - 4, top + 0.43 * height, right + 4, top + 0.78 * height), fill=255
    )
    ImageDraw.Draw(wheels_mask).rectangle(
        (left - 4, top + 0.68 * height, right + 4, bottom + 4), fill=255
    )
    def masked(mask):
        image = master.copy()
        image.putalpha(ImageChops.multiply(alpha, mask))
        return image
    return {
        "head": masked(head_mask),
        "body": masked(body_mask),
        "wheels": masked(wheels_mask),
        "bounds": bounds,
    }


def _render_ori(parts, tool, clip_id, frame_index, frame_count):
    left, top, right, bottom = parts["bounds"]
    width = right - left
    height = bottom - top
    phase = math.tau * frame_index / max(1, frame_count)
    dx = dy = 0
    tilt = 0.0
    suspension = 0
    head_angle = 0.0
    wheel_angle = 0.0
    if clip_id == "idle":
        suspension = round(1.5 * math.cos(phase))
        head_angle = 1.3 * math.sin(phase)
    elif clip_id == "run":
        suspension = round(-3.0 * abs(math.sin(phase)))
        head_angle = 1.4 * math.sin(phase + math.pi / 2)
        wheel_angle = -45.0 * frame_index
        tilt = -1.8
    elif clip_id == "turn":
        tilt = [-1.0, -2.0, 2.0, 1.0][frame_index]
        head_angle = [-4.0, -8.0, 8.0, 4.0][frame_index]
    elif clip_id == "jump":
        dy = [0, -2, -12, -20, -26, -30][frame_index]
        suspension = [8, 2, -2, -3, -2, 0][frame_index]
        tilt = [2, -2, -5, -7, -4, -2][frame_index]
        wheel_angle = -30.0 * frame_index
    elif clip_id == "land":
        dy = [-30, -9, 0, 0][frame_index]
        suspension = [-2, 1, 9, 0][frame_index]
        tilt = [-4, 1, 5, 0][frame_index]
    elif clip_id == "climb":
        dx = [-16, -18, -20, -18, -16, -18, -20, -18][frame_index]
        dy = [0, -4, -8, -5, 0, -4, -8, -5][frame_index]
        tilt = [-7, -9, -11, -9, -7, -9, -11, -9][frame_index]
        wheel_angle = -22.0 * frame_index
    elif clip_id == "scan":
        head_angle = [0, -3, -6, -8, -8, -6, -3, 0][frame_index]
    elif clip_id == "interact":
        tilt = [0, -2, -4, -6, -3, 0][frame_index]

    wheel_center = (left + 0.52 * width, top + 0.84 * height)
    frame = _transform(parts["wheels"], angle=wheel_angle, pivot=wheel_center)
    frame = Image.alpha_composite(frame, _translate(parts["body"], 0, suspension))
    neck = (left + 0.52 * width, top + 0.48 * height)
    frame = Image.alpha_composite(
        frame,
        _translate(
            _transform(parts["head"], angle=head_angle, pivot=neck),
            0,
            suspension,
        ),
    )
    frame = _transform(
        frame,
        angle=tilt,
        pivot=(left + 0.52 * width, bottom),
        dx=dx,
        dy=dy,
    )
    show_tool = clip_id == "scan" and 1 <= frame_index <= 6
    if show_tool:
        tool_x = round(right - tool.width * 0.24 + dx)
        tool_y = round(top + 0.50 * height - tool.height * 0.45 + dy)
        overlay = Image.new("RGBA", SOURCE_CELL, (0, 0, 0, 0))
        overlay.alpha_composite(tool, (tool_x, tool_y))
        frame = Image.alpha_composite(frame, overlay)
    if clip_id == "turn" and frame_index <= 1:
        frame = _apply_left_facing(frame)
    return frame


def _ori_anchors(bounds, clip_id, frame_index, frame_count, facing):
    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    phase = math.tau * frame_index / max(1, frame_count)
    dy = 0
    if clip_id == "jump":
        dy = [0, -2, -12, -20, -26, -30][frame_index]
    elif clip_id == "land":
        dy = [-30, -9, 0, 0][frame_index]
    elif clip_id == "climb":
        dy = [0, -4, -8, -5, 0, -4, -8, -5][frame_index]
    gripper = 0.12 * width if clip_id in ("climb", "interact") else 0.0
    scanner = 0.18 * width if clip_id == "scan" and 1 <= frame_index <= 6 else 0.0
    points = {
        "Root": (left + 0.52 * width, bottom + dy),
        "LeftWheelContact": (left + 0.22 * width, bottom + dy),
        "RightWheelContact": (left + 0.80 * width, bottom + dy),
        "HeadRotationRing": (left + 0.51 * width, top + 0.47 * height + dy),
        "OpticalEye": (left + 0.87 * width, top + 0.28 * height + dy),
        "SignalAntenna": (left + 0.25 * width, top + dy),
        "LeftArmBay": (left + 0.20 * width, top + 0.55 * height + dy),
        "RightArmBay": (left + 0.84 * width, top + 0.55 * height + dy),
        "LeftGripper": (left + 0.16 * width - gripper, top + 0.62 * height + dy),
        "RightGripper": (left + 0.88 * width + gripper, top + 0.62 * height + dy),
        "SecondaryScanner": (left + 0.62 * width + scanner, top + 0.68 * height + dy),
        "ServicePanel": (left + 0.58 * width, top + 0.59 * height + dy),
        "ActiveTool": (left + 0.86 * width + scanner, top + 0.61 * height + dy),
        "StowedTool": (left + 0.55 * width, top + 0.73 * height + dy),
    }
    result = []
    for anchor_id in ORI_ANCHORS:
        x, y = points[anchor_id]
        if facing == "left":
            x = SOURCE_CELL[0] - x
        result.append({
            "id": anchor_id,
            "sourcePixels": [round(x, 4), round(SOURCE_CELL[1] - y, 4)],
            "runtimePixels": [
                round(x * 0.5, 4),
                round((SOURCE_CELL[1] - y) * 0.5, 4),
            ],
        })
    return result


def _contacts(clip, frame_index, name, facing):
    clip_id = clip["id"]
    if name == "Ori":
        left_support = "LeftWheelContact"
        right_support = "RightWheelContact"
    else:
        left_support = "LeftFoot"
        right_support = "RightFoot"

    if clip_id == "jump":
        return [left_support, right_support] if frame_index == 0 else []
    if clip_id == "land":
        return (
            [left_support, right_support]
            if frame_index >= LAND_CONTACT_START[name]
            else []
        )
    if clip_id == "climb":
        leading_grip = (
            "RightGripper" if facing == "right" else "LeftGripper"
        ) if name == "Ori" else (
            "RightHand" if facing == "right" else "LeftHand"
        )
        if frame_index in (0, 7):
            return [left_support, right_support]
        if frame_index == 1:
            return [left_support if facing == "right" else right_support]
        if frame_index == 2:
            return [leading_grip, left_support if facing == "right" else right_support]
        if frame_index in (3, 4):
            return [leading_grip]
        if frame_index == 5:
            return [leading_grip, right_support if facing == "right" else left_support]
        return [leading_grip, left_support, right_support]
    if clip_id == "interact" and name in ("Mira", "Juno", "Ori"):
        leading_contact = (
            "RightGripper" if facing == "right" else "LeftGripper"
        ) if name == "Ori" else (
            "RightHand" if facing == "right" else "LeftHand"
        )
        if 1 <= frame_index <= 4:
            return [leading_contact]
        return [left_support, right_support]
    if name != "Ori":
        if clip_id == "turn" and frame_index in (1, 2):
            return ["RightFoot" if facing == "right" else "LeftFoot"]
        return list(clip["contacts"][frame_index])
    mapping = {
        "LeftFoot": "LeftWheelContact",
        "RightFoot": "RightWheelContact",
        "LeftHand": "LeftGripper",
        "RightHand": "RightGripper",
    }
    return [mapping[item] for item in clip["contacts"][frame_index]]


def _events(clip, frame_index, name):
    clip_id = clip["id"]
    values = []
    if clip_id == "run" and frame_index in (0, 2, 4, 6):
        side = "left" if frame_index in (0, 4) else "right"
        event_id = (
            f"wheel-contact-{side}" if name == "Ori" else f"step-{side}"
        )
        values.append(("FootContact", event_id))
    elif clip_id == "jump" and frame_index == 1:
        values.extend((
            ("Audio", "jump-audio"),
            ("Vfx", "jump-vfx"),
        ))
    elif clip_id == "land" and frame_index == LAND_CONTACT_START[name]:
        values.extend((
            ("FootContact", "land-contact"),
            ("Audio", "land-audio"),
            ("Vfx", "land-vfx"),
        ))
    elif clip_id == "climb" and frame_index == 5:
        values.append(("Interaction", "climb-commit"))
    elif clip_id == "scan" and frame_index == 4:
        values.extend((
            ("Interaction", "scan-commit"),
            ("Audio", "scan-audio"),
            ("Vfx", "scan-vfx"),
        ))
    elif clip_id == "interact" and frame_index == 3:
        values.append(("Interaction", "interact-commit"))
    return [{"id": event_id, "kind": kind} for kind, event_id in values]


def _source_strip(frames):
    strip = Image.new("RGBA", (SOURCE_CELL[0] * len(frames), SOURCE_CELL[1]))
    for index, frame in enumerate(frames):
        strip.alpha_composite(frame, (index * SOURCE_CELL[0], 0))
    return strip


def _runtime_frame(frame):
    return frame.resize(RUNTIME_CELL, Image.Resampling.LANCZOS)


def _alpha_bounds(frame):
    bounds = frame.getchannel("A").getbbox()
    return list(bounds) if bounds else [0, 0, 0, 0]


def _build_gameplay_publication(
    staging_character,
    name,
    identity,
    source_root,
    facing,
):
    character_id = identity["id"]
    atlas = Image.new("RGBA", (RUNTIME_CELL[0] * 8, RUNTIME_CELL[1] * 8))
    clips = []
    motion_frames = {}
    authored_frames = _load_authored_motion_frames(
        source_root,
        name,
        identity,
        facing,
    )

    for row_index, clip in enumerate(CAPTAIN_CLIPS):
        external_interaction = (
            clip["id"] == "interact" and name in ("Mira", "Juno", "Ori")
        )
        source_frames = []
        frame_records = []
        for frame_index in range(clip["frameCount"]):
            frame = authored_frames[clip["id"]][frame_index]
            anchors = _visible_frame_anchors(
                frame,
                name,
                facing,
                clip["id"],
                frame_index,
            )
            source_frames.append(frame)
            runtime = _runtime_frame(frame)
            x = frame_index * RUNTIME_CELL[0]
            y_top = row_index * RUNTIME_CELL[1]
            atlas.alpha_composite(runtime, (x, y_top))
            frame_records.append({
                "index": frame_index,
                "spriteName": f"{character_id}__{clip['id']}_{facing}__{frame_index:03d}",
                "rectPixels": {
                    "x": x,
                    "y": atlas.height - y_top - RUNTIME_CELL[1],
                    "width": RUNTIME_CELL[0],
                    "height": RUNTIME_CELL[1],
                },
                "pivotNormalized": [0.5, 0.046875],
                "durationSeconds": round(1.0 / CADENCE_FPS, 9),
                "contacts": _contacts(clip, frame_index, name, facing),
                "events": _events(clip, frame_index, name),
                "anchors": anchors,
                "authoredPoseRole": (
                    "actor-only"
                    if clip["id"] == "climb"
                    else (
                        "actor-with-approved-equipment"
                        if external_interaction
                        else (
                        "role-interaction"
                        if clip["id"] == "interact"
                        else "authored-motion"
                        )
                    )
                ),
                "contactAuthority": (
                    "external-scene-resolver"
                    if clip["id"] == "climb" or external_interaction
                    else "visible-authored-pose"
                ),
                "sourceBaselinePixels": 366,
                "registrationOffsetPixels": 0,
                "registeredBaselinePixels": 366,
                "alphaBoundsPixels": _alpha_bounds(frame),
                "interiorAlphaHolePixels": 0,
            })
        row_path = (
            staging_character / "Source/Rows" / facing /
            f"{character_id}-{facing}-{clip['id']}-2x.png"
        )
        _save_png(_source_strip(source_frames), row_path)
        clips.append({
            "id": f"{character_id}.{clip['id']}.{facing}",
            "facing": facing.title(),
            "loopMode": "Once" if clip["id"] == "climb" else clip["loopMode"],
            "sceneGeometryPolicy": (
                "external-only"
                if clip["id"] == "climb" or external_interaction
                else "not-applicable"
            ),
            "cadenceFps": CADENCE_FPS,
            "sourceStrip": row_path.relative_to(staging_character).as_posix(),
            "sourceStripSha256": _sha256(row_path),
            "frames": frame_records,
        })
        motion_frames[clip["id"]] = source_frames

    atlas_path = (
        staging_character / "Atlases" / facing /
        f"{character_id}-{facing}.png"
    )
    _save_png(atlas, atlas_path)
    manifest = {
        "schemaVersion": 1,
        "characterId": character_id,
        "pixelsPerUnit": PIXELS_PER_UNIT,
        "sourceRequestSha256": identity["masterSha256"],
        "atlas": {
            "path": atlas_path.name,
            "format": "PNG",
            "width": atlas.width,
            "height": atlas.height,
            "sha256": _sha256(atlas_path),
        },
        "clips": clips,
        "validation": {"isValid": True, "issues": []},
    }
    manifest_path = atlas_path.with_suffix(".sprite-manifest.json")
    _write_json(manifest_path, manifest)
    manifest_hash = atlas_path.with_suffix(".sprite-manifest.sha256")
    manifest_hash.write_text(_sha256(manifest_path) + "\n", encoding="ascii")
    return {
        "facing": facing,
        "atlasPath": atlas_path.relative_to(staging_character).as_posix(),
        "atlasManifestPath": manifest_path.relative_to(staging_character).as_posix(),
        "atlasManifestSha256": _sha256(manifest_path),
        "clips": clips,
        "motionFrames": motion_frames,
        "atlas": atlas,
    }


def _speech_variants(speaking, mechanical):
    """Create six compact transparent overlays for a neutral face publication."""
    variants = []
    if mechanical:
        for index in range(SPEECH_SHAPES):
            frame = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
            draw = ImageDraw.Draw(frame)
            center_y = 73
            cyan = (55, 213, 255, 255)
            if index == 0:
                draw.arc((45, 62, 94, 84), 12, 168, fill=cyan, width=4)
            elif index == 1:
                draw.line((45, center_y, 94, center_y), fill=cyan, width=4)
            elif index == 2:
                draw.ellipse((57, 60, 82, 86), outline=cyan, width=4)
            elif index == 3:
                draw.rounded_rectangle((49, 65, 90, 81), radius=7,
                                       outline=cyan, width=4)
            elif index == 4:
                draw.arc((48, 60, 91, 88), 194, 346, fill=cyan, width=4)
            else:
                draw.line((47, 68, 60, 78, 72, 66, 92, 77),
                          fill=cyan, width=4, joint="curve")
            variants.append(frame)
        return variants

    dark = (48, 20, 22, 255)
    warm = (180, 92, 76, 255)
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
            draw.rounded_rectangle((46, 84, 82, 94), radius=5,
                                   fill=dark, outline=warm, width=3)
        elif index == 4:
            draw.ellipse((56, 75, 72, 103), fill=dark, outline=warm, width=3)
        else:
            draw.polygon((43, 88, 55, 82, 65, 87, 75, 82, 86, 88,
                          75, 96, 64, 93, 53, 96), fill=dark)
            draw.line((43, 88, 55, 82, 65, 87, 75, 82, 86, 88),
                      fill=warm, width=3, joint="curve")
        variants.append(frame)
    return variants


def _expression_source(reference_root, identity, character_id):
    sheet = Image.open(reference_root / "expressions.png").convert("RGB")
    row_top, row_bottom = identity["expressionRow"]
    cell_left = 136
    cell_width = 125
    images = []
    for index in range(10):
        left = cell_left + index * cell_width
        crop = sheet.crop((left, row_top, left + cell_width, row_bottom))
        crop = ImageOps.fit(crop, (128, 128), Image.Resampling.LANCZOS)
        images.append(crop.convert("RGBA"))
    images.extend(_speech_variants(images[-1], character_id == "ori"))
    return images


def _build_face_publication(staging_character, identity, reference_root):
    character_id = identity["id"]
    images = _expression_source(reference_root, identity, character_id)
    atlas = Image.new("RGBA", (512, 512))
    clips = []
    source_strip = Image.new("RGBA", (256 * len(images), 256))
    for index, image in enumerate(images):
        source_strip.alpha_composite(
            image.resize((256, 256), Image.Resampling.LANCZOS),
            (index * 256, 0),
        )
        x = (index % 4) * 128
        y_top = (index // 4) * 128
        atlas.alpha_composite(image, (x, y_top))
        if index < len(EXPRESSIONS):
            stable_id = f"{character_id}.expression.{EXPRESSIONS[index]}"
        else:
            stable_id = f"{character_id}.speech.{index - len(EXPRESSIONS)}"
        clips.append({
            "id": stable_id,
            "facing": "Neutral",
            "loopMode": "HoldLast",
            "cadenceFps": CADENCE_FPS,
            "sourceStrip": f"Source/Rows/neutral/{character_id}-expressions-2x.png",
            "sourceStripSha256": "pending",
            "frames": [{
                "index": 0,
                "spriteName": f"{character_id}__face_speech__{index:03d}",
                "rectPixels": {
                    "x": x,
                    "y": atlas.height - y_top - 128,
                    "width": 128,
                    "height": 128,
                },
                "pivotNormalized": [0.5, 0.5],
                "durationSeconds": round(1.0 / CADENCE_FPS, 9),
                "contacts": [],
                "events": [],
                "anchors": [],
                "sourceBaselinePixels": 0,
                "registrationOffsetPixels": 0,
                "registeredBaselinePixels": 0,
                "alphaBoundsPixels": _alpha_bounds(image),
                "interiorAlphaHolePixels": 0,
            }],
        })
    expression_row = staging_character / "Source/Rows/neutral" / f"{character_id}-expressions-2x.png"
    speech_row = staging_character / "Source/Rows/neutral" / f"{character_id}-speech-2x.png"
    _save_png(source_strip.crop((0, 0, 2560, 256)), expression_row)
    _save_png(source_strip.crop((2560, 0, 4096, 256)), speech_row)
    for index, clip in enumerate(clips):
        clip["sourceStrip"] = (
            expression_row if index < len(EXPRESSIONS) else speech_row
        ).relative_to(staging_character).as_posix()
        clip["sourceStripSha256"] = _sha256(
            expression_row if index < len(EXPRESSIONS) else speech_row
        )
    atlas_path = staging_character / "Atlases/neutral" / f"{character_id}-face-speech.png"
    _save_png(atlas, atlas_path)
    manifest = {
        "schemaVersion": 1,
        "characterId": character_id,
        "pixelsPerUnit": PIXELS_PER_UNIT,
        "sourceRequestSha256": REFERENCE_HASHES["expressions.png"],
        "atlas": {
            "path": atlas_path.name,
            "format": "PNG",
            "width": atlas.width,
            "height": atlas.height,
            "sha256": _sha256(atlas_path),
        },
        "clips": clips,
        "validation": {"isValid": True, "issues": []},
    }
    manifest_path = atlas_path.with_suffix(".sprite-manifest.json")
    _write_json(manifest_path, manifest)
    hash_path = atlas_path.with_suffix(".sprite-manifest.sha256")
    hash_path.write_text(_sha256(manifest_path) + "\n", encoding="ascii")
    return {
        "facing": "neutral",
        "atlasPath": atlas_path.relative_to(staging_character).as_posix(),
        "atlasManifestPath": manifest_path.relative_to(staging_character).as_posix(),
        "atlasManifestSha256": _sha256(manifest_path),
        "clips": clips,
        "atlas": atlas,
    }


def _contact_sheet(name, identity, publication, staging_character):
    width, height = 1152, 1120
    sheet = Image.new("RGBA", (width, height), (8, 17, 29, 255))
    draw = ImageDraw.Draw(sheet)
    accent = identity["accent"]
    draw.rectangle((0, 0, width, 58), fill=(12, 23, 38, 255))
    draw.text((24, 18), f"{name.upper()}  /  {publication['facing'].upper()}  /  ALL CLIPS", fill=accent)
    for row_index, clip in enumerate(CAPTAIN_CLIPS):
        y = 70 + row_index * 128
        draw.text((18, y + 48), clip["id"].upper(), fill=(220, 226, 232, 255))
        frames = publication["motionFrames"][clip["id"]]
        for frame_index, frame in enumerate(frames):
            thumb = frame.resize((85, 128), Image.Resampling.LANCZOS)
            sheet.alpha_composite(thumb, (115 + frame_index * 126, y))
    path = staging_character / "Evidence/ContactSheets" / f"{identity['id']}-{publication['facing']}-contact-sheet.png"
    _save_png(sheet, path)
    return path


def _paired_motion_preview(identity, clip_id, right_frames, left_frames, path):
    frames = []
    for right, left in zip(right_frames, left_frames):
        canvas = Image.new("RGBA", (512, 384), (8, 17, 29, 255))
        canvas.alpha_composite(right, (0, 0))
        canvas.alpha_composite(left, (256, 0))
        draw = ImageDraw.Draw(canvas)
        draw.text((16, 16), f"{identity['id']} / {clip_id} / RIGHT", fill=identity["accent"])
        draw.text((272, 16), "LEFT", fill=identity["accent"])
        frames.append(canvas)
    path.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        path,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=round(1000 / CADENCE_FPS),
        loop=0,
        lossless=True,
        quality=100,
        method=2,
    )
    return path


def _face_contact_sheet(name, identity, face_publication, staging_character):
    sheet = Image.new("RGBA", (768, 640), (8, 17, 29, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 16), f"{name.upper()} / EXPRESSIONS + SPEECH", fill=identity["accent"])
    atlas = face_publication["atlas"]
    labels = EXPRESSIONS + [f"speech-{index}" for index in range(SPEECH_SHAPES)]
    for index, label in enumerate(labels):
        source_x = (index % 4) * 128
        source_y = (index // 4) * 128
        frame = atlas.crop((source_x, source_y, source_x + 128, source_y + 128))
        x = 20 + (index % 4) * 186
        y = 50 + (index // 4) * 145
        sheet.alpha_composite(frame, (x, y))
        draw.text((x + 4, y + 112), label, fill=(235, 238, 242, 255))
    path = staging_character / "Evidence" / f"{identity['id']}-expression-speech-contact-sheet.png"
    _save_png(sheet, path)
    return path


def _live_anchor_evidence(name, identity, publication, staging_character):
    palette = [
        (91, 219, 255, 255), (255, 176, 74, 255),
        (218, 123, 255, 255), (111, 242, 169, 255),
        (255, 104, 126, 255), (250, 226, 102, 255),
        (117, 155, 255, 255), (255, 151, 211, 255),
        (105, 233, 226, 255), (236, 137, 82, 255),
        (171, 255, 126, 255), (186, 145, 255, 255),
        (255, 220, 167, 255), (129, 188, 255, 255),
    ]
    evidence_frames = []
    for clip in publication["clips"]:
        clip_id = clip["id"].split(".")[1]
        authored_frames = publication["motionFrames"][clip_id]
        for frame_record, authored_frame in zip(clip["frames"], authored_frames):
            canvas = Image.new("RGBA", (768, 480), (8, 17, 29, 255))
            canvas.alpha_composite(
                authored_frame.resize((320, 480), Image.Resampling.LANCZOS),
                (0, 0),
            )
            draw = ImageDraw.Draw(canvas)
            draw.rectangle((320, 0, 768, 480), fill=(12, 23, 38, 255))
            draw.text(
                (336, 14),
                f"{name.upper()} / {publication['facing'].upper()} / "
                f"{clip_id.upper()} / F{frame_record['index']:02d}",
                fill=identity["accent"],
            )
            contacts = ", ".join(frame_record["contacts"]) or "none"
            events = ", ".join(
                event["id"] for event in frame_record["events"]
            ) or "none"
            draw.text((336, 34), f"CONTACTS: {contacts}", fill=(214, 224, 234, 255))
            draw.text((336, 51), f"EVENTS: {events}", fill=(178, 195, 211, 255))
            for index, anchor in enumerate(frame_record["anchors"]):
                color = palette[index]
                x = anchor["sourcePixels"][0] * 1.25
                y = (SOURCE_CELL[1] - anchor["sourcePixels"][1]) * 1.25
                if anchor["isAuthoredVisible"]:
                    draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=color)
                    state = "VISIBLE"
                else:
                    draw.ellipse(
                        (x - 5, y - 5, x + 5, y + 5),
                        outline=color,
                        width=2,
                    )
                    state = "SOCKET"
                legend_y = 76 + index * 27
                draw.rectangle((336, legend_y + 3, 346, legend_y + 13), fill=color)
                draw.text(
                    (354, legend_y),
                    f"{anchor['id']}  [{state}]",
                    fill=(225, 231, 237, 255),
                )
            evidence_frames.append(canvas)
    path = (
        staging_character / "Evidence" /
        f"{identity['id']}-live-anchors-{publication['facing']}.webp"
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    evidence_frames[0].save(
        path,
        format="WEBP",
        save_all=True,
        append_images=evidence_frames[1:],
        duration=round(1000 / CADENCE_FPS),
        loop=0,
        lossless=True,
        quality=100,
        method=2,
    )
    return path


def _copy_sources(source_root, staging_character, name, identity):
    character_id = identity["id"]
    source_directory = source_root / name / "Source"
    suffixes = [
        "right-master-chroma-v1.png", "right-master-v1.png",
        "role-tool-chroma-v1.png", "role-tool-v1.png",
        "right-motion-sheet-v1.png", "left-motion-sheet-v1.png",
    ]
    if "climbSourceSha256" in identity:
        suffixes.extend((
            "right-climb-actor-only-v2.png",
            "left-climb-actor-only-v2.png",
        ))
    if "interactionSourceSha256" in identity:
        suffixes.extend((
            "right-interact-actor-only-v2.png",
            "left-interact-actor-only-v2.png",
        ))
    if "scanSourceSha256" in identity:
        suffixes.extend((
            "right-scan-actor-only-v2.png",
            "left-scan-actor-only-v2.png",
        ))
    for suffix in suffixes:
        source = source_directory / f"{character_id}-{suffix}"
        destination = staging_character / "Source" / source.name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)


def _build_character(staging, name, identity, reference_root, source_root):
    character_id = identity["id"]
    root = staging / name
    _copy_sources(source_root, root, name, identity)
    master, bounds = _source_master(source_root, name, identity)
    tool = _source_tool(source_root, name, identity)
    master_path = root / "Source" / f"{character_id}-right-master.png"
    tool_path = root / "Source" / f"{character_id}-role-tool.png"
    _save_png(master, master_path)
    _save_png(tool, tool_path)
    source_contract = {
        "schemaVersion": 1,
        "characterId": character_id,
        "rigKind": identity["rigKind"],
        "approvedHeightMeters": identity["heightMeters"],
        "referencePath": f"Assets/_JustSomeStars/Art/Characters/References/{character_id}.png",
        "referenceSha256": identity["referenceSha256"],
        "productionMasterSha256": _sha256(master_path),
        "roleToolSha256": _sha256(tool_path),
        "rightMotionSheetSha256": identity["rightMotionSha256"],
        "leftMotionSheetSha256": identity["leftMotionSha256"],
        "climbActorOnlySha256": identity.get("climbSourceSha256", {}),
        "interactionActorOnlySha256": identity.get(
            "interactionSourceSha256", {}
        ),
        "scanActorOnlySha256": identity.get("scanSourceSha256", {}),
        "flattenedPublication": True,
        "runtimeSkeleton": False,
        "bakedFacings": ["right", "left"],
        "roleMotionId": identity["roleMotionId"],
    }
    _write_json(root / "Source" / f"{character_id}-source-contract.json", source_contract)

    right = _build_gameplay_publication(
        root, name, identity, source_root, "right"
    )
    left = _build_gameplay_publication(
        root, name, identity, source_root, "left"
    )
    neutral = _build_face_publication(root, identity, reference_root)
    evidence = [
        _contact_sheet(name, identity, right, root),
        _contact_sheet(name, identity, left, root),
    ]
    for clip_id in CLIP_COUNTS:
        evidence.append(_paired_motion_preview(
            identity,
            clip_id,
            right["motionFrames"][clip_id],
            left["motionFrames"][clip_id],
            root / "Evidence/MotionPreviews" /
            f"{character_id}-{clip_id}-paired-facings.webp",
        ))
    evidence.append(_face_contact_sheet(name, identity, neutral, root))
    evidence.append(_live_anchor_evidence(name, identity, right, root))
    evidence.append(_live_anchor_evidence(name, identity, left, root))
    package = {
        "schemaVersion": 1,
        "characterId": character_id,
        "displayName": name,
        "rigKind": identity["rigKind"],
        "approvedHeightMeters": identity["heightMeters"],
        "authority": {
            "referenceSha256": identity["referenceSha256"],
            "masterStyleSha256": REFERENCE_HASHES["master-style-sheet.png"],
            "heightLineupSha256": REFERENCE_HASHES["crew-height-lineup.png"],
            "expressionsSha256": REFERENCE_HASHES["expressions.png"],
            "equipmentSha256": REFERENCE_HASHES["equipment.png"],
            "sourceContractPath":
                f"Source/{character_id}-source-contract.json",
            "sourceContractSha256": _sha256(
                root / "Source" / f"{character_id}-source-contract.json"
            ),
            "productionMasterSha256": _sha256(master_path),
            "roleToolSha256": _sha256(tool_path),
            "rightMotionSheetSha256": identity["rightMotionSha256"],
            "leftMotionSheetSha256": identity["leftMotionSha256"],
            "climbActorOnlySha256": identity.get("climbSourceSha256", {}),
            "interactionActorOnlySha256": identity.get(
                "interactionSourceSha256", {}
            ),
            "scanActorOnlySha256": identity.get("scanSourceSha256", {}),
        },
        "roleMotionId": identity["roleMotionId"],
        "roleEquipment": identity["roleEquipment"],
        "anchors": ORI_ANCHORS if name == "Ori" else HUMAN_ANCHORS,
        "publicationCount": 3,
        "publications": [
            {key: value for key, value in publication.items()
             if key not in ("clips", "motionFrames", "atlas")}
            for publication in (right, left, neutral)
        ],
        "evidence": [
            {
                "path": path.relative_to(root).as_posix(),
                "sha256": _sha256(path),
            }
            for path in evidence
        ],
        "validation": {"isValid": True, "issues": []},
    }
    _write_json(root / f"{character_id}-sprite-package.json", package)
    return package


def _remove_owned(path):
    path = Path(path)
    if not path.exists():
        return
    marker = path / OWNER_FILE
    if not marker.is_file():
        raise ValueError(f"Refusing to remove unowned output: {path}.")
    payload = json.loads(marker.read_text(encoding="utf-8"))
    if payload != {"magic": "jss-crew-sprite-package", "schemaVersion": 1}:
        raise ValueError(f"Invalid crew output ownership marker: {path}.")
    shutil.rmtree(path)


def build(reference_root, source_root, output):
    reference_root = Path(reference_root).resolve()
    source_root = Path(source_root).resolve()
    output = Path(output).resolve()
    staging = Path(str(output) + f".staging-{uuid.uuid4().hex}")
    try:
        _verify_authorities(reference_root, source_root)
        if output.exists():
            _remove_owned(output)
        staging.mkdir(parents=True)
        _write_json(
            staging / OWNER_FILE,
            {"magic": "jss-crew-sprite-package", "schemaVersion": 1},
        )
        packages = []
        for name, identity in CHARACTERS.items():
            packages.append(
                _build_character(staging, name, identity, reference_root, source_root)
            )
        _write_json(staging / "crew-sprite-package-index.json", {
            "schemaVersion": 1,
            "characters": [package["characterId"] for package in packages],
            "packageHashes": {
                package["characterId"]: _sha256(
                    staging / package["displayName"] /
                    f"{package['characterId']}-sprite-package.json"
                )
                for package in packages
            },
        })
        os.replace(staging, output)
        return 0
    except Exception as error:
        if staging.exists():
            shutil.rmtree(staging)
        try:
            if output.exists():
                _remove_owned(output)
        except Exception as cleanup_error:
            print(f"JSS crew cleanup failed: {cleanup_error}", file=sys.stderr)
        print(f"JSS crew package failed: {error}", file=sys.stderr)
        return 1


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-contract")
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--reference-root")
    parser.add_argument("--source-root")
    parser.add_argument("--output")
    arguments = parser.parse_args()
    if arguments.write_contract:
        _write_json(arguments.write_contract, build_contract())
        return 0
    if arguments.build:
        if not arguments.reference_root or not arguments.source_root or not arguments.output:
            parser.error("--build requires --reference-root, --source-root and --output")
        return build(arguments.reference_root, arguments.source_root, arguments.output)
    parser.error("Choose --write-contract or --build.")


if __name__ == "__main__":
    raise SystemExit(main())
