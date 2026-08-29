# Just Some Stars production execution ledger

Last updated: 2026-08-30

This tracked ledger preserves the active production state, decisions, evidence,
next action and user-help queue across context compaction. It complements the
implementation plans and issue register; it does not replace either authority.

## Locked execution contract

- Complete Tasks 12 through 30 inclusive, then stop before Task 31 so the user
  can return for ShipKit integrations and final publishing work.
- Execute one task at a time. Finish, verify, commit, push and report a
  checkpoint, then automatically begin the next task unless the user interrupts.
- Replacement Task 12 Stages 1 through 5 each receive one independent,
  maximally strict critic review. Later Tasks 13 through 30 each receive one.
- Every critic is spawned without conversation history and receives only the
  authoritative documents/references, work paths and evidence. The critic is
  maximally strict but civil and returns a bounded PASS or HOLD.
- The critic approves implementation autonomously. A new canonical graphical
  result for gameplay, a character or another major visual direction is shown
  to the user for approval between tasks, not repeatedly between internal steps.
- Use focused verification during a task. Run full regressions, APK builds and
  paid-device QA only at the package boundaries declared by the plans.
- Commit and push each completed task. Merge into `main` at work-package
  boundaries rather than after every task.
- Record real out-of-scope findings in `docs/issue-register.md`; do not expand
  the current task into an unrelated repair pass.
- Accumulate non-blocking user questions here. Open Firefox only when a genuine
  decision or external action blocks further progress.
- Preserve the superseded uncommitted Task 12 full-3D Captain work outside
  replacement-task commits until replacement Stage 5 passes.

## Authority order

1. Direct user decisions and approved reference images.
2. `docs/superpowers/specs/2026-08-25-2.5d-gameplay-pivot-design.md`.
3. `docs/superpowers/plans/2026-08-25-2.5d-gameplay-pivot.md`.
4. `outputs/just-some-stars-technical-build-plan.md`.
5. `outputs/astronomy-adventure-game-blueprint.md`.
6. `docs/art/2.5d-visual-approval.md` and other approval ledgers.
7. `docs/issue-register.md` for explicitly deferred findings.

## Current repository state

- Canonical repository: `/mnt/unity-data/JustSomeStars`.
- Branch: `codex/task-12-modular-characters`.
- Pre-Stage-2 HEAD: `f99336333e6946ccd50c1ec43ffdb8b2aa3ac617`.
- Replacement Task 12 Stages 0 through 5 are complete and accepted. Stages 1
  through 4 are committed and pushed; Stage 5 passed its package boundary,
  critic review and explicit user visual approval and is ready for its scoped
  completion commit.
- The final internal APK SHA-256 is
  `6e9597e0f1ae498d0fdf0f6d46d6032279b150f29e4a6906ebb09da4ecbd9d8d`.
- One bounded Limrun instance was used and timed out after final capture;
  `lim android list --json` is empty, the Argent lease is closed and its local
  server is stopped.
- Superseded full-3D Captain work remains deliberately uncommitted and
  preserved pending the user decision tracked as JSS-013.

## Replacement Task 12 Stage 1 checkpoint

### Delivered scope

- Eight explicit URP 2D composition bands with independently owned parallax.
- Sky, far terrain, atmosphere and midground own distinct visible sprite
  assets, Addressables identities, lighting masks and registered alpha frames.
- Orthographic composition camera and `Rigidbody2D` surface movement.
- Separate Captain, companion and Ori cutouts rather than one flattened image.
- Truthful Primary interaction and Secondary jump/jet semantics across keyboard,
  gamepad and touch, plus canonical Settings/Input/Mode composition injection.
- Editor-only local demo launcher that does not enter build settings or ship in
  a player; the user exercised move, jump and jet in Unity Play Mode.
- Layered proof and reference comparison under
  `Builds/VisualEvidence/task12-stage1/`.

### Locked evidence identities

- Approved target: `outputs/just-some-stars-2.5d-gameplay-target-v1.png`.
- Approved target SHA-256:
  `72644970448effd81177222e0aa23ae8a23f9b733077dab6e27e9ca765f5eaed`.
