# Task 30 Android device matrix

Last updated: 2026-09-03

## Exact artifact under test

- Path: `Builds/AndroidInternal/JustSomeStars-internal.apk`
- Size: 619,148,779 bytes
- SHA-256: `a7dba5e20f96a182f2a47e1a539aa4416f6195a4bdaafc2910734fba97402a0e`
- Package: `com.scientificaj.justsomestars`
- Version: 1.0 (code 1)
- Android: minimum SDK 25, target SDK 36, ARM64, internal/debug-signed

The local artifact passed ZIP, manifest, signature, package, ABI, license,
forbidden-asset and build-residue inspection. Those checks do not prove runtime
behavior on a device.

## Session availability

The user authorized one paid Limrun/Argent session and approved Limrun as a
substitute for the unavailable physical Realme. The create request was rejected
before instance creation because the organization has zero remaining credits
and no active subscription. No physical Android device is attached.

Evidence:
`Builds/DeviceEvidence/task30-final/limrun-capacity-blocker.txt`.

Postcondition: zero Limrun instances, zero attached ADB/Argent devices, Argent
server stopped, no tunnel or inspector lease created. Therefore every device
row below remains pending rather than being inferred from automated tests or an
older APK.

## Lifecycle and artifact matrix

| Check | Status |
|---|---|
| Install exact APK and hash installed `base.apk` | Pending device capacity |
| Absent-PID cold launch to Frontend | Pending device capacity |
| Offline launch and truthful unavailable services | Pending device capacity |
| Background/resume with stable PID and state | Pending device capacity |
| Root Back process exit and clean filtered log | Pending device capacity |
| Vesper flight through Koro completion and reload (JSS-025) | Pending device capacity |
| Mirra, flight and Aster representative interaction | Pending device capacity |
| No fatal/managed exception or duplicate initialization | Pending device capacity |

## Responsive and accessibility matrix

For every row, check both landscape directions, the normal phone viewport, a
foldable/resized viewport, safe area, 48dp targets, scroll reachability and no
clipping/overlap.

| Surface/state | Standard | 1.35 text | Combined accessibility | Foldable resize |
|---|---:|---:|---:|---:|
| Frontend no-save and valid/recovered/unreadable states | [ ] | [ ] | [ ] | [ ] |
| Credits top, both license tails and reopen-to-top | [ ] | [ ] | [ ] | [ ] |
| Opening and customization | [ ] | [ ] | [ ] | [ ] |
| Mirra HUD, dialogue, interaction and Lens | [ ] | [ ] | [ ] | [ ] |
| Koro HUD, dialogue, Lens and durable reload | [ ] | [ ] | [ ] | [ ] |
| Vesper/flight HUD and Photo Mode | [ ] | [ ] | [ ] | [ ] |
| Aster HUD, objective and finale route | [ ] | [ ] | [ ] | [ ] |
| Clubhouse pause/customization/account/birthday | [ ] | [ ] | [ ] | [ ] |
| Atlas, optional shop and Restore flow | [ ] | [ ] | [ ] | [ ] |

## Performance matrix

| Segment | Duration | Median / 1% low | CPU / GPU | Peak PSS | Thermal / battery | Status |
|---|---:|---:|---:|---:|---:|---|
| Mirra representative play | 20 min | — | — | — | — | Pending |
| Vesper/flight representative play | 20 min | — | — | — | — | Pending |
| Aster representative play | 20 min | — | — | — | — | Pending |

The exact thresholds, units and sampling requirements are frozen in
`docs/qa/performance-results.md`. A Limrun result, when capacity returns, is a
cloud-emulator substitution and cannot be relabeled as Realme thermal proof.
