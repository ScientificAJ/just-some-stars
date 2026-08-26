# Just Some Stars production execution ledger

Last updated: 2026-08-27

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
- Replacement Task 12 Stages 1 through 3 are complete and accepted. Stages 1
  and 2 are committed and pushed; the scoped Stage 3 checkpoint is ready to
  commit and push before Stage 4 begins automatically.
- The local Unity Editor demo was closed cleanly before final verification.
- No APK or Limrun session is part of this Stage 1 checkpoint.

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

## Immediate next task: replacement Task 12 Stage 4

Produce bespoke Mira, Juno, Kai, Bea and Ori sprite sets from their approved
Task 10 sheets using the Stage 2 atlas contract and the accepted Stage 3
runtime integration. Continue autonomously; the user-facing graphical approval
remains at the Task 12 boundary, not between these internal stages.

## User-help queue

No user decision or external action is currently blocking progress.

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
