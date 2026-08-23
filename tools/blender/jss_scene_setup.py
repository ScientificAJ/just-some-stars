"""Canonical Blender scene policy for Just Some Stars character assets."""

from __future__ import annotations

import json
import re
from collections.abc import Iterable

import bpy


SCHEMA_VERSION = 1
EXPORT_COLLECTION = "JSS_EXPORT"
LOD_COLLECTIONS = ("JSS_LOD0", "JSS_LOD1", "JSS_LOD2")
SUPPORTED_RIG_KINDS = ("Generic", "Humanoid")
ASSET_NAME_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")


def configure_scene(asset_name: str, rig_kind: str, expected_bones: Iterable[str]):
    """Apply the deterministic character-scene contract without deleting art."""
    if not isinstance(asset_name, str) or not ASSET_NAME_PATTERN.fullmatch(asset_name):
        raise ValueError(
            "asset_name must be lowercase kebab-case, for example 'mira' or "
            "'task11-primitive'."
        )
    if rig_kind not in SUPPORTED_RIG_KINDS:
        raise ValueError(
            f"rig_kind must be one of {SUPPORTED_RIG_KINDS}; got {rig_kind!r}."
        )
    normalized_bones = _normalize_bones(expected_bones)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    scene.frame_start = 1
    scene.frame_end = 31
    scene["jss_schema_version"] = SCHEMA_VERSION
    scene["jss_asset_name"] = asset_name
    scene["jss_rig_kind"] = rig_kind
    scene["jss_expected_bones"] = json.dumps(normalized_bones, separators=(",", ":"))
    scene["jss_root_motion_bone"] = "Root"
    scene["jss_forward_marker"] = "SOCKET_Forward"
    scene["jss_export_forward_axis"] = "-Z"
    scene["jss_export_up_axis"] = "Y"

    export = _ensure_collection(EXPORT_COLLECTION, scene.collection)
    for collection_name in LOD_COLLECTIONS:
        lod = _ensure_collection(collection_name, export)
        _unlink_child_if_present(scene.collection, lod)
    return scene


def require_collection(name: str):
    collection = bpy.data.collections.get(name)
    if collection is None:
        raise KeyError(f"Required collection {name!r} does not exist.")
    return collection


def iter_collection_objects(collection, recursive: bool = True):
    seen = set()

    def visit(current):
        for obj in current.objects:
            if obj.name not in seen:
                seen.add(obj.name)
                yield obj
        if recursive:
            for child in current.children:
                yield from visit(child)

    yield from visit(collection)


def expected_bones(scene=None):
    target_scene = scene or bpy.context.scene
    raw = target_scene.get("jss_expected_bones", "[]")
    try:
        parsed = json.loads(raw)
    except (TypeError, json.JSONDecodeError) as exc:
        raise ValueError("jss_expected_bones must be a JSON string array.") from exc
    return _normalize_bones(parsed)


def _normalize_bones(expected_bones_value):
    if expected_bones_value is None:
        raise ValueError("expected_bones is required.")
    normalized = []
    for bone_name in expected_bones_value:
        if not isinstance(bone_name, str) or not bone_name.strip():
            raise ValueError("Every expected bone name must be nonempty.")
        if bone_name != bone_name.strip():
            raise ValueError("Expected bone names cannot contain outer whitespace.")
        normalized.append(bone_name)
    if not normalized:
        raise ValueError("At least one expected bone is required.")
    if len(normalized) != len(set(normalized)):
        raise ValueError("Expected bone names must be unique.")
    return sorted(normalized)


def _ensure_collection(name, parent):
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
    if collection.name not in {child.name for child in parent.children}:
        parent.children.link(collection)
    return collection


def _unlink_child_if_present(parent, child):
    if child.name in {candidate.name for candidate in parent.children}:
        parent.children.unlink(child)
