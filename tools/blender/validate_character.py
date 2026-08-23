"""Fail-closed validation for Just Some Stars Blender character sources."""

from __future__ import annotations

import json
import math
import sys
from dataclasses import asdict, dataclass

import bmesh
import bpy

import jss_scene_setup


@dataclass(frozen=True)
class ValidationIssue:
    code: str
    subject: str
    message: str

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class ValidationResult:
    issues: list[ValidationIssue]

    @property
    def is_valid(self):
        return not self.issues

    def format_issues(self):
        if self.is_valid:
            return "Character scene is valid."
        return "\n".join(
            f"[{issue.code}] {issue.subject}: {issue.message}"
            for issue in self.issues
        )

    def to_dict(self):
        return {
            "isValid": self.is_valid,
            "issues": [issue.to_dict() for issue in self.issues],
        }


class CharacterValidationError(RuntimeError):
    def __init__(self, result: ValidationResult):
        super().__init__(result.format_issues())
        self.result = result


def validate_scene(scene=None):
    target_scene = scene or bpy.context.scene
    issues = []

    _validate_scene_metadata(target_scene, issues)
    export = bpy.data.collections.get(jss_scene_setup.EXPORT_COLLECTION)
    collections = _validate_lod_collections(target_scene, export, issues)
    export_objects = (
        list(jss_scene_setup.iter_collection_objects(export)) if export else []
    )
    _validate_object_names(export_objects, issues)
    _validate_transforms(export_objects, issues)
    armatures = [obj for obj in export_objects if obj.type == "ARMATURE"]
    _validate_armature_contract(target_scene, armatures, issues)
    _validate_meshes(collections, armatures, issues)

    return ValidationResult(issues)


def validate_or_raise(scene=None):
    result = validate_scene(scene)
    if not result.is_valid:
        raise CharacterValidationError(result)
    return result


def triangle_count(obj):
    if obj.type != "MESH":
        return 0
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def _validate_scene_metadata(scene, issues):
    if scene.unit_settings.system != "METRIC" or not math.isclose(
        scene.unit_settings.scale_length, 1.0, abs_tol=1e-6
    ):
        issues.append(
            ValidationIssue(
                "INVALID_UNIT_SCALE",
                scene.name,
                "Scene units must be metric with one Blender unit equal to one metre.",
            )
        )
    if scene.get("jss_rig_kind") not in jss_scene_setup.SUPPORTED_RIG_KINDS:
        issues.append(
            ValidationIssue(
                "INVALID_RIG_KIND",
                scene.name,
                "Declare jss_rig_kind as Generic or Humanoid.",
            )
        )
    try:
        jss_scene_setup.expected_bones(scene)
    except ValueError as exc:
        issues.append(
            ValidationIssue("INVALID_BONE_DECLARATION", scene.name, str(exc))
        )


def _validate_lod_collections(scene, export, issues):
    found = {}
    if export is None:
        issues.append(
            ValidationIssue(
                "MISSING_EXPORT_COLLECTION",
                jss_scene_setup.EXPORT_COLLECTION,
                "The canonical export collection is required.",
            )
        )
    elif _collection_parents(scene, export) != [scene.collection]:
        issues.append(
            ValidationIssue(
                "INVALID_EXPORT_COLLECTION_PARENT",
                jss_scene_setup.EXPORT_COLLECTION,
                "JSS_EXPORT must be linked directly and only under the active scene root.",
            )
        )
    for collection_name in jss_scene_setup.LOD_COLLECTIONS:
        collection = bpy.data.collections.get(collection_name)
        if collection is None:
            issues.append(
                ValidationIssue(
                    "MISSING_LOD_COLLECTION",
                    collection_name,
                    "LOD0, LOD1 and LOD2 collections are all required.",
                )
            )
            continue
        found[collection_name] = collection
        if export is None or _collection_parents(scene, collection) != [export]:
            issues.append(
                ValidationIssue(
                    "INVALID_LOD_COLLECTION_PARENT",
                    collection_name,
                    f"{collection_name} must be linked directly and only under JSS_EXPORT.",
                )
            )
        if not any(obj.type == "MESH" for obj in collection.objects):
            issues.append(
                ValidationIssue(
                    "EMPTY_LOD_COLLECTION",
                    collection_name,
                    "Each LOD collection must contain at least one mesh.",
                )
            )
    return found


def _collection_parents(scene, child):
    parents = []
    candidates = [scene.collection, *bpy.data.collections]
    for parent in candidates:
        if child.name in {candidate.name for candidate in parent.children}:
            parents.append(parent)
    return parents