- Actual center runtime proof SHA-256:
  `c672825fd350c87e209b1f97608b039deaad23d310e833ba635814f89685010d`.
- Actual left camera-limit proof SHA-256:
  `6cb71a9f9e36486940dfd1300370817da861aa1f075e629f06805819e4e6e687`.
- Actual right camera-limit proof SHA-256:
  `64c25ccd1be66b8830d7b2c2749246f13aa847c3aff72be8151a8ea9d047f0fa`.
- Final proof-scene SHA-256:
  `2be17f63a6b662323883bdb676886bef5d9791394b3ba40f2946154299e2365a`.
- Canonical input-actions SHA-256:
  `43564e5bb3bcc87fcbeec4548e3c03bd6829af1585fff2e2f6dccd5f932d55b3`.
- The earlier proof/comparison hashes are superseded and are not final
  acceptance evidence.

### Known Stage 1 limits

- JSS-014: whole-character proof PNGs are intentionally static; Stages 2–4
  replace them with coherent frame-atlas animation.
- JSS-015: the proof has no fall-recovery volume; Stage 5 owns bounded recovery
  and safe respawn anchors.
- Neither limit weakens Stage 1's declared architecture/visual-proof contract.

### Final gate

- Independent Stage 1 extreme critic: final `PASS` after direct comparison of
  the center and both camera-limit captures with the canonical target.
- Fresh focused EditMode checkpoint:
  `task12-stage1-edge-detail-final-green-rerun.xml`, `7/7` passed with zero
  fail/skip/inconclusive after an authentic `6/7` scanline-detail RED.
- Fresh combined focused PlayMode checkpoint:
  `task12-stage1-final-playmode.xml`, `15/15` passed with zero
  fail/skip/inconclusive across the motor, camera, parallax, gameplay lifecycle,
  interaction, permanent composition and Frontend reload contracts.
- Both focused logs have no C# compiler error, unhandled exception, WeakPtr or
  test-failure diagnostic.
- Asset tree has zero missing metas, orphan metas, empty asset directories or
  executable asset files; LFS routes all Stage 1 PNGs.
- Stage 1 checkpoint: complete, committed and pushed on
  `codex/task-12-modular-characters`.

## Replacement Task 12 Stage 2 checkpoint

### Delivered scope

- One public coherent-strip transaction covering extraction, structural-facing
  validation, baseline registration, atlas/manifest assembly and preview.
- Schema-checked output ownership: invalid requests clean only their owned
  stale artifacts; markerless and differently owned paths are preserved.
- Global normalized sprite-name collision rejection before publication.
- Source-only facing markers, zero enclosed runtime alpha holes and stable
  opaque painterly detail.
- Scoped Unity atlas import, versioned `CharacterSpriteSet` definitions and
  deterministic manifest-driven animation/events.
- One temporary painterly primitive with four idle frames and eight alternating
  run frames. It proves the pipeline, not final Captain or crew likeness.

### Locked evidence identities

- Source request SHA-256:
  `6f8fd0d12aa13195ed5ea05fe7e904fcd61fef637d6aa9886fa77263f2d255a1`.
- Idle source SHA-256:
  `6569e04553f3d313be9fa9f97cf650859efff225967671f9e7b9fb0d041d609f`.
- Run source SHA-256:
  `fc969bcfde8aa3f3a899dd5550b6772a61f3094d23432146b04c53c6ac09b01e`.
- Canonical atlas SHA-256:
  `61a7448e3cfb80b634095037bccbaeefbbc927d4ec9b4771cc7b1bc6a7e79d0e`.
- Canonical manifest SHA-256:
  `507e9865978ed3eaf30573141f7fd4be759d056bb8ae8db7cbe560465dae1621`.
- Final contact-sheet SHA-256:
  `d2b8cf5aceb371f44e49ee99602f99f2532f1bc1c33f961d672f051cdfb52298`.
- Final animated-preview SHA-256:
  `27bb341f5aef897357e8147c0590ecd0c5d1517653b82cd77edeea3c40fe664c`.

### Final gate

