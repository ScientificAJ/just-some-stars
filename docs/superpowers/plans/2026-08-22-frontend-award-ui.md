# Frontend Award-UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Task 5 Frontend to match the five approved cinematic landscape references with live controls, licenses and animation-ready 2.5D art.

**Architecture:** Immutable reference images drive a layered Unity uGUI/TMP prefab. Image-generated art layers provide the scene and tactile instrument surfaces; focused runtime components provide ambient and interaction motion without owning product state. The prefab is verified independently, then replaces the old Frontend hierarchy through Unity Editor APIs.

**Tech Stack:** Unity 6000.3.22f1, URP 17.3.0, uGUI, TextMeshPro, Input System 1.20, C# 9, built-in ImageGen, Unity Test Framework, Android CLI build tooling, Limrun/ADB device QA.

**Spec:** `docs/superpowers/specs/2026-08-22-frontend-award-ui-design.md`

## Global Constraints

- Work only in `/mnt/unity-data/JustSomeStars`; do not create another worktree.
- The five files under `outputs/frontend-redesign-targets/` are immutable visual authorities.
- Preserve all existing Frontend functionality and exact player-facing text.
- Lock the Frontend to landscape; support both landscape directions and safe areas.
- Use real Buttons, TMP text and ScrollRect content; do not ship a baked-screen or invisible-button approximation.
- Generate real raster layers and icon art; do not replace visible assets with code-drawn approximations.
- New runtime behavior follows RED/GREEN; generated assets and Editor-generated serialization are validated rather than unit-tested gratuitously.
- Do not run remote CI, upload to a store, mutate seller records or begin Task 6.
- Only the harsh critic may be used as a subagent, and only for the final fidelity gate.

---

### Task 1: Lock target evidence and focused RED contracts

**Files:**
- Create: `outputs/frontend-redesign-targets/*.png`
- Create: `Assets/_JustSomeStars/Tests/EditMode/FrontendRedesignAssetTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/FrontendMotionTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/EditMode/FrontendSceneAssetTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/Task5LaunchIntegrationTests.cs`

**Interfaces:**
- Consumes: the five hashes in the design spec and existing `IFrontendView`/`IFrontendLifecycle` behavior.
- Produces: failing contracts for `FrontendVisualRoot.prefab`, required art/font bindings, landscape settings and settled motion.

- [ ] **Step 1: Add the EditMode target/prefab contract**

Assert exact target hashes, target dimensions/aspect, a landscape-only PlayerSettings contract, the required prefab hierarchy, live TMP copy, real Buttons, ScrollRect clipping, required texture/font assets, minimum 48dp targets at `1616x720 @ 280dpi`, and absence of obsolete Backdrop/Masthead/Manifest visual objects after promotion.

- [ ] **Step 2: Add the PlayMode motion/interaction contract**

Construct the real prefab/scene and assert entrance settlement, bounded ambient displacement, exact-once events, disabled Continue, Settings/Credits/Privacy open, Close/Back behavior, Credits top reset and pause/resume without duplicate owners.

- [ ] **Step 3: Run the focused RED commands**

Run only `FrontendRedesignAssetTests` and `FrontendMotionTests` with Android active and project assembly filters. Expected: authentic failures for absent prefab/assets/motion types, with no unrelated compiler or harness failure.

### Task 2: Generate and import the animation-ready art kit

**Files:**
- Create: `Assets/_JustSomeStars/Art/UI/FrontendRedesign/Textures/*.png`
- Create: matching Unity `.meta` files through Unity import
- Create: `Assets/_JustSomeStars/Art/UI/FrontendRedesign/Fonts/`
- Create: font license/source/static TMP assets
- Create: `Assets/_JustSomeStars/Editor/UI/FrontendRedesignAssetImporter.cs`

**Interfaces:**
- Consumes: `main-landscape.png` and modal targets.
- Produces: persistent Unity sprites/TMP assets with deterministic importer settings and no chroma halo.

- [ ] **Step 1: Generate the opaque landscape plate**

Use ImageGen with the approved Main reference to remove UI and isolated foreground layers while preserving camera, palette, horizon and Signal location. Save at or above the target resolution.

- [ ] **Step 2: Generate foreground cutouts sequentially**

Generate ObservatoryForeground, Telescope, SignalTower, StarGlints and AmbientMotes individually on a flat unused chroma key. Remove the key with the supplied soft-matte/despill helper and validate alpha bounds and halos.

- [ ] **Step 3: Generate the UI surface kit**

Generate empty PrimaryControl, SecondaryControl and ModalFrame surfaces plus the three-glyph Signal strip from the approved references. No baked labels enter these textures.

- [ ] **Step 4: Resolve and license the title font**

Render candidate high-contrast OFL serif fonts against the title crop, choose the closest measured match, retain source/license, and generate a static TMP asset through Unity APIs. Keep Liberation Sans for UI/body text.

- [ ] **Step 5: Import deterministically**

