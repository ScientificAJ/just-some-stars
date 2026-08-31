# Mirra mobile quality review

Date: 2026-08-31 (Asia/Kolkata)

## Authority and exact artifact

- Locked target: `outputs/just-some-stars-2.5d-gameplay-target-v1.png`,
  SHA-256 `72644970448effd81177222e0aa23ae8a23f9b733077dab6e27e9ca765f5eaed`.
- Android development APK: `Builds/AndroidInternal/JustSomeStars-internal.apk`,
  329,620,194 bytes, SHA-256
  `ebce83c1a4fe13ab4efc5b8edc586d40d69e1e01ebd0d3b3f351fdd71afadb5c`.
- The installed `base.apk` hash matched the local APK exactly.
- Build target: Android, Unity `6000.3.22f1`, ARM64 player, development/debug
  signing. The Unity build report completed `Succeeded` with 2 warnings and
  0 errors.

## Device and comparison route

The bounded gate used Limrun instance
`android_assa_01m1as87w4e0dtq433xhqzdcaq` in the Asian jurisdiction:

- model `limdroid_x86_64_only`, Android API 35;
- physical display `720 x 1616`, density `280`, font scale `1.0`;
- settled app bounds `1616 x 720` landscape;
- Vulkan renderer `Intel(R) Graphics (RKL GT1)`, Mesa `25.2.0`.

Performance and Balanced were captured from the same APK, checkpoint-4 save,
camera and scene. The fixture is
`Builds/DeviceEvidence/task19-final-exact-apk/replacement-resume-save.json`,
SHA-256 `d2abbd643b8a58bb414592513320a7c780fb46e5c192ccf9e9b070c1a6911740`.
It is a bounded visual fixture, not a claim that this session replayed the
already-proven Flight route.

The direct comparison is
`outputs/quality-reviews/mirra-device-capture.png`, SHA-256
`666ec6737df4f7212636271d3bb84f165ca1cf8a9ede0faf76311fe8c7555f45`.
Its top panel is the locked 1672 x 941 target; the bottom panels are exact
1616 x 720 Performance and Balanced device captures scaled equally for review.

## Visual verdict

| Lens | Verdict | Evidence |
|---|---|---|
| Material credibility | Pass | Fabric, metal, rock, ice and visor use the live alpha-preserving surface graph with distinct normal, surface and palette masks; Signal and atmosphere use the live alpha-preserving emission graph. The same graph path preserves authored sprite transparency and band ownership. |
| Orange/blue lighting | Pass | The warm observatory/sun and cool Signal-spire halves remain immediately readable in both profiles without flattening actors or HUD. |
| Silhouettes | Pass | Captain, Mira, Juno and Ori remain distinct from the route and ship at the supported mobile viewport. |
| Density and hierarchy | Pass | Exactly eight composition bands, three bounded lights, one Signal particle system and one global volume preserve the Task 12 ceilings. |
| HUD clarity | Pass | Objective, crew rail, status copy and four touch zones remain legible and unobstructed. |
| Focal point | Pass | The observatory establishes the warm origin; the violet spire remains the unambiguous destination. |
| Emotional impact | Pass | The accepted sunset-to-frozen-night storybook contrast survives the lower-cost Performance profile. |

This is a measured preservation and material/profile-system uplift over the
approved Task 12 production proof; it is not described as pixel-identical to
the painted concept. Performance intentionally reduces render scale, one
light, particles, volume weight and parallax motion without disabling gameplay.

## Performance observation

After a 25-second warm sample in Performance, SurfaceFlinger provided 127 presented
frames / 126 intervals:

- mean `33.3320 ms`, median `33.3179 ms`;
- p95 `34.2619 ms`, maximum `36.3567 ms`;
- `0` intervals over `40 ms`.

Total PSS was `865,920 KB` and total RSS `1,186,096 KB`. PSS is 128,087 KB
(`17.36%`) above the older Task 12 observation of 737,833 KB. That increase is
not hidden or attributed without a profiler: Tasks 13–20 added production
runtime/content after the baseline. Task 30 owns the broad memory/GPU capture
and must explain or reduce the delta before release-device approval.

## Automated gates

- corrected `MirraQualityAssetTests`: `3/3` passed.
- corrected `MirraQualityControllerTests`: `2/2` passed.
- affected `Mirra2DAssetValidationTests`: `5/5` passed.

The Linux Editor backend reports dynamic resolution unsupported, so its focused
test proves the selected scalable-buffer intent and only asserts the native
camera flag when the backend supports it. The exact Android player reports
`systemSupportsDynamicResolution=True`; Performance then reports
`renderScale=0.800`, `scalableBufferPath=True` and
`cameraAllowDynamicResolution=True`, while Balanced truthfully reports all
three paths inactive at render scale `1.000`.

The authoritative corrected pair is `12-corrective-final-performance.png` and
`11-corrective-final-balanced.png`. Captures `00` through `10` are superseded
candidate, clean-install or pre-correction diagnostics and are excluded from
the final comparison.

The instance was restored to Balanced, deleted after capture, and the final
Limrun instance list was empty. The exact device facts and teardown record are
preserved in `Builds/DeviceEvidence/task20-quality-final/exact-final-provenance.txt`.