- Python sprite-pipeline suite: `13/13` passed after authentic REDs for stale
  owned cleanup, markerless overwrite, cross-character deletion, normalized
  name collisions and runtime-pixel artifacts.
- Unity `CharacterSpriteImportTests`: `6/6` passed.
- Unity `SpriteAtlasAnimatorTests`: `4/4` passed.
- Independent Stage 2 extreme critic: `PASS` after the bounded ownership,
  collision and visual-pixel corrections.
- No APK, full regression or Limrun session was used for this focused stage.

## Replacement Task 12 Stage 3 checkpoint

### Delivered scope

- Compact, Average and TallBroad Captain sprite sets with equal capability and
  visibly distinct approved silhouettes at 1.46 m, 1.56 m and 1.66 m.
- Eight coherent motions in both facings: idle, run, turn, jump, land, climb,
  scan and interact.
- Exactly five synchronized runtime layers: body/base, head/hair, silhouette
  costume, backpack/equipment and foreground hand/tool.
- Palette masks, bounded modules, stable attachment anchors and one
  manifest-driven Unity `CharacterSpriteSet` contract per family.
- Stage 1 demo integration with live family, tone, iris, hair, Signal and
  equipment customization.
- Corrected jump/land publication: all five modular layers remain present and
  articulated without global identity-layer scaling.

### Locked evidence identities

- Captain package manifest SHA-256:
  `146161089615df11a74f1ccc7a81e68a94afc05eb69dca7d34459d7763dbf14c`.
- Family lineup SHA-256:
  `9b59da0cae3cb5f9fac15a641417a1e4850189bc5bb7f6b881fc8757d389249e`.
- Runtime three-capture composite SHA-256:
  `11f01204ac039f5daa950ee9a5a59eecdfc9987767c5aaa0682bdb1a3f6ca571`.
- Tracked runtime-evidence manifest SHA-256:
  `f49613639ed893940385f6ce27904e55ad505dbfc4fa00ce69ccf73b15f2a076`.

### Final gate

- Python Captain package suite: `6/6` passed.
- Unity focused EditMode: `4/4` passed.
- Unity focused PlayMode: `3/3` passed with llvmpipe graphics.
- Three fresh real Unity runtime captures and 24 sequence frames are bound by
  a 46-artifact evidence manifest.
- The same independent Stage 3 extreme critic that found flattened/scaled
  jump/land publications returned final `PASS` after the bounded correction.
- No APK, full regression or Limrun session was used for this focused stage.

## Replacement Task 12 Stage 4 checkpoint

### Delivered scope

- Bespoke, independently authored Mira, Juno, Kai and Bea frame sets plus a
  dedicated mechanical Ori set; no child is a recolor of another.
- Eight clips in right and left facings for every character: idle, run, turn,
  jump, land, climb, scan and interact.
- Character-specific silhouettes, tools and motion semantics with stable
  baselines, live attachment/event anchors and deterministic atlas manifests.
- Corrected actor-only climb and interaction sources, full-body Bea scan rows,
  and a bounded Juno crouched-interaction scale in both facings.
- Five typed Unity `CharacterSpriteSet` assets, scoped import policy and a
  dedicated Addressables `Characters-Crew` group.

### Locked evidence identities

- Crew package index SHA-256:
  `2a100b886020eda0bd2cf9d8a5e78a8cd5024e9a06da97e036a436fb6fa15a0e`.
- Runtime capture manifest SHA-256:
  `ed41e1efe0f0de91364efa3857c8712c18acf306c449cf4d0cf4ce3d1db64c10`.
- 96-frame both-facing runtime reel SHA-256:
  `2faf97dea16d969bd52fad7879fabf21c5ab4cc29dceaa513da3c69d08ed7e12`.
- Right same-scale lineup SHA-256:
  `400b3dd0dbdacaaa8f56cdebf12a0213e32f9a8f76d3df0c6b91fa23e089b474`.
- Left same-scale lineup SHA-256:
  `5dcb74ccebcfd4afc3c291c62d626641de28ea388df510ba418b98a4da203072`.

### Final gate

