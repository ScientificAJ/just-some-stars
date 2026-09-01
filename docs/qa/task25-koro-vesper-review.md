# Task 25 Koro/Vesper review

Status: **local chapter complete; corrected exact-APK completion spot-check
deferred to JSS-025**.

## Implemented chapter

- The real `Task25VesperFlight` route uses the production flight motor,
  Vesper gravity opportunity, authored landing gate and `LandingSequence` to
  enter `KoroVesper` without test-authored arrival events.
- One destination progression coordinator preserves the completed Mirra
  chapter and first Signal fragment, then owns six durable Koro checkpoints:
  approach, landing, traversal, spectra, geyser rhythm and second fragment.
- A rejected early geyser scan remains retryable. Natural and Signal spectra
  use two authored targets, a real comparison result and science-depth-specific
  Atlas copy.
- Bea, Mira and Ori observations reach the live dialogue presenter. The final
  save contains both `fragment.signal.mirra.001` and
  `fragment.signal.koro.002` exactly once.
- The surface is a full-bleed layered 2.5D composition. Responsive framing
  covers 1616x720 without gutters or hard inner seams; the natural and Signal
  geysers use distinct dedicated RGBA plume sprites above Gameplay.

## Visual authority and evidence

- Canonical target:
  `outputs/task25-koro-vesper-gameplay-target-v1.png`, SHA-256
  `a76309c8f98a3020363056e6f977ba3965ff0d604b1a4e1538ed5ffbb38ef8b1`.
- Corrected local Unity runtime frame:
  `Builds/DeviceEvidence/task25-final-corrected/local-runtime-visual-1616x720-v3.png`,
  SHA-256
  `9ada94c0e84f4fe30de78d2875b52e7eab61593d072ccb82f0ff187f36d1b330`.
- Same-viewport reference-left/runtime-right comparison:
  `Builds/DeviceEvidence/task25-final-corrected/reference-left-vs-runtime-right-1616x720.png`,
  SHA-256
  `ad56e408cd487d8694d8c0e8978eb303c43080f79aa36276fb18c41b3619d1d6`.
- Dedicated plume assets are 887x1774 RGBA. Natural SHA-256 is
  `9dea9b579a04915cfc7b91023017786548068e1634fa43c3941cac8821a3af4a`;
  Signal SHA-256 is
  `f638eb3eaae307aede3298904bdfb936e0ab65417092f67ef525c64422dc14e9`.

The plume sources were generated with the built-in image generator against
the locked Koro target, converted from chroma-key source to real alpha, then
imported and bound through Unity APIs. The earlier local `v1`/`v2` captures and
the pre-correction device screenshots are superseded for final visual claims.

## Focused verification

| Gate | Result |
|---|---|
| Authentic critic progression RED | `task25-critic-progression-red.xml`: `8/10`, exactly early-scan and Mirra-fragment failures |
| Corrected progression GREEN | `task25-critic-progression-green.xml`: `10/10` |
| Final production flight-to-surface E2E | `task25-production-e2e-flight-landing-first.xml`: `1/1`, exit `0` |
| Source-final Task 25 fixture | `task25-production-e2e-flight-landing-final-green.xml`: `10/10`, exit `0` |
| Extreme bounded critic | `PROCEED` after the five original blockers and final flight/landing boundary were rechecked |

The source-final E2E starts in the production Vesper scene, advances the bound
production flight model across more than 400 world units, satisfies the
authored lane/speed gate, lands only through `LandingSequence`, transitions to
the real surface, performs both real Lens scans and the comparison, samples
both real geyser controllers, recovers the fragment, presents all three
companion observations and verifies checkpoint 6 plus both durable fragments.

## Corrected Android artifact

`Builds/AndroidInternal/JustSomeStars-internal.apk` is 352,290,974 bytes with
SHA-256
`c7f55a58f8ea14b38c935616905fd440b8869e695a0066b96b4b0396fde735ca`.
It is a clean ZIP, package `com.scientificaj.justsomestars`, version code `1`,
version name `1.0`, min SDK 25, target SDK 36, label `Just Some Stars`, ARM64
only and debuggable. It verifies with APK Signature Scheme v2 and one Android
Debug signer. The build report is `Succeeded` with two warnings and zero
errors; Addressables succeeded and the report includes both dedicated plume
assets.

## Honest device boundary

The earlier paid-device session proved Vesper flight, landing, low-gravity
traversal, Lens focus and one spectrum on the pre-final APK. It did not complete
the corrected chapter and is not relabeled as evidence for the final hash.
After the correction, Limrun reported no remaining credit and
`lim android list --json` returned `[]`; no local Android device was attached.
The source-fresh production E2E therefore closes Task 25 locally. JSS-025 owns
one corrected exact-APK completion/reload spot-check when device capacity is
available, without reopening the accepted implementation.
