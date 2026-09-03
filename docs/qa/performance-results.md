# Task 30 performance and package results

Last updated: 2026-09-03

## Acceptance state

Task 30's performance architecture, deterministic budgets, representative-scene
smoke coverage, final package regression and Android build are complete and
source-fresh. Device frame-time, memory, thermal, battery and content-lock
acceptance are **not complete**: Limrun rejected the one authorized instance
request before creation because the organization has zero remaining credits and
no active subscription, and no physical Android device is attached.

This report deliberately separates proven results from pending measurements.
It does not reuse an older APK or rename editor/emulator evidence as Realme
Narzo evidence.

## Frozen performance contract

All memory units below are binary units. The runtime constant named
`PerformanceProcessMemoryBudgetMb` is interpreted as **896 MiB**
(939,524,096 bytes); Android `dumpsys meminfo` kB values are converted as KiB.

| Metric | Acceptance threshold |
|---|---:|
| Performance target | 30 FPS |
| Sustained median | at least 29.5 FPS |
| 1% low | at least 27 FPS |
| Median CPU frame time | at most 33.33 ms |
| Median GPU frame time | at most 33.33 ms |
| Process total PSS | at most 896 MiB |
| Warm-state PSS growth after unload/reload | at most 5% without monotonic growth |
| Per-destination texture residency | at most 256 MiB |
| Android texture dimension | at most 2048 px |
| Destination atlases | at most 24 |
| Transparent layer peak | at most 8 |
| Active layered characters | at most 6 |
| Active 2D lights | at most 4 |
| Active particle systems | at most 3 |

The intended hardware run is three labelled 20-minute representative segments:
Mirra surface play, Vesper/flight play and Aster Veil play. Each segment must
record median and 1% low FPS, CPU/GPU frame time, peak and ending PSS, thermal
status/temperature, battery delta and the exact installed APK hash. The user
approved Limrun as a hardware substitute, but an x86 cloud emulator still
cannot prove Realme Narzo thermals; that limitation must remain explicit.

## Implemented performance architecture

- `QualityProfileService` is composition-owned and consumes device-local
  settings. It exposes Performance, Balanced, Cinematic and High Frame Rate,
  owns target frame rate, serialized-camera dynamic-resolution intent and
  scalable-buffer state once, adapts only inside the declared profile envelope,
  lowers every profile under low-memory pressure, and restores prior globals and
  camera state on release.
- `MirraQualityController2D` defers global frame-rate/render-scale ownership to
  the service while retaining scene-local light, particle, volume and parallax
  quality. It cannot double-own the shared globals.
- `PerformanceBudgetValidator` measures every enabled player scene and fails
  closed on texture residency, texture dimension, unique active multi-sprite
  PNG atlas textures, spatial overlap between visible authored parallax bands,
  authoritative active actors, 2D lights, particles and the declared process
  ceiling. Each synthetic over-budget rule reports the owner, measured value
  and limit. The highest current scene samples are five atlases, seven
  overlapping bands and six active actors, beneath the frozen 24/8/6 limits.
- `PerformanceMarkers` instruments the real player, crew, flight, Lens, UI and
  scene-streaming hot paths. The production-scene smoke opens Mirra,
  Task25VesperFlight, Aster Veil and Frontend and proves the corresponding
  markers execute.
- The high-cost Mirra environment, masks and ship atlases now use a 2048-pixel
  Android import cap. The full project validator and all performance-budget
  tests pass with those imports.

## Source-final automated evidence

| Gate | Result | Evidence |
|---|---:|---|
| Task 29 audio validator | PASS | validate-only command in final batch transcript |
| Task 29 Captain face validator | PASS | validate-only command in final batch transcript |
| QA Python suite | 12/12 | dependency-free QA final batch |
| PlayMode manifest integrity | 37/37 fixtures | `tools/qa/playmode-fixtures.txt` |
| Sprite pipeline Python suite | 22/22 | final batch; only Pillow deprecation notice |
| Consolidated media/QA/sprite preflight | PASS; QA 12/12 and sprites 22/22 | `Builds/Logs/task30-final-source-preflight.log` |
| Project content validator | PASS | `Builds/Logs/task30-final-corrected-project-content-validation.log` |
| Full EditMode assembly | 428/428, zero fail/skip/inconclusive | `Builds/TestResults/task30-final-corrected-full-editmode.xml` |
| Isolated PlayMode assembly | 235/235 across 37/37 fixtures | `Builds/TestResults/task30-final-corrected-playmode/summary.json` |
| Performance budget contract | 9/9 | included in the full EditMode result |
| Live profile/scene smoke | 7/7 | included in the isolated PlayMode result |

The isolated PlayMode result also closes the deferred automated package checks
for Tasks 26–29: Chapter One `6/6`, Shop `14/14`, accessibility `3/3`, launch
`1/1`, Frontend controller/dependencies/motion `12/12 + 6/6 + 1/1`, audio
`3/3`, cinematics `5/5`, and facial playback `2/2`.

## Android artifact

The one source-final Android Internal build completed after the final product
source change:

- APK: `Builds/AndroidInternal/JustSomeStars-internal.apk`
- size: 619,148,779 bytes
- SHA-256: `a7dba5e20f96a182f2a47e1a539aa4416f6195a4bdaafc2910734fba97402a0e`
- build report SHA-256:
  `39fb292213e76c7c6f60d49ae6a0e62f3edde427f361a8f77dbc158ecb7afc42`
- package: `com.scientificaj.justsomestars`
- version: 1.0 (code 1), minimum SDK 25, target SDK 36, ARM64
- internal/development build, Android Debug signer, APK Signature Scheme v2
- build report: Succeeded, three warnings, zero errors
- package ZIP integrity: clean
- manifest: OS backup disabled; GameActivity retained; Process Lifecycle
  initializer retained; unused EmojiCompat initializer absent
- payload: exact Liberation OFL and Apache-2.0 texts present; EmojiOne
  names/GUIDs absent

Primary build log:
`Builds/Logs/task30-final-corrected-android-internal.log`. The exact artifact
audit is `Builds/Logs/task30-final-corrected-artifact-audit.txt`; signature and
manifest reports use the same `task30-final-corrected-` prefix.

## Memory attribution status (JSS-020)

The prior Task 12 diagnostic was 737,833 kB total PSS (about 720.5 MiB). The
Task 20 sample was 865,920 kB (845.6 MiB), an increase of 128,087 kB
(about 125.1 MiB). The new 896 MiB ceiling leaves about 50.4 MiB above that
latest observation, but a static ceiling is not attribution.

Because no source-final Android process could be launched, Task 30 did not
capture a Unity Memory Profiler snapshot, destination PSS series, GPU timing or
unload/reload growth series. JSS-020 therefore remains open. Neither the new
APK size nor editor memory is used as a substitute for process PSS.

## Device/thermal attempt

The preserved sanitized attempt is
`Builds/DeviceEvidence/task30-final/limrun-capacity-blocker.txt`. Limrun returned
HTTP 400 before creation: no remaining credits and no active subscription.
Follow-up checks found zero Limrun instances, zero attached ADB devices and zero
Argent devices; the unused local Argent server was stopped. No paid time,
tunnel, inspector lease or orphaned instance remains.

Consequently the following remain pending and unclaimed:

- three 20-minute representative performance segments;
- CPU/GPU frame timing, peak memory, thermal and battery measurements;
- exact-installed-APK hash equality and lifecycle/log checks;
- content-lock screenshots and the accessibility/foldable matrix;
- the corrected Vesper-to-Koro completion/reload spot-check (JSS-025).
