# Frontend Award-UI Design Specification

**Date:** 2026-08-22
**Status:** User-approved visual direction; implementation authorized
**Product:** *Just Some Stars* Android Frontend

## Objective

Replace the committed Task 5 engineering-style Frontend presentation with the
approved cinematic landscape design while preserving every existing behavior,
truthful string, license, Back action and lifecycle contract. The approved
images are visual authorities: a material difference between a settled Unity
capture and its corresponding target is a defect to fix, not an invitation to
reinterpret the design.

## Immutable visual references

The following files are the only visual targets for this redesign:

| State | Reference | SHA-256 |
| --- | --- | --- |
| Main | `outputs/frontend-redesign-targets/main-landscape.png` | `4c70a107b5206d976b3febcb3d41b0d6408cac084002a697f3625374bd59796d` |
| Settings | `outputs/frontend-redesign-targets/settings-landscape.png` | `27edc232b6c8901430c2712811951e643dc53ed1c3900e5df4164c16ff8d50e1` |
| Credits top | `outputs/frontend-redesign-targets/credits-top-landscape.png` | `c509a7e69c913b8d793738bd1749dfc10248fd5a8196043389c5243eebcebe7c` |
| Credits tail | `outputs/frontend-redesign-targets/credits-tail-landscape.png` | `51d6a7d34f109ff8dbf14259067936676adc929d16af08d27c316d83ecb99e89` |
| Privacy | `outputs/frontend-redesign-targets/privacy-landscape.png` | `a7cdc5e2a38bd4732dad4f4a00b4311473baae263cb4d5872fbbde90cdc54881` |

The references are approximately the exact target-device aspect ratio
(`1616x720`). Unity captures are compared after fitting each reference to the
capture dimensions without changing its composition.

## Scope

### Included

- Landscape-only Frontend presentation for both Android landscape directions.
- Generated, animation-ready 2.5D scene layers.
- Live title, version, controls, panel content and license text.
- Main, Settings, Credits top/tail and Privacy visual states.
- Entrance, ambient, interaction and modal motion.
- Exact current functionality and exact current player-facing copy.
- Rebuilt internal APK, artifact inspection and one final exact-device QA run.
- Target-versus-capture review by the harsh critic.

### Excluded

- Real settings controls; Task 6 owns them.
- Enabling Continue or adding New Game; later save/mode/UI tasks own them.
- New copy, accounts, links, purchases, store SDKs or gameplay.
- Google Play upload, Codemagic remote execution and Galaxy Seller mutation;
  these remain paused until the user approves the rebuilt Frontend.

## Visual composition

- The canvas is designed at `1616x720` with `ScaleWithScreenSize`,
  `MatchWidthOrHeight` and match `0.5`.
- The title occupies the upper-left observatory arch and uses a high-contrast
  luminous serif matching the approved reference. UI and body copy use the
  existing clean sans family.
- The primary disabled Continue control and its microcopy occupy the lower-left
  instrument area. Settings, Credits and Privacy form one subordinate row.
- Development Flight and Version 1.0 remain quiet metadata in the upper-right.
- The scene remains visible through modal states. A depth-preserving dim layer
  lowers distraction without turning the environment into a black backdrop.
- The modal uses one smoked-glass and painted-metal instrument frame, a thin
  brass edge, restrained cyan status detail and a fixed tactile Close control.
- Credits uses the same frame with a clipped live ScrollRect, a brass rail and
  cyan thumb. The Close control never scrolls.

## Asset architecture

Generated art is stored under
`Assets/_JustSomeStars/Art/UI/FrontendRedesign/` and imported by Unity with
deterministic texture settings.

Required layers:

1. `LandscapePlate.png` — sky, horizon, terrain and distant observatory lighting.
2. `ObservatoryForeground.png` — arch, deck, rail and edge shadow with alpha.
3. `Telescope.png` — isolated foreground telescope with alpha.
4. `SignalTower.png` — distant tower/emissive silhouette with alpha.
5. `StarGlints.png` — sparse glint sprites with alpha.
6. `AmbientMotes.png` — sparse warm/cyan motes with alpha.
7. `PrimaryControl.png` — empty nine-slice primary instrument plate.
8. `SecondaryControl.png` — empty nine-slice secondary instrument plate.
9. `ModalFrame.png` — empty nine-slice modal instrument frame.
10. `SignalGlyphs.png` — authored Settings, Credits and Privacy glyph strip.

Opaque layers are generated at or above the target resolution. Cutouts use the
built-in image generator on a flat chroma key, then the supplied soft-matte and
despill helper. Alpha bounds, halos and composite seams are inspected before
Unity import. Translucent Signal beam, light sweeps and glow are rendered live
in Unity rather than encoded into a lossy cutout.

The main title remains live TextMeshPro text. Use the closest legally
redistributable high-contrast serif after a direct rendered comparison; bundle
its exact license and retain the existing Liberation Sans/OFL and Apache texts.
If no font matches the approved title materially, a separately generated title
cutout may replace only the visible title while the live TMP title remains the
semantic/source contract.

## Runtime hierarchy and behavior

`FrontendVisualRoot.prefab` is built and verified separately before replacing
the current scene hierarchy. It contains:

- Canvas, CanvasScaler and GraphicRaycaster
- full-screen BackgroundLayers and SafeArea
- TitleGroup, StatusGroup and MenuGroup
- real Button controls for Continue, Settings, Credits and Privacy
- LocalPanel with dim layer, instrument frame, ScrollRect and Close Button
- existing `FrontendController`, `FrontendView`, `SafeAreaFitter` and
  `UnityFrontendLifecycle` contracts
- `FrontendMotionDirector` for visual motion only

The existing controller strings, license concatenation, event subscriptions,
disabled Continue behavior, panel behavior and root/modal Back behavior remain
unchanged.

## Motion contract

Motion uses unscaled time, never changes controller state and always settles to
the static target geometry.

- **Entrance:** background resolves from a subtle camera drift; the title
  reveals with a soft warm mask; controls enter in a short stagger.
- **Ambient:** landscape parallax stays below 8 authored pixels; telescope idle
  tracking stays below 1.25 degrees; star glints vary slowly; the Signal beam
  breathes; lens and panel indicators pulse without strobing.
- **Buttons:** focus/press adds shallow depth, a short edge-light sweep and a
  restrained status response. Disabled Continue never appears enabled.
- **Panels:** dim layer fades in, panel moves a short distance with a damped
  settle, copy fades after the frame, and Close reverses the sequence. Back uses
  the same close path. Credits scroll position resets to top on every open.
- **Pause/resume:** no duplicate motion coroutine, listener or particle owner is
  created. Resume preserves the current panel state.

No loop flashes faster than three times per second. A single serialized motion
scale provides the seam Task 6 will connect to accessibility settings.

## Testing and evidence

Testing is proportional and concentrated:

1. One focused EditMode asset contract covers target hashes, landscape
   orientation, exact prefab bindings, required art/font assets, touch sizes,
   live copy, modal/scroll hierarchy and absence of the obsolete visual tree.
2. One focused PlayMode contract covers entrance settlement, exact-once button
   callbacks, panel animation/Back/Close, Credits top reset and pause/resume.
3. Existing Frontend controller and Boot-to-Frontend integration tests remain.
4. Run focused tests during RED/GREEN; run the complete EditMode and PlayMode
   suites once after the final scene is installed.
5. Rebuild the internal APK once after full GREEN, then inspect package,
   signing, manifest, architecture and bundled licenses.
6. Capture all five states at the settled `1616x720` device viewport and create
   target/capture comparison pairs. The harsh critic blocks on any material
   mismatch, clipping, unreadable text, false state or broken interaction.

## Completion boundary

Completion means the rebuilt APK has passed automated, artifact, device and
critic fidelity gates. Work then pauses for explicit user approval. No remaining
Release Runway external action begins in this redesign task.
