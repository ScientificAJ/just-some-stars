# Just Some Stars issue register

This register holds findings that are real but outside the frozen acceptance
contract of the task currently being executed.

## Operating rule

- An out-of-scope finding is recorded here; it does not expand, block, or
  restart the current task.
- Severity does not automatically promote a finding into the current task.
- A finding is worked only when the user explicitly promotes it or its named
  owner task begins.
- Critics report against the frozen task contract once. Newly observed
  out-of-scope findings are appended here instead of starting another audit
  loop.
- Focused verification belongs inside implementation. Full regression runs
  happen once at the declared package/release boundary.

## Open findings

| ID | Finding | Owner / revisit point | Current-task effect |
|---|---|---|---|
| JSS-001 | Codemagic app/repository/workflow setup is complete, with 500 free macOS minutes available. The user approved deferring remote Unity execution because the account has Unity Personal and no Plus/Pro serial for CI activation. | Revisit only if a valid Unity CI license becomes available | None; do not spend build minutes |
| JSS-002 | Produce the signed Google Play AAB, begin closed testing, and create the Galaxy Seller application record when a playable release candidate exists. | Growth and release package (Tasks 31–33) | None |
| JSS-003 | Replace disabled Continue with the real navigation/content flow. Task 6's Settings/input, Task 7's recoverable saves and Task 8's mode/streaming foundations are complete; the committed Task 8 catalogue remains honestly empty until real destinations exist. | Later Frontend/content integration | None |
| JSS-004 | Evolve the temporary offline/footer copy when the first real online service is introduced. | Task 21/28 integration | None |
| JSS-005 | Investigate the two un-attributed persistent native allocations reported only during development-player process shutdown if they recur under stack-enabled profiling. | Later device/performance QA | None |
| JSS-006 | Improve Unity-canvas device automation discovery so Argent does not require the documented logical-coordinate fallback; add local recording support only if the missing encoder becomes necessary. | Tooling/QA maintenance | None |
| JSS-007 | Replace Task-specific CI test counts and smoke filters as later tasks expand or rename the suites. | Task 31 CI hardening | None |
| JSS-008 | Replace Task 8's honest `Frontend` streaming-failure fallback with the real Clubhouse scene once that ship-hub scene exists. | Task 26 Clubhouse integration | None; Task 8 must not ship a fake hub scene |
| JSS-009 | If typed events later publish across worker threads, define and test the stronger contract for a subscriber being disposed while its snapshotted callback is already in flight. | First cross-thread event producer | None; Task 9 guarantees type-safe snapshot dispatch and idempotent removal, not cancellation of an already-running callback |
| JSS-010 | Consider introducing an `IGameEvent` marker only if arbitrary non-game payloads are actually published through the generic bus. | First event-vocabulary expansion | None; current callers and published contracts use only the six typed game events |
| JSS-013 | Decide whether to retain, move to an archival branch, or delete the uncommitted Task 12 full-3D Captain source, textures, scripts and evidence after the 2.5D pipeline is proven. | After replacement Task 12 Stage 5 passes | None; preserve the files and do not resume Stage 4 rigging |
| JSS-014 | The accepted Stage 1 proof uses static whole-character PNG cutouts, so legs and other body parts do not animate during movement. Replace the proxies with coherent validated frame-atlas clips rather than deforming the approved stills. | Replacement Task 12 Stages 2–4 atlas pipeline and character production | None; expected limitation of the Stage 1 runtime proof |
| JSS-015 | The Stage 1 proof allows the Captain proxy to leave the authored traversal area and fall indefinitely because it has no recovery volume or safe respawn anchor. Add bounded world recovery to the final Mirra segment. | Replacement Task 12 Stage 5 integration | None; expected limitation of the Stage 1 runtime proof |

## Completed tasks