def _validate_object_names(objects, issues):
    required_prefix = {
        "MESH": "CHR_",
        "ARMATURE": "RIG_",
        "EMPTY": "SOCKET_",
    }
    for obj in objects:
        prefix = required_prefix.get(obj.type)
        if prefix is None:
            issues.append(
                ValidationIssue(
                    "UNSUPPORTED_EXPORT_OBJECT",
                    obj.name,
                    f"Objects of type {obj.type} cannot enter a character FBX.",
                )
            )
        elif not obj.name.startswith(prefix):
            issues.append(
                ValidationIssue(
                    "INVALID_OBJECT_PREFIX",
                    obj.name,
                    f"{obj.type} objects must use the {prefix} prefix.",
                )
            )


def _validate_transforms(objects, issues):
    for obj in objects:
        if any(not math.isclose(component, 1.0, abs_tol=1e-5) for component in obj.scale):
            issues.append(
                ValidationIssue(
                    "UNAPPLIED_SCALE",
                    obj.name,
                    f"Apply object scale before export; found {tuple(obj.scale)}.",
                )
            )


def _validate_armature_contract(scene, armatures, issues):
    if len(armatures) != 1:
        issues.append(
            ValidationIssue(
                "ARMATURE_COUNT",
                scene.name,
                f"Exactly one armature is required; found {len(armatures)}.",
            )
        )
        return
    try:
        expected = set(jss_scene_setup.expected_bones(scene))
    except ValueError:
        return
    actual = {bone.name for bone in armatures[0].data.bones}
    for bone_name in sorted(actual - expected):
        issues.append(
            ValidationIssue(
                "UNEXPECTED_BONE",
                bone_name,
                "The bone is not declared in jss_expected_bones.",
            )
        )
    for bone_name in sorted(expected - actual):
        issues.append(
            ValidationIssue(
                "MISSING_EXPECTED_BONE",
                bone_name,
                "A declared bone is absent from the armature.",
            )
        )


def _validate_meshes(collections, armatures, issues):
    rig = armatures[0] if len(armatures) == 1 else None
    lod_triangles = []
    for collection_name in jss_scene_setup.LOD_COLLECTIONS:
        collection = collections.get(collection_name)
        if collection is None:
            continue
        meshes = [obj for obj in collection.objects if obj.type == "MESH"]
        total_triangles = 0
        for obj in meshes:
            total_triangles += triangle_count(obj)
            _validate_materials(obj, issues)
            _validate_armature_binding(obj, rig, issues)
            if collection_name == "JSS_LOD0" and not _is_manifold(obj):
                issues.append(
                    ValidationIssue(
                        "NON_MANIFOLD_HERO_MESH",
                        obj.name,
                        "LOD0 hero meshes must be closed manifold geometry.",
                    )
                )
        if meshes:
            lod_triangles.append((collection_name, total_triangles))
    for (earlier_name, earlier_count), (later_name, later_count) in zip(
        lod_triangles, lod_triangles[1:]
    ):
        if earlier_count <= later_count:
            issues.append(
                ValidationIssue(
                    "NON_DESCENDING_LOD_TRIANGLES",
                    later_name,
                    f"{earlier_name} has {earlier_count} triangles and {later_name} "
                    f"has {later_count}; every later LOD must be simpler.",
                )
            )


def _validate_materials(obj, issues):
    materials = list(obj.data.materials)
    if not materials:
        issues.append(
            ValidationIssue(
                "UNNAMED_MATERIAL",
                obj.name,
                "Every character mesh must declare at least one MAT_ material.",
            )
        )
        return
    for material in materials:
        if material is None or not material.name.startswith("MAT_"):
            issues.append(
                ValidationIssue(
                    "UNNAMED_MATERIAL",
                    obj.name,
                    "Character materials must be non-null and use the MAT_ prefix.",
                )
            )


def _validate_armature_binding(obj, rig, issues):
    armature_modifiers = [modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]
    if (
        rig is None
        or len(armature_modifiers) != 1
        or armature_modifiers[0].object is not rig
    ):
        issues.append(
            ValidationIssue(
                "INVALID_ARMATURE_BINDING",
                obj.name,
                "Every LOD mesh must have exactly one modifier bound to the declared rig.",
            )
        )


def _is_manifold(obj):
    editable = bmesh.new()
    try:
        editable.from_mesh(obj.data)
        return bool(editable.faces) and all(edge.is_manifold for edge in editable.edges)
    finally:
        editable.free()


def main():
    result = validate_scene()
    print(json.dumps(result.to_dict(), indent=2, sort_keys=True))
    if not result.is_valid:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