- Python crew package suite: `3/3` passed.
- Unity focused EditMode: `3/3` passed.
- Unity focused PlayMode: `1/1` passed.
- Fresh Unity evidence contains 96 frames: all eight clips for all five actors
  in both right and left facings, plus both same-scale lineups.
- The same independent Stage 4 extreme critic that found the four bounded
  publication/evidence/staging defects returned final `PASS` after correction.
- No APK, full regression or Limrun session was used for this focused stage.

## Replacement Task 12 Stage 5 checkpoint

### Delivered scope

- Six final Mirra environment publications plus normal/emission masks, five
  restrained HUD assets, actor shadow and Signal mote assets.
- Eight independently declared and rendered bands with matching runtime
  parallax factors; no flattened whole-scene underlay or 3D-character path.
- Accepted modular Captain, bespoke Mira and mechanical Ori presentations,
  each grounded by owned contact-shadow treatment.
- One authored traversal route, bounded fall recovery, interaction probe,
  Signal Lens target and composition-camera limits.
- Three bounded 2D lights, one particle system, normal/emission support and one
  final color-grading volume.
- A development-only Boot-to-Mirra route for the package-boundary proof; release
  routing remains truthful and unchanged.
- Lens moved from the URP-debug-conflicting gamepad shoulder binding to
  `buttonNorth`, with an authentic targeted RED/GREEN and exact-device proof
  that the debug overlay no longer opens.

### Locked evidence identities

- Final Candidate 9 SHA-256:
  `adc10f11de965625bff8fa81b902323bd293f173089adaf0908c0ffdd6367032`.
- User-approved final Android screenshot SHA-256:
  `3e402d344bfbbb9c02aa12a5a91dc6dcac455de27ea13eaa70027f2671c48848`.
- Final internal APK SHA-256:
  `6e9597e0f1ae498d0fdf0f6d46d6032279b150f29e4a6906ebb09da4ecbd9d8d`.
- Unity build-report SHA-256:
  `3a62ff34fa80d8d66fb58cb349eb488d474c7054da6662ec3decc5161da0ce33`.

### Final gate

- Full EditMode `293/293` passed.
- Source-validated isolated PlayMode runner passed 19/19 fixtures and 114/114
  tests; all Unity exits were zero.
- Lens conflict correction passed its focused `1/1` test after authentic RED.
- Android build succeeded with warnings 2, errors 0; ZIP, package, ARM64 ABI,
  v2 debug signature, manifest, Mirra scene and exact OFL/Apache payload gates
  passed; EmojiOne and EmojiCompat startup are absent.
- The independent Stage 5 extreme critic returned `PASS` for Candidate 9.
- The user explicitly approved the final graphical result on 2026-08-28.
- The user also correctly rejected the claim that the final video visibly
  proves inputs. It is stability evidence only; JSS-016 preserves the exact
  performance/lifecycle and visible-motion follow-up without reopening Task 12.
- Authored ceilings and honest device limitations are recorded in
  `docs/qa/task12-mirra-benchmark.md`.

## Task 15 checkpoint

### Delivered scope

- Exactly two human companions plus Ori are selected deterministically for an
  expedition, with authored formation positions and a bounded five-Hz decision
  cadence.
- Mira, Juno, Kai, Bea and Ori have validated personality assets with distinct
  approved attention domains.
- Mandatory story actions outrank safety/recovery, which outrank personality
  and ambient actions; quantized utility and ordinal IDs form a deterministic
  total order even for three or more near-equal candidates.
- The real Director tick owns dialogue tokens and interaction reservations,
  suppresses autonomous actions during cinematics and releases all leases.
- All 12 crew states execute through a typed 2D runtime route. Traversal uses
  only declared `TraversalGraph2D` nodes/depth transitions; no NavMesh exists.
- Recovery requires both the stranded actor and destination to be off camera,
  and handles blocked or excessively long authored routes without visible warp.

### Final gate

- Authentic focused REDs reproduced every bounded critic finding, including
  disconnected arbitration, unused traversal, arbitrary team/cadence input,
  visible recovery and non-transitive three-candidate near ties.
