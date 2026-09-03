# Task 28 accessibility and responsive UI matrix

Date frozen: 2026-09-02

This document records the Task 28 implementation and final automated package
checkpoint. The source, prefab, scenes and automated contracts are authored and
the Tasks 26–30 source-final Unity batch is green. The manual device matrix is
still pending because the authorized Limrun request was rejected before
instance creation with zero remaining credits/no active subscription and no
physical Android device is attached. An unchecked runtime row is pending
evidence, not a claim of failure or success.

## Authored product contract

- All Task 28 player surfaces use the shared Homemade/Signal token asset,
  sliced approved plates, live TextMesh Pro labels and real Unity buttons.
- The Frontend keeps the approved painted-metal, smoked-glass, brass and cyan
  Signal presentation. New Game routes to the real opening. Continue is driven
  by missing, valid, recovered, unreadable, storage-unavailable and
  content-unavailable local-save states.
- Every Task 28-owned player string resolves from the English catalog. The
  canonical Liberation Sans OFL and Apache-2.0 TextAssets remain verbatim and
  outside localization; the localized credits wrapper precedes each raw text.
- The readable-font source is the system `fonts-noto-core` Noto Sans Regular
  file, copied byte-for-byte (SHA-256
  `89c3c497f618fdaa0b2d1e98fef93582f28c71debd2c4a8cdf41f190ced2909d`).
  Its Google copyright holders are identified in the credits wrapper and the
  user-readable canonical OFL 1.1 text immediately follows.
- The gameplay pause layer exposes Journey, Accessibility, Atlas, Captain,
  optional shop, private account backup and private birthday state from their
  real services. Restore Purchases and cloud-account linking require the shared
  age-aware grown-up confirmation gate; cancellation fails closed without
  starting either external action.
- Base Photo Mode pauses the active mode and provides bounded 2D pan/zoom,
  depth focus, exposure, clean-HUD capture and owned frames. Explorer adds only
  cinematic lenses, expanded authored poses and three local presets. Free 3D
  orbit is impossible.
- Accessibility settings apply immediately across Frontend, surface, flight
  and Chapter One compositions: 0.85–1.35 text scale, standard/readable font,
  captions with separate speaker labels, color-safe status symbols, reduced
  shake/flashing/motion/blur/particles and mirrored touch-control placement.

## Automated matrix authored now

| Contract | Authored coverage | Execution |
|---|---|---|
| Frontend launch states | `FrontendControllerTests`, `ApplicationLaunchIntegrationTests` | Passed in Task 30 isolated PlayMode (`12/12`, `1/1`) |
| Composition injection/release | `FrontendDependencyInjectionTests` | Passed in Task 30 isolated PlayMode (`6/6`) |
| Localized assets and immutable licenses | `FrontendSceneAssetTests`, `AccessibilityUiTests` | Passed in Task 30 full EditMode/isolated PlayMode (`12/12`, `3/3`) |
| 48dp controls and containment | 1920×1080 @ 420dpi and 2208×1768 @ 420dpi synthetic profiles | Passed in Task 30 full EditMode |
| Maximum text and combined options | 1.35 text scale, readable font, captions, Protanopia symbols, reduced effects and left-handed controls | Passed in Task 30 isolated PlayMode (`3/3`) |
| Photo Mode state restoration | Mode overlay, camera position/rotation/zoom, HUD, exposure and actor pose restoration | Passed in Task 30 package matrix |

## Manual device matrix pending device capacity

For each row, verify both landscape directions, safe-area containment, scroll
reachability, no overlap/clipping and at least 48dp touch targets.

| Surface/state | Standard | 1.35 text | Combined accessibility | Foldable resize |
|---|---:|---:|---:|---:|
| Frontend — no save | [ ] | [ ] | [ ] | [ ] |
| Frontend — valid save | [ ] | [ ] | [ ] | [ ] |
| Frontend — recovered backup | [ ] | [ ] | [ ] | [ ] |
| Frontend — unreadable/no recovery | [ ] | [ ] | [ ] | [ ] |
| Credits — top, both license tails, reopen-to-top | [ ] | [ ] | [ ] | [ ] |
| Opening and customization | [ ] | [ ] | [ ] | [ ] |
| Mirra surface HUD/dialogue/lens | [ ] | [ ] | [ ] | [ ] |
| Koro surface HUD/dialogue/lens | [ ] | [ ] | [ ] | [ ] |
| Vesper flight HUD | [ ] | [ ] | [ ] | [ ] |
| Aster Veil HUD/dialogue/objective | [ ] | [ ] | [ ] | [ ] |
| Clubhouse pause/customization/account/birthday | [ ] | [ ] | [ ] | [ ] |
| Atlas and optional shop/Restore flow | [ ] | [ ] | [ ] | [ ] |
| Base Photo Mode open/edit/capture/close | [ ] | [ ] | [ ] | [ ] |
| Explorer Photo Mode lenses/poses/presets | [ ] | [ ] | [ ] | [ ] |

## Final source-checkpoint evidence

- `Builds/Logs/task28-grownup-gate-materialize-green.log` (SHA-256
  `72ca19e38fb620190ef24a80fe24c2e43c55b7fe316cbbceffba5c24d0bc9772`):
  the builder saves, reopens and statically validates the localized Frontend,
  age-aware account challenge, persistent accessibility components, five
  gameplay scenes and four Chapter One scenes.
- `Builds/Logs/task28-final-source-compile.log` (SHA-256
  `898af10498a8d255b71cb5602c598b5e2fc260157dd8ca512607619cadf21d06`):
  Runtime, Editor, EditMode-test and PlayMode-test assemblies compile after the
  final materialization; Unity exits successfully without a C# compiler error.
- The bounded extreme critic initially held only on cloud-account linking
  bypassing the grown-up gate. Both Settings and the pause menu now use the
  shared age-aware authorization path, focused denial/approval coverage is
  authored, and the same critic returned final `PROCEED`.
- At final freeze the system partition is healthy (`29G/44G` used, `14G`
  available) and the canonical data volume has `93G` available. No storage
  cleanup or unrelated product work was performed during this checkpoint.

## Final package evidence

- Full EditMode: `428/428`, zero failed/skipped/inconclusive at
  `Builds/TestResults/task30-final-corrected-full-editmode.xml`.
- Isolated PlayMode: `235/235` across `37/37` fixtures at
  `Builds/TestResults/task30-final-corrected-playmode/summary.json`.
- Exact locally inspected APK SHA-256:
  `a7dba5e20f96a182f2a47e1a539aa4416f6195a4bdaafc2910734fba97402a0e`.
- Failed device-capacity preflight:
  `Builds/DeviceEvidence/task30-final/limrun-capacity-blocker.txt`.

Do not mark the manual matrix or Task 28 exact-device acceptance complete until
the same final source is rebuilt if necessary and one cleaned-up device session
executes every unchecked row.
