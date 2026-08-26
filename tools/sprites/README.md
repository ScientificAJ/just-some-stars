# Just Some Stars sprite pipeline

This folder owns the deterministic coherent-strip pipeline used by replacement
Task 12. It adapts the useful `hatch-pet` production pattern—one grounded
source, deterministic frame processing, preview evidence and fail-closed
publishing—to the game's cinematic 2.5D characters. It does not use the Codex
pet atlas format and does not introduce runtime skeletal deformation.

## Public workflow

Use the workspace Python recorded by `codex_app__load_workspace_dependencies`.
On this workstation that is currently:

```bash
JSS_WORKSPACE_PYTHON=/home/john/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3
```

Generate the Stage 2 primitive source rows, then run the only public
orchestrator:

```bash
"$JSS_WORKSPACE_PYTHON" tools/sprites/create_stage2_primitive.py \
  --root Assets/_JustSomeStars/Art/2D/Characters/Source/Fixtures/Stage2Primitive

"$JSS_WORKSPACE_PYTHON" tools/sprites/prepare_animation_run.py \
  --request Assets/_JustSomeStars/Art/2D/Characters/Source/Fixtures/Stage2Primitive/primitive-stage2-request.json \
  --output Builds/VisualEvidence/task12-stage2/primitive-pipeline-final
```

`prepare_animation_run.py` owns extraction, validation, registration, atlas
assembly and evidence as one transaction. The other scripts are deliberately
imported stages rather than independent publishing commands; no partial stage
may publish a success artifact.

## Request contract

Schema version 1 declares:

- one safe `characterId`;
- exact frame width, height, pixels per unit and atlas columns;
- a bounded alpha threshold, baseline correction and optional tiny intentional
  enclosed-alpha tolerance;
- an exact RGBA facing marker used only for structural direction validation and
  removed before registration/runtime publication;
- `complete-rows-only` repair ownership;
- one or more coherent clip rows with stable id, source strip, frame count,
  facing, 1–30 FPS cadence, loop mode, pivot, contacts and events.

Every source strip is horizontal and must be exactly
`frameWidth * frameCount` by `frameHeight`. The pipeline derives alpha bounds
and baselines from pixels, rejects clipping and material enclosed holes,
validates the declared facing marker, validates alternating run contacts, then
registers small baseline variation to one shared baseline. A successful or
in-progress directory carries a deterministic ownership marker. A failed run
removes only stale outputs and staging data carrying that valid marker. An
unrelated, markerless, malformed or differently owned directory is refused
without deleting or overwriting it.

## Published outputs

For `primitive-stage2`, a successful transaction contains exactly:

- `.jss-sprite-pipeline-owner.json` — deletion/overwrite authority for this
  exact pipeline directory;
- `primitive-stage2.png` — lossless runtime atlas;
- `primitive-stage2.sprite-manifest.json` — schema, provenance, atlas hash,
  clips, rects, pivots, timing, contacts, events and validation result;
- `primitive-stage2.sprite-manifest.sha256` — hash of the exact manifest bytes;
- `primitive-stage2-contact-sheet.png` — static row/order review;
- `primitive-stage2-preview.webp` — looping motion review.

Only the runtime atlas, manifest and manifest hash are copied into
`Assets/_JustSomeStars/Art/2D/Characters/Atlases/`. Contact sheets and previews
remain evidence under `Builds/VisualEvidence/`, where Unity cannot mistake them
for runtime atlases.

## Unity contract

`CharacterSpritePostprocessor` scopes itself to PNG files under the canonical
atlas root. It requires a matching manifest and hash, imports deterministic
multiple sprites at the declared PPU and pivots, disables mipmaps/readability,
and uses Android ASTC 6x6. `CharacterSpriteSetValidator` reconciles the real
imported sprites, pivots, durations, contacts and events with the manifest.
`SpriteAtlasAnimator` advances declared per-frame durations deterministically
and emits each frame event exactly once per occurrence.

The Stage 2 primitive is pipeline evidence, not Captain or crew art. Stage 3
owns final Captain identity, body families, customization layers and motion.

## Verification

```bash
"$JSS_WORKSPACE_PYTHON" -m unittest discover -s tools/sprites/tests -v
```

Then run the focused EditMode `CharacterSpriteImportTests` and PlayMode
`SpriteAtlasAnimatorTests` commands recorded in the replacement Task 12 plan.