The Editor importer applies Sprite/UI type, alpha transparency, appropriate max sizes, no mipmaps for UI, lossless/high-quality compression policy, nine-slice borders and stable pixels-per-unit. Reimport and assert settings persist.

- [ ] **Step 6: Composite-check the art kit**

Render the layers without UI at target dimensions and compare against the reference scene. Regenerate only the mismatching layer until no material seam, halo, crop or palette shift remains.

### Task 3: Build the redesigned prefab in isolation

**Files:**
- Create: `Assets/_JustSomeStars/Prefabs/UI/FrontendVisualRoot.prefab`
- Create: `Assets/_JustSomeStars/Runtime/UI/FrontendMotionDirector.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/FrontendButtonVisual.cs`
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendView.cs`
- Create: `Assets/_JustSomeStars/Editor/UI/FrontendRedesignBuilder.cs`

**Interfaces:**
- Produces: `FrontendMotionDirector.PlayEntrance()`, `ShowPanelAsync()`, `HidePanelAsync()` and a serialized `MotionScale` seam; `FrontendButtonVisual` owns pointer/focus/press visuals only.
- Preserves: `IFrontendView` and `IFrontendLifecycle` public contracts.

- [ ] **Step 1: Implement minimal motion owners to satisfy RED**

Use unscaled-time bounded interpolation. Motion components may change only serialized visual transforms, alpha and material parameters; they never emit product events or change controller state.

- [ ] **Step 2: Extend FrontendView without changing its public contract**

Route panel visibility through the motion director, retain exact listener lifecycle, set live text before reveal, rebuild Credits layout and reset its scroll to top. Hide completes before deactivating the panel.

- [ ] **Step 3: Build the prefab through Unity APIs**

Create the exact layer order, anchors, reference resolution, target geometry, live labels, real controls, modal viewport and persistent object references. Bind the canonical license TextAssets and Input System UI actions. Save/reload and validate every reference.

- [ ] **Step 4: Run focused prefab/motion GREEN**

Run only the two new focused contracts. Expected: all pass with zero compiler, unhandled, WeakPtr or leak diagnostics.

### Task 4: Promote the prefab into Frontend and perform design QA

**Files:**
- Modify: `Assets/_JustSomeStars/Scenes/Core/Frontend.unity`
- Modify: `ProjectSettings/ProjectSettings.asset`
- Modify: existing Frontend asset/integration tests as required by the approved landscape contract

**Interfaces:**
- Consumes: validated `FrontendVisualRoot.prefab`.
- Produces: Boot-to-Frontend using the new visual hierarchy with unchanged controller/report identity.

- [ ] **Step 1: Replace the old visual hierarchy transactionally**

Use the Builder/AssetDatabase to snapshot the current scene, install the prefab, lock both landscape orientations, save/reload and validate. On failure restore exact original scene/settings bytes and retain the repair tool.

- [ ] **Step 2: Capture all five settled states locally**

Capture Main, Settings, Credits top, Credits tail and Privacy at `1616x720`. Use the same viewport and no animation mid-frame.

- [ ] **Step 3: Run target-versus-capture design QA**

Create paired comparisons for every state. Fix typography, geometry, crop, material, color, contrast and spacing differences. Repeat focused tests and captures after each material correction; do not run full suites in the visual loop.

- [ ] **Step 4: Verify animation and interaction visually**

Record entrance, idle, button press, modal open/close, Credits scroll and Back. Confirm no stutter, flash, duplicate callback, layout movement or post-animation drift.

### Task 5: Final automated, APK and exact-device gates

**Files:**
- Modify: `docs/superpowers/specs/2026-08-22-frontend-award-ui-design.md` only if a user-approved target clarification is required
- Create ignored evidence under `Builds/`

**Interfaces:**
- Produces: final internal APK and target/capture evidence for critic and user review.

- [ ] **Step 1: Run complete project suites once**

Run full project-owned EditMode and PlayMode suites with Android active. Require zero failed/skipped/inconclusive tests and clean compiler/unhandled/WeakPtr scans.

- [ ] **Step 2: Build and inspect the internal APK**

Run `BuildCli.BuildAndroidInternal` with `JSS_BUILD_NUMBER` unset. Verify package, version, ARM64, debug signing, manifest initializer policy, OFL/Apache payloads, no EmojiOne payload and no staging/build-scene residue.

- [ ] **Step 3: Run one final device session**

Install the exact APK on one project-labelled Limrun emulator, record APK hash/device metrics, capture all five settled states, exercise every control, modal/root Back, Credits tail/reopen, and background/resume. Remove the instance and stop the tunnel after evidence capture.

- [ ] **Step 4: Run the harsh-critic fidelity gate**

Give the critic each immutable target beside its exact-device capture plus interaction evidence. Resolve every Critical/Important fidelity or behavior finding and repeat only the affected gate.

- [ ] **Step 5: Pause for user approval**

Present the final comparisons, APK hash, test totals and critic verdict. Do not start Codemagic remote execution, Google Play upload/closed testing or Galaxy Seller work until the user explicitly approves.