| Task | Completed | Evidence |
|---|---|---|
| Award-level Frontend redesign | 2026-08-22 — user approved | Approved landscape main screen and all local panels; EditMode `211/211`; PlayMode `70/70`; internal APK SHA-256 `9e85f4ec24d20c62a786997ac4a42dab132a79118e401f2c1d3efa61b3ff6b83` |
| Release Runway local foundation | 2026-08-22 — user approved revised exit | Installable tested Android skeleton, pushed repository, Codemagic app/workflow configured with remote Unity execution explicitly deferred, and store submission moved to the final Growth and release package |
| Task 6 — settings, accessibility and semantic input | 2026-08-22 — complete | Versioned atomic device-local settings; 20-control Frontend Settings surface; one project-wide semantic Input Router; composition-owned injection/reload/teardown; focused final gates `11/11` settings, `6/6` input, `11/11` assets, `11/11` controller, `1/1` motion, `12/12` bootstrap, `4/4` dependency/reload and `1/1` real launch; bounded critic PROCEED |
| Task 7 — versioned local saves and recovery | 2026-08-23 — complete | Schema-v1 story/Captain/discovery/cosmetic/Atlas/photo/birthday metadata; durable temporary write and validated backup rotation; explicit recovery results; ordered future migration registry; deterministic merge with typed player-choice conflicts; focused final gates `14/14` local save, `8/8` migration, `12/12` bootstrap and `1/1` real launch; bounded critic PROCEED |
| Task 8 — game modes and additive scene streaming | 2026-08-23 — complete | Exact guarded base-mode/overlay matrix and input/camera policies; version-1 Addressables scene catalogue at `jss.scene-catalog`; additive held activation, monotonic progress, exact-once cleanup, safe fallback and idempotent shutdown; permanent five-service bootstrap with no development initializer; focused final gates `7/7` modes, `2/2` catalogue, `12/12` streaming, `10/10` bootstrap and `1/1` real launch; all three bounded critic lifecycle findings reproduced and resolved |
| Task 9 — content IDs, typed events and editor validation | 2026-08-23 — complete | Immutable validated `ContentId`; six typed event payloads and snapshot-dispatch bus; deterministic contributor-based validation for duplicate IDs and every required reference/binding family; runtime events `4/4`, content validation `9/9`, full EditMode `262/262`, affected PlayMode Boot `8/8`, bootstrap `11/11` and cross-fixture `2/2`; CLI validator green; Android APK SHA-256 `3b10344f54aa82d853c8c5459b3f87950b594bf56a138f23b041fc7a27d79933`; Limrun/Argent Boot-to-Frontend, same-PID resume and root-Back exit smoke passed; bounded critic blocker reproduced and resolved |
| Runtime foundation QA remediation | 2026-08-23 — complete | JSS-011/JSS-012 promoted by the user; dependency-free tooling tests `11/11`; real isolated PlayMode manifest `90/90` across 10 Unity processes; one OS-locked inspector lease; bounded critic PROCEED |
| Task 10 — approved character reference sheets | 2026-08-23 — complete | All `12/12` required reference PNGs explicitly approved and hash-locked in `docs/art/character-reference-approval.md`; cinematic storybook style, cast scale, customization, individual turnarounds, expressions, equipment and material authority recorded; written semantic and dimensional contracts override incidental illustration errors in downstream builds |
| Task 11 — Blender source/export pipeline | 2026-08-23 — complete | Blender 5.2 scene setup, strict validation, deterministic LOD generation, transactional FBX/JSON export and scoped Unity import policy; Blender `13/13`, Unity importer `6/6`, real Generic primitive `.blend`→FBX round-trip, matching source/export hashes and staged Git LFS pointers; bounded critic PROCEED; no Task 12 hero asset produced |
| Task 12 Stage 0 — layered-2.5D production pivot | 2026-08-25 — user approved | Canonical 2.5D target hash-locked; surface and flight changed to authored 2.5D; character production changed to deterministic frame atlases; old 3D Stages 1–3 preserved as superseded history and Stage 4 stopped; implementation resumes at replacement Task 12 Stage 1 |
| Task 12 Stage 1 — layered 2.5D runtime proof | 2026-08-26 — complete | Eight-band URP 2D proof with independent parallax, `Rigidbody2D` surface movement, composition camera, real Captain/companion/Ori cutouts, touch controls and separate collision; permanent Settings/Input/Mode push-injection and lifecycle teardown; focused layer `5/5`, parallax `2/2`, motor `4/4`, camera `3/3`, production binding gates `3/3`, affected Frontend binding `1/1`; proof/comparison hashes recorded in the pivot plan; bounded harsh critic `PASS`; commit intentionally pending user request |

## Resolved or promoted findings

Move an entry here only when its owner task starts or the user explicitly
promotes it. Record the resolving task or commit instead of deleting history.

| ID | Resolution | Evidence |
|---|---|---|
| JSS-011 | Resolved by the 2026-08-23 runtime-foundation QA remediation. Full PlayMode verification now uses the source-validated fixture manifest and one Unity process per fixture; the invalid aggregate is no longer a gate. | Strict runner summary: 10 fixtures and `90/90` selected tests passed; stale/missing/malformed/zero-test reports fail closed. |
| JSS-012 | Resolved by the 2026-08-23 runtime-foundation QA remediation. Limrun supplies the emulator/install/tunnel while an identity-bound lease gives exactly one UI-inspection backend ownership. | Cross-process tests prove competing Argent/Limrun-UIAutomator claims cannot both succeed and guarded commands retain the OS lock through completion. |
