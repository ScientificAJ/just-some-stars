"""Transactional Blender-to-Unity character FBX export."""

from __future__ import annotations

import hashlib
import json
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector

import jss_scene_setup
import validate_character


REPORT_SCHEMA_VERSION = 1
REPORT_SUFFIX = ".jss-character.json"
PROJECT_ROOT = Path(__file__).resolve().parents[2]


def export_character(output_directory=None, basename=None):
    scene = bpy.context.scene
    source_path = _require_saved_source()
    output_dir = Path(output_directory) if output_directory else _default_output_dir(source_path)
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    output_name = basename or scene.get("jss_asset_name")
    if not output_name or not jss_scene_setup.ASSET_NAME_PATTERN.fullmatch(output_name):
        raise ValueError("Export basename must be lowercase kebab-case.")

    fbx_path = output_dir / f"{output_name}.fbx"
    report_path = output_dir / f"{output_name}{REPORT_SUFFIX}"
    temporary_fbx = output_dir / f".{output_name}.tmp.fbx"
    temporary_report = output_dir / f".{output_name}.tmp.json"
    transaction_paths = (fbx_path, report_path, temporary_fbx, temporary_report)
    _remove_paths(transaction_paths)

    try:
        if bpy.data.is_dirty:
            raise RuntimeError(
                "Save the .blend after every in-memory change before export."
            )
        validation = validate_character.validate_or_raise(scene)
        export_objects = _export_objects()
        _export_fbx(temporary_fbx, export_objects)
        if not temporary_fbx.is_file() or temporary_fbx.stat().st_size == 0:
            raise RuntimeError("Blender did not create a nonempty FBX export.")
        report = _build_report(
            scene,
            source_path,
            fbx_path,
            temporary_fbx,
            validation,
        )
        temporary_report.write_text(
            json.dumps(report, indent=2, sort_keys=False) + "\n",
            encoding="utf-8",
        )
        os.replace(temporary_fbx, fbx_path)
        try:
            os.replace(temporary_report, report_path)
        except Exception:
            fbx_path.unlink(missing_ok=True)
            raise
        return report
    except Exception:
        _remove_paths(transaction_paths)
        raise


def _require_saved_source():
    if not bpy.data.filepath:
        raise RuntimeError("Save the .blend source before exporting.")
    source = Path(bpy.data.filepath).resolve()
    if not source.is_file() or source.suffix.lower() != ".blend":
        raise RuntimeError(f"The active Blender source is not a saved .blend: {source}")
    return source


def _default_output_dir(source_path):
    source_root = PROJECT_ROOT / "Assets" / "_JustSomeStars" / "Art" / "Characters" / "Source"
    export_root = PROJECT_ROOT / "Assets" / "_JustSomeStars" / "Art" / "Characters" / "Export"
    try:
        relative_parent = source_path.parent.relative_to(source_root)
    except ValueError as exc:
        raise RuntimeError(
            f"Character sources must live under {source_root}; got {source_path}."
        ) from exc
    return export_root / relative_parent


def _export_objects():
    export_collection = jss_scene_setup.require_collection(
        jss_scene_setup.EXPORT_COLLECTION
    )
    objects = sorted(
        jss_scene_setup.iter_collection_objects(export_collection),
        key=lambda obj: obj.name,
    )
    if not objects:
        raise RuntimeError("The character export collection is empty.")
    return objects


def _export_fbx(path, objects):
    prior_selected = list(bpy.context.selected_objects)
    prior_active = bpy.context.view_layer.objects.active
    try:
        bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.hide_set(False)
            obj.hide_viewport = False
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        result = bpy.ops.export_scene.fbx(
            filepath=str(path),
            check_existing=False,
            use_selection=True,
            object_types={"ARMATURE", "MESH", "EMPTY"},
            global_scale=1.0,
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            use_space_transform=True,
            bake_space_transform=False,
            axis_forward="-Z",
            axis_up="Y",
            use_mesh_modifiers=True,
            use_mesh_modifiers_render=True,
            mesh_smooth_type="OFF",
            colors_type="SRGB",
            prioritize_active_color=False,
            use_armature_deform_only=False,
            add_leaf_bones=False,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            use_custom_props=True,
            bake_anim=True,
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=False,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0,
            path_mode="AUTO",
            embed_textures=False,
        )
        if "FINISHED" not in result:
            raise RuntimeError(f"FBX export did not finish: {result}")
    finally:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in prior_selected:
            if obj.name in bpy.data.objects:
                obj.select_set(True)
        if prior_active and prior_active.name in bpy.data.objects:
            bpy.context.view_layer.objects.active = prior_active