- Fresh `CrewUtilityTests`: `11/11` passed, exit code 0.
- Fresh `CrewRecoveryTests`: `8/8` passed, exit code 0.
- Both final logs are free of C# compiler, unhandled-exception and WeakPtr
  diagnostics.
- The same bounded harsh critic returned `PROCEED` after the final total-order
  correction.

## Immediate next task: Task 16

Task 15 is complete. Next, implement Discovery Lens modes, authored
instrument/phenomenon compatibility, prediction and evidence recording, and
typed discovery events without reopening the accepted crew, interaction,
motor, camera or Captain-presentation contracts.

## User-help queue

No user decision or external action is currently blocking Task 16. JSS-019
records the remote-push safety block without stopping continued local work.

## Chronological checkpoint log

- 2026-08-26: User approved the Stage 1 local Unity Editor demo and observed
  that static cutouts do not animate and the proxy can fall beyond the map.
  JSS-014 and JSS-015 were recorded with their correct later-stage owners.
- 2026-08-26: User locked autonomous Tasks 12–30 execution, the per-task
  commit/push/checkpoint sequence, per-stage/task independent critics, focused
  verification cadence, package-boundary merges and visual user approvals only
  between tasks.
- 2026-08-26: The independent critic identified four bounded Stage 1 gaps:
  flattened environment ownership, incomplete binding metadata, stale camera
  evidence and incorrect interaction semantics. Each was reproduced with
  focused RED evidence and corrected without starting Stage 2.
- 2026-08-26: The actual PlayMode capture exposed a horizontal alpha-layer seam.
  Full-rect import and shared vertical registration were each reproduced with
  a focused RED, corrected through temporary self-removing Unity patchers and
  visually recaptured without the seam.
- 2026-08-26: Final Stage 1 verification passed `7/7` focused EditMode and
  `15/15` focused PlayMode tests. Artifact and scene hashes were re-read from
  disk and match the authority values above.
- 2026-08-26: The final critic held on visible scanline/curtain artifacts at
  both camera limits. A strengthened asset contract reproduced the defect as
  `6/7` RED; grounded painterly overscan replaced only the two affected layer
  margins. Fresh center/x=-2/x=+2 captures are clean, focused verification is
  `7/7`, and the same critic returned `PASS`.
- 2026-08-26: Stage 2 completed the deterministic sprite-atlas round trip.
  Its critic found unsafe ownership, normalized-name collision and runtime
  alpha/marker artifacts. Each was reproduced with focused RED tests, corrected
  without broad regression work, and re-reviewed to `PASS`; final focused gates
  are Python `13/13`, EditMode `6/6` and PlayMode `4/4`.
- 2026-08-27: Stage 3 completed the modular Captain package. The same extreme
  critic held on flattened/scaled jump and land publications; focused tests
  reproduced both defects. The corrected clips keep all five layers present
  and independently articulated. Final focused gates are Python `6/6`,
  EditMode `4/4` and PlayMode `3/3`, with final critic `PASS`.
- 2026-08-27: Stage 4 completed the bespoke Mira/Juno/Kai/Bea and mechanical
  Ori package. Its bounded critic held on climb semantics, live anchors, Juno
  scale, Bea scan framing and both-facing evidence; the corrected package
  passed Python `3/3`, EditMode `3/3`, PlayMode `1/1` and critic review.
- 2026-08-28: Stage 5 completed the final Mirra scene, full package regression,
  exact Android build and one paid-device session. Candidate 9 received critic
  `PASS`; the exact-device image received user approval. The user's correction
  that the final video does not visibly prove interaction was accepted and
  recorded in the evidence, benchmark and JSS-016 rather than defended.
- 2026-08-28: Task 13 implemented the production motor, body calibration and
  composition camera and produced a fresh exact Android build. Focused prefab,
  motor, camera, lifecycle, affected-Mirra and real touchscreen-to-virtual-
  gamepad route tests are green. The first replacement Limrun session proved
  real movement and airborne Jump, but expired before one continuous Interact
  and Lens recording; its incomplete footage is retained as diagnostic evidence
  only and Task 13 remains active.
