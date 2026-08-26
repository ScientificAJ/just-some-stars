#!/usr/bin/env python3
"""Fail-closed orchestrator for coherent-strip sprite-atlas production."""

import argparse
import json
import os
import shutil
import sys
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

from assemble_character_atlas import assemble_atlas
from extract_animation_strip import extract_clip_frames, resolve_source
from render_animation_preview import render_evidence
from validate_animation_frames import validate_and_register


OWNER_FILENAME = ".jss-sprite-pipeline-owner.json"
OWNER_MAGIC = "just-some-stars-sprite-pipeline"
OWNER_SCHEMA_VERSION = 1


def run_pipeline(request_path, output_root):
    request_path = Path(request_path).resolve()
    output_root = Path(output_root).resolve()
    staging_root = output_root.parent / f".{output_root.name}.staging"

    try:
        _require_target_outside_protected(output_root, [request_path])
        _require_target_outside_protected(staging_root, [request_path])
        output_owner = _inspect_owned_target(output_root)
        staging_owner = _inspect_owned_target(staging_root)
    except Exception as error:
        print(f"JSS sprite pipeline failed: {error}", file=sys.stderr, flush=True)
        return 1

    requested_character_id = None
    try:
        request = json.loads(request_path.read_text(encoding="utf-8"))
        if isinstance(request, dict) and _is_safe_character_id(
            request.get("characterId")
        ):
            requested_character_id = request["characterId"]
        _validate_request(request)
        protected = [request_path]
        protected.extend(
            resolve_source(request_path, clip["sourceStrip"])
            for clip in request["clips"]
        )
        _require_target_outside_protected(output_root, protected)
        _require_target_outside_protected(staging_root, protected)
        _require_matching_owner(output_root, output_owner, request["characterId"])
        _require_matching_owner(staging_root, staging_owner, request["characterId"])
    except Exception as error:
        _remove_owned_for_request(
            output_root,
            output_owner,
            requested_character_id,
        )
        _remove_owned_for_request(
            staging_root,
            staging_owner,
            requested_character_id,
        )
        print(f"JSS sprite pipeline failed: {error}", file=sys.stderr, flush=True)
        return 1

    _remove_verified_owned(output_root, output_owner)
    _remove_verified_owned(staging_root, staging_owner)
    try:
        staging_root.mkdir(parents=True)
        _write_owner_marker(staging_root, request["characterId"])
        rows = []
        for clip in request["clips"]:
            source_path, frames = extract_clip_frames(request_path, request, clip)
            registered, diagnostics = validate_and_register(request, clip, frames)
            rows.append(
                {
                    "clip": clip,
                    "sourcePath": source_path,
                    "frames": registered,
                    "diagnostics": diagnostics,
                }
            )
        _, manifest_path, manifest = assemble_atlas(
            request_path,
            request,
            rows,
            staging_root,
        )
        render_evidence(request, rows, staging_root)
        if output_root.exists():
            raise RuntimeError("Canonical output unexpectedly appeared during staging.")
        os.replace(staging_root, output_root)
        print(
            json.dumps(
                {
                    "status": "ok",
                    "characterId": manifest["characterId"],
                    "manifest": str(output_root / manifest_path.name),
                },
                sort_keys=True,
            ),
            flush=True,
        )
        return 0
    except Exception as error:
        _remove_if_owned_by(output_root, request["characterId"])
        _remove_if_owned_by(staging_root, request["characterId"])
        print(f"JSS sprite pipeline failed: {error}", file=sys.stderr, flush=True)
        return 1


