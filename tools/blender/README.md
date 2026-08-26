# Just Some Stars character pipeline

> **Status after the 2026-08-25 2.5D pivot:** this is a preserved, tested Task
> 11 pipeline for optional 3D props, reference renders and future experiments.
> It is not the active shipping-character pipeline. Do not resume the
> unfinished Task 12 Captain rig, weighting, walk or LOD work. Shipping
> characters use the frame-atlas architecture in
> `docs/superpowers/specs/2026-08-25-2.5d-gameplay-pivot-design.md`.

This directory is the preserved Blender-to-Unity asset contract. It handles pipeline
mechanics only; the Task 10 written character labels and dimensions remain the
visual authority, and generated reference-sheet drift is never accepted as a
modeling instruction.

## Scene contract

- Blender 5.2 LTS; `1 BU = 1 metre`; metric unit scale `1.0`.
- Export scale `1.0`, FBX forward `-Z`, up `Y`; Unity receives forward `+Z`.
- Character asset names use lowercase kebab-case.
- Mesh objects use `CHR_`, armatures `RIG_`, materials `MAT_`, sockets `SOCKET_`.
- The export graph is `JSS_EXPORT` with `JSS_LOD0`, `JSS_LOD1`, and `JSS_LOD2`.
- Every mesh has applied scale, exactly one declared armature modifier, and a
  named material. LOD0 must be manifold.
- The exact bone set is declared in scene metadata. Extra or missing bones fail
  validation; they are not silently accepted.
- `jss_rig_kind` is explicitly `Humanoid` or `Generic`. Human crew use the
  declared Humanoid policy when their complete Unity-compatible skeleton exists;
  Ori and non-human rigs use Generic. A tiny rig is never called Humanoid.

Run a Blender tool from the repository root with the tool directory on the
script path, or open the `.blend` and invoke the module from Blender Python.
The test suite demonstrates the background invocation.

## Tools

- `jss_scene_setup.py` creates deterministic units, animation range, metadata,
  and LOD collections.
- `validate_character.py` validates the whole export graph and exits nonzero on
  any issue. Export always calls it first.
- `generate_lods.py` creates deterministic LOD1/LOD2 starting points from LOD0
  with ratios `0.50` and `0.20`. It preserves transforms, materials, and the
  shared rig, refuses shape-key meshes, and only replaces objects it previously
  generated. Automatic decimation is a production starting point, not final
  hero-art quality; an artist must review silhouettes, joints, UVs, and weights.
- `export_unity_fbx.py` transactionally writes an FBX and adjacent
  `.jss-character.json`. A failed validation or export removes both canonical
  outputs and all temporary files so stale success cannot be imported.

The schema-versioned report records source/FBX paths and hashes, Blender
version, units, axes, rig kind, dimensions, exact bones and materials, LOD mesh
and triangle counts, forward marker, measured animated root displacement,
export configuration, and validation result. Unity rejects missing, malformed,
stale, or incompatible reports rather than guessing.

## Authoring and export commands

The modules are deliberately separate so an automatic LOD pass cannot silently
change a source and immediately publish it. Configure or regenerate, inspect the
result, save the `.blend`, then validate and export the clean saved source.

From Blender's Scripting workspace, with this directory added to `sys.path`:

```python
import bpy
import jss_scene_setup
import generate_lods

jss_scene_setup.configure_scene(
    asset_name="your-character",
    rig_kind="Generic",  # or Humanoid only with the complete declared rig
    expected_bones=("Root", "Hips", "Spine"),  # replace with the exact rig
)
generate_lods.generate_lods(lod1_ratio=0.50, lod2_ratio=0.20)
bpy.ops.wm.save_as_mainfile()  # inspect first; export rejects a dirty scene
```

Then run the saved source in background mode:

```bash
blender --background Assets/_JustSomeStars/Art/Characters/Source/your-character.blend \
  --python tools/blender/validate_character.py
blender --background Assets/_JustSomeStars/Art/Characters/Source/your-character.blend \
  --python tools/blender/export_unity_fbx.py
```

Both commands exit nonzero on failure. The exporter deletes the canonical FBX,
report, and temporary outputs before reporting a dirty source, validation error,
or export error. Save after every Python-driven edit as well as every UI edit;
Blender's background Python API does not automatically mark every direct data
assignment dirty.

## Focused verification

```bash
blender --background --factory-startup --python-exit-code 1 \
  --log-file Builds/Logs/task11-blender-pipeline.log \
  --python tools/blender/tests/test_character_pipeline.py
```

The committed `task11-primitive` fixture is intentionally Generic and primitive.
It proves scale, axes, three LODs, bone identity, a real one-metre animated root
displacement, report hashing, FBX import, and reimport idempotence. It does not
claim character likeness, final deformation quality, or Task 12 hero art.