- 2026-08-28: A second explicitly approved Limrun session proved the exact APK,
  full landscape bounds, real movement, Jump and Lens response. It also proved
  Argent two-pointer injection unsuitable for this Unity target and corrected
  the ADB coordinate contract to logical `1616x720`. The route overshot the
  world-X `5.25` interaction probe and fall recovery reset the Captain before
  `SIGNAL LINKED`; both recordings remain diagnostic, all paid resources were
  cleaned up, and Task 13 remains active pending one measured final capture.
- 2026-08-29: A third explicitly approved ten-minute Limrun session installed
  the same APK SHA-256 `800fd80a...dbf7`, preserved clean `1616x720` launch and
  calibration frames, and isolated the remaining control detail. A stationary
  `(205,575)` hold is ignored; a center-to-right drag moves but ramps the
  joystick; the next route is a genuine `(204,575)` to `(205,575)` one-pixel
  full-right drag for `1,150 ms`, calculated to end inside the world-X `5.25`
  console radius. The hard timeout arrived before that correction could run.
  No final video is claimed; the lease, tunnel and Argent server are closed,
  `lim android list --json` is empty, and Task 13 remains active.
- 2026-08-29: Task 13 completed after a second, stricter lower-leg correction.
  The user correctly rejected `final-lower-leg-run-v2.mp4`: the first fix kept
  the boot aligned with a translated leg but the full leg still tore away from
  the pelvis and glitched during motion. An authentic pixel-connectivity RED
  found two significant alpha components in Average/right run frame 2. The
  production generator no longer translates whole run legs. Each leg remains
  hinged to its authored hip, swing clearance comes from a bounded scale around
  that hip, and the lower-leg/boot anchors use the identical scale and rotation.
  All six family/facing run sets now remain one connected body component, with
  minimum runtime foot distance above the `18 px` floor. Final focused gates are
  Captain package Python `6/6`, importer `6/6` and actual layered renderer
  `3/3`. The rebuilt Android APK SHA-256 is
  `9c8652adcdd8dbc315e9c3a58cedcb3ba4191449a2acbc02f481617b59e14d12`.
  Its accepted `1616x720` runtime excerpt
  `final-connected-stride-runtime.mp4` has SHA-256
  `cb23adc61887171edc0e7443a80e22d696205d9a1c6a0ab442a6bbf80326d008`;
  both legs and boots remain connected and readable through the stride, and a
  fresh harsh critic returned `PASS` after reviewing every frame in seconds
  28–32 for detachment, popping, visibility and mobile readability. Two
  earlier bounded diagnostic instances expired, the final instance was deleted,
  `lim android list --json` returned `[]`, Argent and all tunnels were stopped,
  and Task 14 is next.
- 2026-08-30: Task 14 completed contextual interactions and reservations.
  Captain, Juno and Ori select distinct anchors through exact 2D distance,
  facing, physics-layer, depth-band, game-mode and required-tool filters. The
  first critic pass found duplicate clip identity, peer-cancellation and active
  timeout gaps; authentic focused REDs reproduced all three. The corrected
  runner cancels blocked peers on first fault or deadline, recovers all reserved
  actors, releases all leases and publishes no events on failure. Fresh focused
  EditMode and PlayMode fixtures each pass `5/5` with exit code 0, and the same
  harsh critic returned `PROCEED`. JSS-019 records that the platform rejected
  the authorized remote push on destination-trust grounds; local commits remain
  intact and Task 15 is next.
- 2026-08-30: Task 15 completed the Crew Director and personality brains.
  Exactly two humans plus Ori now use authored personalities, deterministic
  formation, strict story/safety/personality priority, real dialogue and
  interaction leases, all 12 crew states, declared 2D traversal and invisible
  recovery. The critic's six initial integration findings were reproduced and
  corrected. Its final three-candidate near-tie example then exposed the last
  pairwise-epsilon defect as an authentic `0/1` RED; global score buckets now
  form a true order-independent total order. Fresh focused EditMode is `11/11`,
  PlayMode is `8/8`, both exit 0, and the same critic returned `PROCEED`.
  Task 16 is next; JSS-019 still prevents remote delivery but not local work.