def _build_report(scene, source_path, canonical_fbx_path, temporary_fbx, validation):
    bones = sorted(
        bone.name
        for obj in _export_objects()
        if obj.type == "ARMATURE"
        for bone in obj.data.bones
    )
    lods = {}
    for collection_name in jss_scene_setup.LOD_COLLECTIONS:
        collection = jss_scene_setup.require_collection(collection_name)
        meshes = sorted(
            (obj for obj in collection.objects if obj.type == "MESH"),
            key=lambda obj: obj.name,
        )
        lods[collection_name] = {
            "meshes": [obj.name for obj in meshes],
            "triangles": sum(validate_character.triangle_count(obj) for obj in meshes),
        }
    dimensions = _lod0_dimensions()
    root_motion = _root_motion(scene)
    return {
        "schemaVersion": REPORT_SCHEMA_VERSION,
        "assetName": scene["jss_asset_name"],
        "sourceAsset": _project_relative(source_path),
        "sourceBlendSha256": _sha256(source_path),
        "fbxAsset": _project_relative(canonical_fbx_path),
        "fbxSha256": _sha256(temporary_fbx),
        "blenderVersion": bpy.app.version_string,
        "rigKind": scene["jss_rig_kind"],
        "units": {"lengthUnit": "METERS", "metersPerUnit": 1.0},
        "axes": {"forward": "-Z", "up": "Y", "unityForward": "+Z"},
        "dimensionsMeters": dimensions,
        "bones": bones,
        "lods": lods,
        "materials": sorted({
            material.name
            for obj in _export_objects()
            if obj.type == "MESH"
            for material in obj.data.materials
            if material is not None
        }),
        "forwardMarker": scene.get("jss_forward_marker", "SOCKET_Forward"),
        "rootMotion": root_motion,
        "configuration": {
            "globalScale": 1.0,
            "applyUnitScale": True,
            "forwardAxis": "-Z",
            "upAxis": "Y",
            "addLeafBones": False,
            "useSelection": True,
        },
        "validation": validation.to_dict(),
    }


def _lod0_dimensions():
    collection = jss_scene_setup.require_collection("JSS_LOD0")
    corners = []
    for obj in collection.objects:
        if obj.type != "MESH":
            continue
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not corners:
        raise RuntimeError("Cannot measure dimensions without an LOD0 mesh.")
    minimum = Vector((min(point[i] for point in corners) for i in range(3)))
    maximum = Vector((max(point[i] for point in corners) for i in range(3)))
    size = maximum - minimum
    return {"x": size.x, "y": size.y, "z": size.z}


def _root_motion(scene):
    bone_name = scene.get("jss_root_motion_bone", "Root")
    rigs = [obj for obj in _export_objects() if obj.type == "ARMATURE"]
    if len(rigs) != 1 or bone_name not in rigs[0].pose.bones:
        raise RuntimeError(f"Root-motion bone {bone_name!r} is unavailable.")
    rig = rigs[0]
    prior_frame = scene.frame_current
    try:
        scene.frame_set(scene.frame_start)
        start = rig.matrix_world @ rig.pose.bones[bone_name].matrix.to_translation()
        scene.frame_set(scene.frame_end)
        end = rig.matrix_world @ rig.pose.bones[bone_name].matrix.to_translation()
    finally:
        scene.frame_set(prior_frame)
    delta = end - start
    return {
        "bone": bone_name,
        "startFrame": scene.frame_start,
        "endFrame": scene.frame_end,
        "deltaMeters": {"x": delta.x, "y": delta.y, "z": delta.z},
        "distanceMeters": delta.length,
    }


def _project_relative(path):
    resolved = Path(path).resolve()
    try:
        return resolved.relative_to(PROJECT_ROOT).as_posix()
    except ValueError:
        return resolved.name


def _sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _remove_paths(paths):
    for path in paths:
        Path(path).unlink(missing_ok=True)


def main():
    report = export_character()
    print(json.dumps(report, indent=2, sort_keys=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"JSS character export failed: {exc}", file=sys.stderr)
        raise
