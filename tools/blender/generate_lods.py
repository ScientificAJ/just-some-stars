"""Deterministic pre-production LOD generation for JSS character sources."""

from __future__ import annotations

import json

import bpy

import jss_scene_setup
from validate_character import triangle_count


DEFAULT_LOD1_RATIO = 0.5
DEFAULT_LOD2_RATIO = 0.2


def generate_lods(
    lod1_ratio: float = DEFAULT_LOD1_RATIO,
    lod2_ratio: float = DEFAULT_LOD2_RATIO,
):
    """Regenerate LOD1/2 from LOD0 while preserving rig/material bindings.

    This creates deterministic production starting points. Hero characters still
    require an artist's silhouette, deformation and facial-shape review.
    """
    _validate_ratios(lod1_ratio, lod2_ratio)
    lod0_collection = jss_scene_setup.require_collection("JSS_LOD0")
    source_meshes = sorted(
        (obj for obj in lod0_collection.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    if not source_meshes:
        raise RuntimeError("JSS_LOD0 must contain at least one source mesh.")
    for source in source_meshes:
        if source.data.shape_keys is not None:
            raise RuntimeError(
                f"{source.name} has shape keys; author reviewed LOD meshes instead of "
                "using automatic decimation."
            )
        if not source.name.endswith("_LOD0"):
            raise RuntimeError(
                f"LOD0 mesh {source.name!r} must end with the _LOD0 suffix."
            )

    for collection_name in ("JSS_LOD1", "JSS_LOD2"):
        _remove_prior_generated_objects(
            jss_scene_setup.require_collection(collection_name)
        )

    for index, ratio in ((1, lod1_ratio), (2, lod2_ratio)):
        target_collection = jss_scene_setup.require_collection(f"JSS_LOD{index}")
        for source in source_meshes:
            generated = source.copy()
            generated.data = source.data.copy()
            generated.name = source.name[:-1] + str(index)
            generated.data.name = generated.name + "_Mesh"
            generated["jss_generated_lod"] = True
            generated["jss_lod_ratio"] = ratio
            target_collection.objects.link(generated)
            _apply_decimation(generated, ratio)

    bpy.context.scene["jss_lod_ratios"] = json.dumps(
        {"JSS_LOD0": 1.0, "JSS_LOD1": lod1_ratio, "JSS_LOD2": lod2_ratio},
        sort_keys=True,
        separators=(",", ":"),
    )
    counts = _triangle_counts()
    if not (
        counts["JSS_LOD0"] > counts["JSS_LOD1"] > counts["JSS_LOD2"] > 0
    ):
        raise RuntimeError(
            "Generated LOD triangle counts must be strictly descending; got "
            f"{counts}."
        )
    return counts


def _validate_ratios(lod1_ratio, lod2_ratio):
    if not (0.0 < lod2_ratio < lod1_ratio < 1.0):
        raise ValueError(
            "LOD ratios must satisfy 0 < lod2_ratio < lod1_ratio < 1."
        )


def _remove_prior_generated_objects(collection):
    for obj in tuple(collection.objects):
        if not obj.get("jss_generated_lod", False):
            raise RuntimeError(
                f"Refusing to replace artist-authored object {obj.name!r} in "
                f"{collection.name}."
            )
        bpy.data.objects.remove(obj, do_unlink=True)


def _apply_decimation(obj, ratio):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new(name="JSS_AutoLOD", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    obj.modifiers.move(obj.modifiers.find(modifier.name), 0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def _triangle_counts():
    result = {}
    for collection_name in jss_scene_setup.LOD_COLLECTIONS:
        collection = jss_scene_setup.require_collection(collection_name)
        result[collection_name] = sum(
            triangle_count(obj) for obj in collection.objects if obj.type == "MESH"
        )
    return result


if __name__ == "__main__":
    print(json.dumps(generate_lods(), indent=2, sort_keys=True))
