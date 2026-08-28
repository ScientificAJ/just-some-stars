# Task 12 Mirra 2.5D benchmark and budgets

Last updated: 2026-08-28

This is the production baseline established by replacement Task 12 Stage 5.
Later surface, camera, environment and optimization tasks may improve it, but
must not silently exceed it without a measured reason and updated evidence.

## Visual and runtime authority

- Approved target SHA-256:
  `72644970448effd81177222e0aa23ae8a23f9b733077dab6e27e9ca765f5eaed`.
- Final Unity Candidate 9 SHA-256:
  `adc10f11de965625bff8fa81b902323bd293f173089adaf0908c0ffdd6367032`.
- User-approved exact-device screenshot SHA-256:
  `3e402d344bfbbb9c02aa12a5a91dc6dcac455de27ea13eaa70027f2671c48848`.
- Acceptance viewport: 1616 x 720 landscape on a 720 x 1616, 280-dpi
  Android target with font scale 1.0.

## Enforceable authored budgets

| Area | Task 12 baseline / ceiling |
|---|---|
| Composition bands | Exactly 8: Sky, FarWorld, Atmosphere, Midground, Gameplay, ActorsAndProps, Foreground and Hud |
| Final Mirra PNGs | 15 player-facing source PNGs, 16,580,089 bytes total in the worktree; source plates are excluded |
| Large environment/mask sources | 2508 x 941 authored; Android import override no larger than 2048 |
| 2D lights | 3 in the accepted scene; hard test ceiling 4 |
| Particle systems | 1 Signal system at 5 particles/second; hard test ceiling 3 systems |
| Post-processing | Exactly 1 bounded scene volume |
| Gameplay material data | One gameplay normal map and one Signal emission mask are required |
| Character rendering | Sprite-atlas rendering only; no `Rigidbody`, 3D `Collider`, `SkinnedMeshRenderer` or character-model dependency |
| Recovery | Exactly one bounded `SurfaceRecovery2D` authority and one safe respawn contract |

The EditMode scene/asset contracts enforce the structural ceilings, layer
ownership, Android texture overrides, lighting/mask presence, actor grounding,
route alignment, recovery authority and absence of the superseded 3D path.

## Device observations and limitations

The same-instance pre-Lens-correction development player held 126 consecutive
SurfaceFlinger samples at a 33.333 ms mean interval (30.00 fps), with no sample
over 40 ms. Its total PSS was 737,833 KB. These are diagnostic observations,
not release memory guarantees: the final correction changed only the Lens
input binding, but the paid instance expired before those two measurements
could be repeated against the final APK.

The preserved final 18-second video proves stable presentation only. Although
Argent reported successful input injection, user review correctly found that
the recording does not visibly demonstrate movement, Jump, Interact or Lens.
Movement has a separate screenshot diff; behavior is covered by focused
PlayMode tests and the Lens-specific RED/GREEN plus before/after capture.

Unity/Vulkan overdraw was not attributable through the available remote ADB
instrumentation. Until Task 30's performance pass supplies a GPU capture, the
eight-band limit, independent ownership tests, four-light ceiling and
three-particle-system ceiling are the enforceable overdraw proxies. This
instrumentation gap is tracked in `docs/issue-register.md` and does not turn a
guessed number into a budget.
