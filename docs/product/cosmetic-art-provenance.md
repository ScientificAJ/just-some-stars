# Task 27 cosmetic art provenance

Task 27 uses built-in ImageGen to create the launch catalogue art in the locked
*Just Some Stars* painterly 2.5D storybook direction. ImageGen supplied the
authored pixels; deterministic tooling only resized, keyed and packed them for
Unity.

## Canonical Unity outputs

- `Assets/_JustSomeStars/Art/2D/Cosmetics/IconAtlases/`: eight opaque 4x4
  catalogue boards, each 1248x1248 pixels and sliced into sixteen 312x312 icons.
- `Assets/_JustSomeStars/Art/2D/Cosmetics/PresentationAtlases/`: eight RGBA 4x4
  presentation boards with the same item order and dimensions.
- `Assets/_JustSomeStars/Art/2D/Cosmetics/cosmetic-presentation-manifest.json`:
  published presentation-atlas hashes and slice contract.
- `Assets/_JustSomeStars/Content/Cosmetics/CosmeticCatalog.asset`: 128 distinct
  sprite bindings and canonical ownership/presentation metadata.

## ImageGen source records

The source records remain in the Codex generated-image store. They must not be
treated as active Unity assets or deleted as repository cleanup.

| Board | Opaque source | Solid-key presentation source |
|---|---|---|
| Captain suits | `exec-d8eaf23c-fd87-45a2-8752-98820f9aa70b.png` | `exec-b290375d-b330-4b03-89d6-9da725bd9620.png` |
| Captain gear | `exec-ac1da702-f1b4-4c40-98ac-6bebd68ffd9e.png` | `exec-2b737d33-9d5c-4e9a-940a-76cc41322606.png` |
| Ori | `exec-d299f86b-15d9-4b95-8a40-d381c115d68c.png` | `exec-b8a81bf9-6570-4677-b975-fd69b0d884a3.png` |
| Ship | `exec-d60401f6-648c-4ca5-bc8a-f04ce6a67b6d.png` | `exec-e657569c-8bff-4c98-9e01-f84e605663f8.png` |
| Lens | `exec-5137ebc3-5946-4575-9c6e-94e99a5193ce.png` | `exec-f6846c11-9cdb-4083-9d04-58cf64a546d1.png` |
| Clubhouse | `exec-3284cfbd-6cc9-4967-a489-1f5fdfe863cc.png` | `exec-e2a477fb-e23c-48ed-b04b-93c767b05f0c.png` |
| Photo | `exec-0ec65c05-2f97-4e69-a536-dfbd29f6360b.png` | `exec-bbace80f-205a-49fb-add8-a7d0b37a41ed.png` |
| Crew | `exec-ebeffb53-15fc-4377-bc21-fe7a9a86cf9f.png` | `exec-c49bafef-7373-4c81-b804-429a05be075b.png` |

All records are beneath
`/home/john/.codex/generated_images/01a01ef6-7435-7a90-9779-a1d0243b6585/`.
The generation brief required sixteen distinct category-appropriate cosmetics
in exact row-major order, no labels or logos, isolated studio presentation,
the approved warm copper/cyan/violet palette and consistent hand-painted 2.5D
lighting. The second pass was an image edit of each accepted opaque board: keep
the item pixels and ordering unchanged while replacing only the surrounding
background with pure `#00FF00`.

## Deterministic processing

The generated 1254x1254 images were centre-cropped to 1248x1248. Presentation
boards were chroma-extracted from the solid-key edits into true RGBA PNGs, then
visually composited over the game's dark navy for inspection. No catalogue
subject was redrawn by code, represented by a placeholder or replaced with a
programmatic shape.

The rejected transparency experiment
`exec-e2c3444e-1996-4a45-84a5-0ec806f90bf3.png` produced a painted checkerboard
rather than alpha and is not referenced by the Unity project.