def _validate_request(request):
    if request.get("schemaVersion") != 1:
        raise ValueError("Unsupported sprite request schemaVersion.")
    character_id = request.get("characterId")
    if not _is_safe_character_id(character_id):
        raise ValueError("characterId must be a safe non-empty identifier.")
    for field in ("pixelsPerUnit", "atlasColumns", "frameWidth", "frameHeight"):
        value = request.get(field)
        if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
            raise ValueError(f"{field} must be a positive integer.")
    repair = request.get("repair")
    if repair != {"mode": "complete-rows-only"}:
        raise ValueError(
            "Repairs must replace a complete-row; partial frame repair is forbidden."
        )
    clips = request.get("clips")
    if not isinstance(clips, list) or not clips:
        raise ValueError("At least one complete clip row is required.")
    identifiers = [clip.get("id") for clip in clips]
    if any(not isinstance(value, str) or not value for value in identifiers):
        raise ValueError("Every clip row needs a stable id.")
    if len(set(identifiers)) != len(identifiers):
        raise ValueError("Clip ids must be unique.")
    for clip in clips:
        if clip.get("loopMode") not in {"Loop", "Once", "HoldLast"}:
            raise ValueError(f"Unsupported loopMode for {clip['id']}.")
        cadence = clip.get("cadenceFps")
        if not isinstance(cadence, int) or not 1 <= cadence <= 30:
            raise ValueError(f"Invalid cadenceFps for {clip['id']}.")
        pivot = clip.get("pivotPixels")
        if not isinstance(pivot, list) or len(pivot) != 2:
            raise ValueError(f"Invalid pivotPixels for {clip['id']}.")


def _remove(path):
    if path.is_symlink() or path.is_file():
        path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)


def _expected_output_names(character_id):
    return {
        OWNER_FILENAME,
        f"{character_id}.png",
        f"{character_id}.sprite-manifest.json",
        f"{character_id}.sprite-manifest.sha256",
        f"{character_id}-contact-sheet.png",
        f"{character_id}-preview.webp",
    }


def _require_target_outside_protected(path, protected_paths):
    if any(path == protected or path in protected.parents for protected in protected_paths):
        raise ValueError(
            f"Output target {path} contains request/source data and is not owned."
        )


def _inspect_owned_target(path):
    if not path.exists():
        return None
    if path.is_symlink() or not path.is_dir():
        raise ValueError(f"Output target {path} is not an owned pipeline directory.")
    marker_path = path / OWNER_FILENAME
    if marker_path.is_symlink() or not marker_path.is_file():
        raise ValueError(
            f"Output target {path} is not owned by this pipeline; "
            f"{OWNER_FILENAME} is missing."
        )
    try:
        marker = json.loads(marker_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(
            f"Output target {path} has an invalid pipeline ownership marker."
        ) from error
    if set(marker) != {"magic", "schemaVersion", "characterId"}:
        raise ValueError(
            f"Output target {path} has an invalid pipeline ownership marker."
        )
    if (
        marker.get("magic") != OWNER_MAGIC
        or marker.get("schemaVersion") != OWNER_SCHEMA_VERSION
    ):
        raise ValueError(
            f"Output target {path} has an invalid pipeline ownership marker."
        )
    character_id = marker.get("characterId")
    if not _is_safe_character_id(character_id):
        raise ValueError(
            f"Output target {path} has an invalid pipeline ownership marker."
        )
    expected_names = _expected_output_names(character_id)
    unexpected = [
        child.name
        for child in path.iterdir()
        if child.is_dir() or child.name not in expected_names
    ]
    if unexpected:
        raise ValueError(
            f"Output target {path} is not owned by this pipeline; "
            f"unexpected entries: {sorted(unexpected)}."
        )
    return character_id


def _require_matching_owner(path, owner, character_id):
    if owner is not None and owner != character_id:
        raise ValueError(
            f"Output target {path} is owned by character {owner!r}, "
            f"not {character_id!r}."
        )


def _write_owner_marker(path, character_id):
    (path / OWNER_FILENAME).write_text(
        json.dumps(
            {
                "magic": OWNER_MAGIC,
                "schemaVersion": OWNER_SCHEMA_VERSION,
                "characterId": character_id,
            },
            indent=2,
            sort_keys=True,
        ) + "\n",
        encoding="utf-8",
    )


def _remove_verified_owned(path, owner):
    if owner is not None:
        _remove(path)


def _remove_owned_for_request(path, owner, requested_character_id):
    if owner is not None and (
        requested_character_id is None or owner == requested_character_id
    ):
        _remove(path)


def _remove_if_owned_by(path, character_id):
    try:
        owner = _inspect_owned_target(path)
    except ValueError:
        return
    if owner == character_id:
        _remove(path)


def _is_safe_character_id(character_id):
    return isinstance(character_id, str) and bool(character_id) and not any(
        part in character_id for part in ("/", "\\", "..")
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--request", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    arguments = parser.parse_args()
    return run_pipeline(arguments.request, arguments.output)


if __name__ == "__main__":
    raise SystemExit(main())
