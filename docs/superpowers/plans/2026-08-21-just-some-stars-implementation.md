# Just Some Stars Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, verify and publicly release the complete Android Chapter One of *Just Some Stars* with its approved visual quality, family experience, optional cloud account, birthday gifts and RevenueCat-powered cosmetics.

**Architecture:** A Unity 6 URP project uses isolated feature assemblies,
data-driven content and explicit runtime modes. Gameplay scenes use the URP 2D
Renderer, 2D physics, authored layered routes and deterministic frame-atlas
characters. All external capabilities sit behind C# service interfaces; Google
Play and Galaxy Store receive separate builds while sharing gameplay, content
and cloud identity. Unity CLI automation and CI produce deterministic Android
artifacts from the same repository.

**Tech Stack:** Unity 6 LTS, C#, URP 2D Renderer, Input System, Addressables,
SpriteRenderer/SpriteAtlas, 2D Physics, uGUI, TextMeshPro, UI Toolkit, Shader
Graph/HLSL, Python sprite tooling, optional preserved Blender/Python/Blender
MCP tooling, Firebase Authentication/Firestore/Cloud Functions, RevenueCat,
Kotlin/Java, Gradle, Codemagic and Android SDK tools.

**Spec:** `outputs/just-some-stars-technical-build-plan.md`

## Global Constraints

- Use `/mnt/unity-data/JustSomeStars` as the canonical Git worktree and project root. Keep every game file there, including the Unity project, `Library`, imported/generated assets, build outputs and caches; do not create a second active worktree on the system partition.
- The image at `outputs/just-some-stars-2.5d-gameplay-target-v1.png` is the
  binding gameplay visual quality floor. The earlier Mirra image remains
  historical palette/world/cast lineage only. Hackathon speed, mobile
  constraints and temporary prototypes never justify lowering the final bar.
- Code must remain production-grade and modular: small focused assemblies and components, explicit interfaces, data-driven content, dependency injection at composition roots, automated tests where practical, and no giant manager classes or hidden cross-system coupling.
- Execute strictly one numbered task at a time. Finish its checklist, run fresh verification, report the evidence and pause for approval before beginning the next task.
- Chapter One is a complete, free 45–60 minute story ending before dinner.
- Primary runtime is Android; Google Play and Galaxy Store builds are separate and Steam remains future work.
- Package IDs are `com.scientificaj.justsomestars` and `com.scientificaj.justsomestars.galaxy`.
- The Realme Narzo Performance profile must sustain a stable 30 FPS in representative play and thermal testing.
- Three Captain body families share gameplay capability and receive fitted
  sprite layers, collider, camera-framing and contact/cadence calibration.
- Only two crew companions plus Ori run full destination intelligence.
- Gameplay, learning, accessibility and story completion never require a purchase, login or network connection.
- No advertisements, subscriptions, premium currency, randomized loot, energy, public chat or paid power ship in Chapter One.
- Exact birthday is private, supports annual gifts and is excluded from advertising analytics.
- No hero character enters frame-atlas production before its approved reference sheet.
- Unity CLI and the deterministic sprite pipeline are required production
  interfaces. Blender MCP is preserved optional Task 11 tooling, not a
  shipping-character dependency.
- External SDK failures must degrade to local/offline play without blocking Chapter One.
- Every implementation task follows red-green-refactor where automated tests are practical and ends at a reviewer/commit gate.
- Never commit signing keys, passwords, store credentials, Firebase admin credentials or private API keys.

---

## Delivery map

| Work package | Tasks | Exit condition |
|---|---:|---|
| Release runway | 1–5 | Installable skeleton and release tooling are validated locally; cloud Unity execution and store submission are explicitly deferred until a playable release candidate exists |
| Runtime foundation | 6–9 | Modes, input, saves, services and scene streaming work offline |
| Art pipeline and Captain | 10–12 | Approved references produce a validated layered-2.5D proof and modular frame-atlas characters in Unity |
| Core play | 13–18 | 2D surface movement, crew, Lens, 2.5D flight and missions form one playable loop |
| Mirra benchmark | 19–20 | Mirra reaches mechanics-complete and visual-quality acceptance |
| Accounts and commerce | 21–24 | Google cloud, birthdays, RevenueCat and Galaxy adapters are verified |
| Full Chapter One | 25–26 | Koro/Vesper, Aster Veil, opening and ending complete the story |
| Product polish | 27–30 | Cosmetics, UI/accessibility, audio/cinematics and performance are release-ready |
| Growth and release | 31–33 | Services are truthfully activated, both store candidates verified and one public release is live |

## Dependency order

```mermaid
flowchart TD
    A["1–5 Release runway"] --> B["6–9 Runtime foundation"]
    B --> C["10–12 2.5D art pipeline and Captain"]
    C --> D["13–18 Core play"]
    D --> E
    B --> F["21–24 Accounts and commerce"]
    E --> G["25–26 Full Chapter One"]
    F --> H["27–30 Product polish"]
    G --> H
    H --> I["31–33 Growth and release"]
```

## Planned repository structure

```text
just-some-stars/
├── Assets/
│   ├── _JustSomeStars/
│   │   ├── Art/{2D,Characters,Ori,Ship,Environments,Materials,VFX,UI}/
│   │   ├── Audio/{Music,SFX,Voice}/
│   │   ├── Content/{Missions,Dialogue,Atlas,Cosmetics,Phenomena}/
│   │   ├── Prefabs/{Characters,Crew,Ship,Gameplay,UI}/
│   │   ├── Scenes/{Core,Destinations,Cinematics}/
│   │   ├── Runtime/
│   │   │   ├── Core/
│   │   │   ├── Input/
│   │   │   ├── Saving/
│   │   │   ├── Player/
│   │   │   ├── Animation2D/
│   │   │   ├── Rendering2D/
│   │   │   ├── Crew/
│   │   │   ├── Flight/
│   │   │   ├── Interaction/
│   │   │   ├── Discovery/
│   │   │   ├── Missions/
│   │   │   ├── Dialogue/
│   │   │   ├── Atlas/
│   │   │   ├── Cosmetics/
│   │   │   ├── Accounts/
│   │   │   ├── Commerce/
│   │   │   ├── Accessibility/
│   │   │   ├── UI/
│   │   │   └── Platform/
│   │   ├── Editor/{Build,Validation,Importers}/
│   │   └── Tests/{EditMode,PlayMode}/
│   └── Plugins/Android/
├── Packages/
├── ProjectSettings/
├── firebase/functions/
├── tools/{sprites,blender}/
├── docs/
├── outputs/
└── codemagic.yaml
```

## Locked cross-system interfaces

The following signatures are fixed before feature work begins. Supporting result records and enums live beside their owning interface so later tasks do not create competing service shapes.

```csharp
public interface IGameService
{
    ValueTask<StartupResult> InitializeAsync(CancellationToken cancellationToken);
    ValueTask ShutdownAsync();
}

public interface ISaveService : IGameService
{
    ValueTask<LoadSaveResult> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveCheckpointAsync(GameSave save, CancellationToken cancellationToken);
    ValueTask<LoadSaveResult> RecoverAsync(CancellationToken cancellationToken);
    GameSave Merge(GameSave local, GameSave cloud);
}

public interface IAccountService : IGameService
{
    AccountState Current { get; }
    ValueTask<AccountLinkResult> LinkGoogleAsync(CancellationToken cancellationToken);
    ValueTask SignOutAsync(CancellationToken cancellationToken);
    ValueTask DeleteAccountAsync(CancellationToken cancellationToken);
}

public interface ICloudSaveService : IGameService
{
    ValueTask<CloudSaveSnapshot?> DownloadAsync(string userId, CancellationToken cancellationToken);
    ValueTask UploadAsync(string userId, GameSave save, CancellationToken cancellationToken);
    ValueTask DeleteAsync(string userId, CancellationToken cancellationToken);
}

public interface IStoreService : IGameService
{
    StoreAvailability Availability { get; }
    ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(CancellationToken cancellationToken);
    ValueTask<PurchaseResult> PurchaseAsync(ContentId productId, CancellationToken cancellationToken);
    ValueTask<EntitlementSnapshot> RestoreAsync(CancellationToken cancellationToken);
    ValueTask<EntitlementSnapshot> RefreshEntitlementsAsync(CancellationToken cancellationToken);
}
```

`ContentId` is the only identifier passed between content systems. Gameplay listens to typed records such as `LandingCompleted`, `PhenomenonObserved`, `PredictionRecorded`, `InstrumentUsed`, `SignalFragmentRecovered` and `ConversationCompleted`; it never branches on ad-hoc string event names.

---

### Task 0: Prepare the development environment and agent services

**Status:** Executed on 2026-08-21. Pause after this task; Task 1 requires a separate user approval.

**Agent handoff:** The live, detailed source of truth is `/mnt/unity-data/JustSomeStars/docs/tooling/agent-environment.md`. The repository also has a root `AGENTS.md`; read both before installing tools or using ShipKit credits.

- [x] Clone the public greenfield repository, move its canonical work directory to `/mnt/unity-data/JustSomeStars`, and create branch `setup/task-0-environment`.
- [x] Verify Unity `6000.3.22f1`, Blender `5.2.0 LTS`, Git `2.53.0`, Android SDK/ADB, Node.js and pnpm.
- [x] Install Git LFS `3.7.1` locally, expose it on the user PATH and initialize it for the checkout.
- [x] Verify Blender MCP on `localhost:9876`, with Poly Haven enabled and telemetry disabled.
- [x] Authenticate Limrun CLI `0.28.6`, verify its API connection and 2,000 non-expiring credits, and install its official project-scoped Codex skills.
- [x] Install Argent CLI `0.21.0`, verify its 74 tools, declare its project MCP server and install its official agent skills.
- [x] Record every ShipKit/platform status and the correct future activation point in the agent environment manifest.
- [x] Install and verify Unity's bundled Android SDK tools 16.0, ADB 36.0.0, NDK r27c and OpenJDK 17.0.18.
- [x] Select Limrun cloud Android as the primary runtime instead of requiring the
  physical Realme Narzo; use Argent as the interaction/QA layer and create an
  emulator only for an immediate APK test.
- [x] Verify GitHub CLI authentication for `ScientificAJ` through a live `gh api user` request.
- [ ] Reopen the workspace before using the newly installed project-scoped Argent MCP.

Task 0 creates tooling/configuration only. It does not create the Unity project or begin gameplay implementation.

---

### Task 1: Clone the greenfield repository and preserve the approved documents

**Historical note:** this section records the original repository bootstrap.
The Mirra image copied here remains preserved palette/world/cast lineage; the
2026-08-25 Task 12 Stage 0 section owns the current 2.5D gameplay target.

**Files:**
- Create in repository: `outputs/astronomy-adventure-game-blueprint.md`
- Create in repository: `outputs/just-some-stars-technical-build-plan.md`
- Create in repository: `outputs/just-some-stars-mirra-gameplay-target-v1.png`
- Create in repository: `docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md`
- Modify: `.gitattributes`
- Modify: `.gitignore`
- Modify: `README.md`

**Interfaces:**
- Consumes: approved design and visual artifacts from the planning workspace.
- Produces: one real Git working tree whose documentation and image paths match this plan.

- [ ] **Step 1: Clone and enter the repository**

```bash
git clone https://github.com/ScientificAJ/just-some-stars.git
cd just-some-stars
```

Expected: `git remote -v` identifies `ScientificAJ/just-some-stars` and `git status --short` is empty.

- [ ] **Step 2: Copy the four approved planning artifacts into their exact repository paths**

```bash
mkdir -p outputs docs/superpowers/plans
cp /home/john/Documents/Codex/2026-08-20/y/outputs/astronomy-adventure-game-blueprint.md outputs/
cp /home/john/Documents/Codex/2026-08-20/y/outputs/just-some-stars-technical-build-plan.md outputs/
cp /home/john/Documents/Codex/2026-08-20/y/outputs/just-some-stars-mirra-gameplay-target-v1.png outputs/
cp /home/john/Documents/Codex/2026-08-20/y/docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md docs/superpowers/plans/
```

Then verify:

```bash
test -f outputs/astronomy-adventure-game-blueprint.md
test -f outputs/just-some-stars-technical-build-plan.md
test -f outputs/just-some-stars-mirra-gameplay-target-v1.png
test -f docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
```

Expected: all four commands exit `0`.

- [ ] **Step 3: Add Unity-friendly Git attributes and ignore rules**

Enable Git LFS and track source/binary art before adding those files:

```bash
git lfs install
git lfs track "*.blend" "*.fbx" "*.png" "*.psd" "*.wav" "*.mp4"
```

`.gitattributes` must also normalize ordinary text files to LF. `.gitignore` must exclude `Library/`, `Temp/`, `Logs/`, `Obj/`, `Builds/`, user settings and local secrets while retaining `ProjectSettings/`, `Packages/` and source art intended for version control.

- [ ] **Step 4: Update README with title, one-sentence pitch, visual target, Android stores and document links**

Expected: a new contributor reaches the blueprint, technical build plan and implementation plan directly from `README.md`.

- [ ] **Step 5: Review and commit the documentation baseline**

```bash
git diff --check
git status --short
git add .gitattributes .gitignore README.md outputs docs/superpowers/plans
git commit -m "docs: add approved Just Some Stars production plan"
```

### Task 2: Create the Unity 6 URP project and lock packages

**Files:**
- Create: `ProjectSettings/ProjectVersion.txt`
- Create: `ProjectSettings/ProjectSettings.asset`
- Create: `Packages/manifest.json`
- Create: `Packages/packages-lock.json`
- Create: `Assets/_JustSomeStars/Runtime/JustSomeStars.Runtime.asmdef`
- Create: `Assets/_JustSomeStars/Editor/JustSomeStars.Editor.asmdef`
- Create: `Assets/_JustSomeStars/Tests/EditMode/JustSomeStars.EditModeTests.asmdef`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/JustSomeStars.PlayModeTests.asmdef`

**Interfaces:**
- Consumes: Unity 6 LTS editor selected for production.
- Produces: compilable URP project with isolated runtime, editor and test assemblies.

- [ ] **Step 1: Create the project through Unity CLI**

```bash
export JSS_PROJECT_PATH="$PWD"
test -n "$JSS_UNITY_EDITOR"
test -x "$JSS_UNITY_EDITOR"
"$JSS_UNITY_EDITOR" -batchmode -quit -createProject "$JSS_PROJECT_PATH"
```

Expected: the shell already provides `JSS_UNITY_EDITOR` as the installed Unity 6 LTS editor binary, both `test` commands succeed, and `ProjectSettings/ProjectVersion.txt` records that editor revision.

- [ ] **Step 2: Install and resolve the required Unity packages**

`Packages/manifest.json` must include URP, Input System, Addressables, Cinemachine, AI Navigation, Localization and Test Framework packages resolved for the selected editor. Let Unity write `packages-lock.json`; do not hand-invent dependency hashes.

- [ ] **Step 3: Create focused assembly definitions**

Runtime must not reference Editor or test assemblies. Editor references Runtime. EditMode tests reference Runtime and Editor; PlayMode tests reference Runtime.

- [ ] **Step 4: Add a compilation smoke test**

Create `Assets/_JustSomeStars/Tests/EditMode/ProjectCompilationTests.cs`:

```csharp
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode;

public sealed class ProjectCompilationTests
{
    [Test]
    public void ProjectAssembly_IsLoadable() =>
        Assert.That(typeof(JustSomeStars.Runtime.ProjectMarker), Is.Not.Null);
}
```

Create `Assets/_JustSomeStars/Runtime/ProjectMarker.cs` with an empty sealed `ProjectMarker` class in `JustSomeStars.Runtime`.

- [ ] **Step 5: Run the EditMode suite**

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -runTests -testPlatform editmode -testResults Builds/TestResults/editmode.xml
```

Expected: exit `0` and one passing test.

- [ ] **Step 6: Commit the compilable Unity baseline**

```bash
git add Assets Packages ProjectSettings
git commit -m "build: create Unity 6 URP project"
```

### Task 3: Implement deterministic Unity CLI builds

**Files:**
- Create: `Assets/_JustSomeStars/Editor/Build/BuildTargetKind.cs`
- Create: `Assets/_JustSomeStars/Editor/Build/BuildCli.cs`
- Create: `Assets/_JustSomeStars/Editor/Build/BuildConfiguration.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/BuildConfigurationTests.cs`
- Create: `ProjectSettings/AndroidResolverDependencies.xml` only when generated by installed Android SDK resolvers

**Interfaces:**
- Produces: `BuildCli.BuildAndroidInternal()`, `BuildCli.BuildGooglePlayRelease()` and `BuildCli.BuildGalaxyRelease()`.
- Produces: scripting symbols `JSS_DEVELOPMENT`, `JSS_GOOGLE_PLAY` and `JSS_GALAXY` with mutual-exclusion validation.

- [ ] **Step 1: Write failing build-configuration tests**

```csharp
[TestCase(BuildTargetKind.GooglePlay, "com.scientificaj.justsomestars", "JSS_GOOGLE_PLAY")]
[TestCase(BuildTargetKind.Galaxy, "com.scientificaj.justsomestars.galaxy", "JSS_GALAXY")]
public void Resolve_ReturnsStoreSpecificIdentity(
    BuildTargetKind kind, string packageId, string requiredSymbol)
{
    var result = BuildConfiguration.Resolve(kind, buildNumber: 42);
    Assert.That(result.PackageId, Is.EqualTo(packageId));
    Assert.That(result.DefineSymbols, Does.Contain(requiredSymbol));
    Assert.That(result.VersionCode, Is.EqualTo(42));
}
```

- [ ] **Step 2: Run the targeted test and confirm failure**

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -runTests -testPlatform editmode \
  -testFilter JustSomeStars.Tests.EditMode.BuildConfigurationTests \
  -testResults Builds/TestResults/build-config-red.xml
```

Expected: failure because `BuildConfiguration` does not exist.

- [ ] **Step 3: Implement configuration and CLI entry points**

Each entry point must set package ID, symbols, version code, output name, development flags, Android target and keystore variables; build Addressables; call `BuildPipeline.BuildPlayer`; log the report; and throw on any non-success result so the process returns a failing exit code.

- [ ] **Step 4: Run tests and a Development APK build**

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -runTests -testPlatform editmode -testResults Builds/TestResults/build-config-green.xml
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildAndroidInternal
```

Expected: tests pass and `Builds/AndroidInternal/JustSomeStars-internal.apk` exists.

- [x] **Step 5: Commit**

```bash
git add Assets/_JustSomeStars/Editor Assets/_JustSomeStars/Tests ProjectSettings
git commit -m "build: add deterministic Android CLI targets"
```

### Task 4: Create the Boot scene and service registry

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Core/IGameService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/ServiceRegistry.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/GameBootstrap.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/StartupReport.cs`
- Create: `Assets/_JustSomeStars/Scenes/Core/Boot.unity`
- Create: `Assets/_JustSomeStars/Tests/EditMode/ServiceRegistryTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/BootSceneTests.cs`

**Interfaces:**
- Produces: `IGameService.InitializeAsync(CancellationToken)` and `IGameService.ShutdownAsync()`.
- Produces: `ServiceRegistry.Register<T>()`, `Get<T>()` and `TryGet<T>()`.
- Produces: startup results that distinguish required local services from optional network services.

- [ ] **Step 1: Write registry tests for unique registration and missing services**

```csharp
[Test]
public void RegisteringSameContractTwice_Throws()
{
    var registry = new ServiceRegistry();
    registry.Register<ITestService>(new TestService());
    Assert.Throws<InvalidOperationException>(() =>
        registry.Register<ITestService>(new TestService()));
}
```

- [ ] **Step 2: Implement the registry and bootstrap contracts**

Required startup order: settings, local save, input, content catalogue and mode controller. Optional services—cloud, commerce, notifications, attribution and growth—initialize afterward and report unavailable without aborting startup.

- [ ] **Step 3: Build `Boot.unity` with one persistent `GameBootstrap` root**

The root survives scene changes, prevents duplicate bootstraps and routes successful startup to `Frontend`.

- [ ] **Step 4: Run EditMode and PlayMode tests**

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -runTests -testPlatform editmode -testResults Builds/TestResults/boot-edit.xml
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -runTests -testPlatform playmode -testResults Builds/TestResults/boot-play.xml
```

Expected: required services initialize deterministically and an optional failure still reaches Frontend.

- [ ] **Step 5: Commit**

```bash
git add Assets/_JustSomeStars/Runtime/Core Assets/_JustSomeStars/Scenes/Core Assets/_JustSomeStars/Tests
git commit -m "feat: add resilient game bootstrap"
```

### Task 5: Put the first skeleton build into the store testing runway

**Files:**
- Create: `Assets/TextMesh Pro.meta`
- Create: `Assets/TextMesh Pro/` from the pinned official TMP Essential Resources package
- Delete after import: `Assets/TextMesh Pro/Resources/Sprite Assets/` once
  its sole generated Resources asset is removed and the folder is empty
- Delete after import: `Assets/TextMesh Pro/Resources/Sprite Assets.meta`
- Create: `Assets/_JustSomeStars/Scenes/Core/Frontend.unity`
- Create: `Assets/_JustSomeStars/Scenes/Core/Frontend.unity.meta`
- Create: `Assets/_JustSomeStars/Art.meta`
- Create: `Assets/_JustSomeStars/Art/UI.meta`
- Create: `Assets/_JustSomeStars/Art/UI/Generated.meta`
- Create: `Assets/_JustSomeStars/Art/UI/Generated/FrontendUIActions.asset`
- Create: `Assets/_JustSomeStars/Legal.meta`
- Create: `Assets/_JustSomeStars/Legal/Apache-2.0.txt`
- Create: `Assets/_JustSomeStars/Legal/Apache-2.0.txt.meta`
- Create: `Assets/Plugins.meta`
- Create: `Assets/Plugins/Android.meta`
- Create: `Assets/Plugins/Android/AndroidManifest.xml`
- Create: `Assets/Plugins/Android/AndroidManifest.xml.meta`
- Create: `Assets/_JustSomeStars/Runtime/Development.meta`
- Create: `Assets/_JustSomeStars/Runtime/UI.meta`
- Create: `Assets/_JustSomeStars/Runtime/UI/FrontendContracts.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/FrontendController.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/FrontendView.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/SafeAreaFitter.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/UnityFrontendLifecycle.cs`
- Create: `Assets/_JustSomeStars/Runtime/Development/DevelopmentBootstrapInstaller.cs`
- Create: `Assets/_JustSomeStars/Runtime/Development/DevelopmentRequiredServices.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/DevelopmentBootstrapInstallerTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/DevelopmentBootstrapInstallerTests.cs.meta`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/FrontendControllerTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/FrontendControllerTests.cs.meta`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/Task5LaunchIntegrationTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/Task5LaunchIntegrationTests.cs.meta`
- Create: `Assets/_JustSomeStars/Tests/EditMode/FrontendSceneAssetTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/FrontendSceneAssetTests.cs.meta`
- Create: `docs/release/google-play-closed-test.md`
- Create: `docs/release/galaxy-seller-setup.md`
- Create: `codemagic.yaml`
- Create: `docs/tooling/codemagic.md`
- Modify: `Assets/_JustSomeStars/Runtime/JustSomeStars.Runtime.asmdef`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/JustSomeStars.PlayModeTests.asmdef`
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Modify: `ProjectSettings/ProjectSettings.asset`
- Create: `ProjectSettings/URPProjectSettings.asset`

**Interfaces:**
- Consumes: Android Internal and Google Play CLI artifacts.
- Consumes: Task 4's `GameBootstrapComposition`, five required roles and
  `GameBootstrap.CompositionFactory` seam.
- Produces: a valid launchable build with title, local privacy panel, version and quit/background behavior.

- [ ] **Step 1: Test the development bootstrap composition before creating Frontend**

Write PlayMode tests first. They must prove that a before-Boot installer supplies
exactly Settings, LocalSave, Input, ContentCatalogue and ModeController; every
role uses a distinct service instance; Boot initializes each once, reaches the
literal `Frontend` destination and reverses cleanup exactly once. No missing-role
bypass is permitted.

- [ ] **Step 2: Install five truthful development services before Boot**

Use a `RuntimeInitializeLoadType.BeforeSceneLoad` installer to set Task 4's
composition factory before the Boot scene starts. Implement five distinct,
clearly development-only service types: `DevelopmentSettingsService`,
`DevelopmentLocalSaveService`, `DevelopmentInputService`,
`DevelopmentContentCatalogueService` and `DevelopmentModeControllerService`.
They represent only the launchable “Development Flight” skeleton and must not
claim persistence, gameplay or content that does not exist. Task 6 replaces the
Settings and Input registrations, Task 7 replaces LocalSave, and Task 8 replaces
ContentCatalogue and ModeController; remove each development service as its real
owner lands. `DevelopmentBootstrapInstaller` is the only composition-factory
writer through Tasks 6 and 7; those tasks modify that root rather than adding
competing runtime initializers. Task 8 renames it to the permanent
`ApplicationBootstrapInstaller` after the final development service is removed.
“Development-only” describes temporary capability, not `JSS_DEVELOPMENT`
conditional compilation: this truthful skeleton must also launch in the Task 5
Google and Galaxy test variants.

- [ ] **Step 3: Create a minimal but truthful Frontend**

Generate `Frontend.unity` through the Unity Editor API, add it as the second
enabled build scene after Boot and set the player product name to
`Just Some Stars`. It must show *Just Some Stars*, “Development Flight,”
`Version {Application.version}`, Settings, Credits, an in-app plain-language
Privacy panel and a visibly disabled Continue button with an unfinished-gameplay
explanation. Settings and Credits may open truthful local panels only. Do not
invent an external privacy URL or bypass the future grown-up gate, and do not
pretend unfinished gameplay exists. The root Android Back action exits normally;
background/resume returns to the same Frontend without reinitializing services.

Import the pinned official TMP Essential Resources through Unity's package
payload and retain its canonical Liberation Sans source TTF and OFL files. The
scene must serialize the exact OFL `TextAsset`; `Credits & Licenses` presents a
product-credit wrapper followed by the complete license verbatim in a clipped,
top-reset vertical ScrollRect. Because Task 5 uses no emoji sprites, set TMP's
default sprite to null, disable emoji support and delete only the generated
`Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset` so it is not pulled
into the player through `Resources`. Once that generated folder is exactly
empty, delete the `Sprite Assets` folder and its sibling folder `.meta` through
the Unity AssetDatabase so a clean checkout cannot contain an orphaned folder
meta. Retain the official source PNG, JSON and attribution outside `Resources`.
Asset tests must reject every empty Assets directory, missing folder meta and
orphaned meta; asset and artifact tests must prove the OFL is included/readable
and the unused EmojiOne player payload is absent. The Task-5-only
`FrontendUIActions.asset` is an explicit temporary input seam owned for
replacement/deletion by Task 6.

Import the canonical Apache License 2.0 text unchanged as
`Assets/_JustSomeStars/Legal/Apache-2.0.txt`; pin its 11,358-byte length and
SHA-256 `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30`.
The same clipped, top-reset `Credits & Licenses` flow must identify the shipped
AndroidX, Kotlin, Kotlin coroutines, JetBrains annotations and Guava components,
then present the complete Apache text verbatim after the complete OFL. Both
canonical license assets are immutable player dependencies and artifact checks
must find their exact bytes in the built APK.

Enable Unity's supported custom main manifest and retain the complete pinned
GameActivity template contract, including launcher/export/configuration,
rotation/resizing, safe-area/notch, freeform, layout and predictive-Back
metadata. Under AndroidX's existing `InitializationProvider`, remove only
`androidx.emoji2.text.EmojiCompatInitializer` through the manifest merger while
retaining exactly one `androidx.lifecycle.ProcessLifecycleInitializer`. Source,
merged-manifest and final-APK checks must reject the Emoji initializer without
removing the provider or lifecycle initializer.

- [ ] **Step 4: Build and validate on Limrun Android with Argent**

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -buildTarget Android -projectPath "$PWD" \
  -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildAndroidInternal
```

Read the project-scoped Limrun Android and relevant Argent interaction/QA skills
before using credits. Create or reuse a project-labelled Limrun Android emulator
only after the APK exists, install the exact internal artifact, and use Argent as
the primary discovery, interaction and evidence layer. Do not require the
physical Realme Narzo for this runway gate, and delete the Limrun instance after
the immediate test session so it does not consume idle credits.

Expected: Boot reaches Frontend, survives background/resume and logs no unhandled
exception; the run preserves launch and QA evidence from the Limrun-backed
Android target.

- [x] **Step 5: Prepare the Google Play release path and defer external submission**

The Google-specific build entry point, signing contract and closed-test runbook
are prepared, but do not create the Play application or upload a placeholder
skeleton. When a playable release candidate exists, Task 33 must build the
Google artifact separately with `BuildGooglePlayRelease`, a unique monotonic
`JSS_BUILD_NUMBER` and all four Google-specific signing variables. It must
validate the exact non-debug AAB at
`Builds/GooglePlay/JustSomeStars-google-play.aab`; the internal APK is never an
upload substitute. App creation, upload and tester coordination remain explicit
external-account gates owned by the Growth and release package.

For a qualifying new personal Play developer account, register at least 12
legitimate testers and record only aggregate/redacted evidence in
`docs/release/google-play-closed-test.md`. Tester-list membership or a local
timestamp does not start the clock: the closed release must be published, each
tester must opt in and the required count must remain continuously opted in for
14 days. Record whether this account is actually subject to that rule; never
commit tester email addresses.

- [x] **Step 6: Connect Codemagic and explicitly defer remote Unity execution**

Create a minimal `codemagic.yaml` that verifies a clean checkout and the locked
Unity/project inputs, runs project-owned EditMode tests and the targeted Task 5
PlayMode smoke, then invokes `BuildAndroidInternal` and inspects the exact APK.
Task 9's `ProjectContentValidator` does not exist yet and must not be faked here.
Store Unity Plus/Pro credentials and future signing material only in encrypted
Codemagic variable groups. Record the workflow, test reports and exact artifact
location in `docs/tooling/codemagic.md`.

The repository, application and workflow are connected and Codemagic exposes
500 free macOS minutes. The account has Unity Personal rather than the Plus/Pro
serial Codemagic requires for a cloud Unity build, so the user explicitly
approved deferring remote execution without spending minutes. If a suitable
license becomes available later, a clean runner with the exact Unity patch must
produce the same package ID, version, variant and artifact path as the local
CLI. Debug APK bytes and debug certificate identity need not be identical.

- [x] **Step 7: Prepare the Galaxy Seller path and defer the app record**

The Galaxy-specific package, signing decision points and Seller Portal runbook
are prepared. Creating the seller application before a playable release
candidate would produce a stale placeholder record, so Task 33 owns that
explicit external-account mutation. At that time record the redacted
seller/commercial status, application ID when safe, `.galaxy` package, Seller
Portal-managed AAB signing choice and missing commercial/IAP prerequisites in
`docs/release/galaxy-seller-setup.md`. Never invent a seller record or approval.

- [ ] **Step 8: Commit only documentation and code—not console secrets**

```bash
test ! -e 'Assets/TextMesh Pro/Resources/Sprite Assets' \
  && test ! -e 'Assets/TextMesh Pro/Resources/Sprite Assets.meta'
git add -A -- 'Assets/TextMesh Pro.meta' 'Assets/TextMesh Pro/' \
  Assets/Plugins.meta Assets/Plugins/Android.meta Assets/Plugins/Android \
  Assets/_JustSomeStars/Art.meta Assets/_JustSomeStars/Art/UI.meta \
  Assets/_JustSomeStars/Art/UI/Generated.meta \
  Assets/_JustSomeStars/Art/UI/Generated \
  Assets/_JustSomeStars/Legal.meta Assets/_JustSomeStars/Legal \
  Assets/_JustSomeStars/Runtime/UI.meta Assets/_JustSomeStars/Runtime/UI \
  Assets/_JustSomeStars/Runtime/Development.meta \
  Assets/_JustSomeStars/Runtime/Development \
  Assets/_JustSomeStars/Scenes/Core/Frontend.unity \
  Assets/_JustSomeStars/Scenes/Core/Frontend.unity.meta \
  Assets/_JustSomeStars/Runtime/JustSomeStars.Runtime.asmdef \
  Assets/_JustSomeStars/Tests/EditMode \
  Assets/_JustSomeStars/Tests/PlayMode \
  ProjectSettings docs/release docs/tooling/codemagic.md codemagic.yaml \
  docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
git diff --cached --check -- . \
  ':(exclude)Assets/TextMesh Pro/**' ':(exclude)**/*.meta' \
  ':(exclude)**/*.unity' ':(exclude)**/*.asset' \
  ':(exclude)ProjectSettings/*.asset'
git diff --cached --name-status
git status --short --untracked-files=all
git commit -m "release: start Android store testing runway"
```

The whitespace gate deliberately excludes Unity/package-generated serialized
assets, metas, scenes and ProjectSettings because their canonical serializers
emit whitespace that Git flags; do not hand-normalize those files. Their exact
content, GUID/meta pairing, persistence and asset-tree integrity remain covered
by the 211-test Unity suite and the staged-name/clean-checkout gates below.

Before commit, the staged-name audit must contain every new Unity asset/source
and its `.meta`, including all TMP files, the three Art parent metas, both
Runtime parent metas, the Legal and Plugins/Android parent metas, both license
and manifest file metas, `Frontend.unity.meta` and all four new test metas. The
staged TMP tree must omit the deleted empty `Sprite Assets` folder meta. The
status gate must show no unstaged or untracked Task 5 path and no generated
patcher/probe/build residue. Rerun the project-owned suites and require exactly
211 passing EditMode tests and 70 passing PlayMode tests, then validate every
asset/meta pair, the absence of empty asset directories and the absence of
orphaned metas again from a clean checkout before accepting CI evidence.

### Task 6: Implement settings, accessibility profiles and semantic input

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Accessibility/GameSettings.cs`
- Create: `Assets/_JustSomeStars/Runtime/Accessibility/SettingsService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Input/JssInputActions.inputactions`
- Create: `Assets/_JustSomeStars/Runtime/Input/InputRouter.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/FrontendDependencies.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/FrontendSettingsPanel.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/SettingsServiceTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/InputRouterTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/FrontendDependencyInjectionTests.cs`
- Modify: `Assets/_JustSomeStars/Runtime/AssemblyInfo.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Core/SceneTransition.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Core/ServiceStartupCoordinator.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Development/DevelopmentBootstrapInstaller.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Development/DevelopmentRequiredServices.cs`
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendContracts.cs`
- Modify: `Assets/_JustSomeStars/Runtime/UI/UnityFrontendLifecycle.cs`
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendController.cs`
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendView.cs`
- Modify: `Assets/_JustSomeStars/Editor/JustSomeStars.Editor.asmdef`
- Modify: `Assets/_JustSomeStars/Prefabs/UI/FrontendVisualRoot.prefab`
- Modify: `Assets/_JustSomeStars/Scenes/Core/Frontend.unity`
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Modify: `Assets/_JustSomeStars/Tests/EditMode/FrontendSceneAssetTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/DevelopmentBootstrapInstallerTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/FrontendControllerTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/FrontendMotionTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/Task5LaunchIntegrationTests.cs`
- Delete: `Assets/_JustSomeStars/Art/UI/Generated/FrontendUIActions.asset`
- Delete: `Assets/_JustSomeStars/Art/UI/Generated/FrontendUIActions.asset.meta`
- Delete after the asset is removed: `Assets/_JustSomeStars/Art/UI/Generated/`
- Delete after the asset is removed: `Assets/_JustSomeStars/Art/UI/Generated.meta`

**Interfaces:**
- Produces: `GameSettings` with independent `PilotingAssist`, `ExplorationAssist`, `ScienceDepth`, presentation, audio and control values.
- Produces: semantic actions `Move`, `Look`, `Primary`, `Secondary`, `Pause`, `Lens`, `PhotoMode`, `Recenter` and UI `Back`/`Cancel`.

- [x] **Step 1: Write serialization/default tests**

```csharp
[Test]
public void Defaults_AreBalancedAndAccessibleBeforeOpening()
{
    var settings = GameSettings.CreateDefaults();
    Assert.That(settings.PilotingAssist, Is.EqualTo(AssistLevel.Balanced));
    Assert.That(settings.ScienceDepth, Is.EqualTo(ScienceDepth.Balanced));
    Assert.That(settings.CaptionsEnabled, Is.True);
}
```

Freeze settings schema version `1` before production code. `GameSettings`
contains independent `PilotingAssist` and `ExplorationAssist`
(`Guided`/`Balanced`/`Ace`), `ScienceDepth`
(`Guided`/`Balanced`/`Deep`), captions, text scale, dyslexia-friendly type,
dialogue speed, color-vision mode, reduced camera shake, reduced flashing,
reduced motion, motion blur, particle density, presentation quality, separate
music/dialogue/effects volumes, haptics, left-handed controls and touch
sensitivity. Defaults are Balanced/Balanced/Balanced, captions on, text and
dialogue scale `1`, standard color vision, reductions off, motion blur off,
particle density `1`, Balanced quality, music `0.8`, dialogue `1`, effects
`0.9`, haptics on, right-handed layout and sensitivity `1`. Numeric ranges are
text `0.85..1.35`, dialogue `0.5..2`, particle/audio `0..1`, and sensitivity
`0.5..2`; invalid enums, non-finite values, unsupported schema or any
out-of-range persisted value invalidate the complete document and load safe
defaults rather than a partially trusted profile.

- [x] **Step 2: Implement settings with atomic local persistence**

Keep graphics/control device-local. Expose change events so UI, camera, input, subtitles and VFX update without scene reload.

The sole device-local document is `jss-settings-v1.json`: under
`Library/JustSomeStars/Local/` while running in the Editor and under
`Application.persistentDataPath` in a player. Missing data loads defaults
without claiming persistence. A successful change writes and flushes a
same-directory `.tmp`, atomically replaces the primary, swaps the in-memory
snapshot and then raises exactly one change event. Write failure preserves the
prior primary/current snapshot, emits no change event and removes only the
owned temporary file. `Current` returns an owned snapshot; caller mutation
cannot mutate service state. Applying an equal snapshot performs no write and
raises no event. Reopening a new service instance must load the exact last
successful snapshot.

- [x] **Step 3: Author Input System maps for UI, Surface, Flight and Lens**

Every runtime action is semantic; gameplay and UI code receive values from
`InputRouter` rather than reading touch coordinates or keys directly. The UI
map owns pointer/click plus one semantic Back/Cancel path. Replace
`UnityFrontendLifecycle`'s temporary private InputAction with an injected
InputRouter/JssInputActions subscription, migrate the Frontend EventSystem to
the canonical actions asset and delete Task 5's temporary
`FrontendUIActions.asset`. After migration, the Frontend must have exactly one
input authority and one Back/Cancel callback path.

Perform the temporary asset cleanup through the Unity AssetDatabase: delete
`FrontendUIActions.asset` so its asset `.meta` is removed with it, require the
single-purpose `Art/UI/Generated` folder to be exactly empty, then delete that
folder so its sibling `Generated.meta` is removed too. Rerun the asset-tree
integrity test after the migration so no empty directory or orphaned meta can
survive a clean checkout.

Use explicit root-owned push injection, not `ServiceRegistry`, a static service
locator or a second input authority. The development composition creates one
`SettingsService` and one `InputRouter` and passes the exact instances in a
typed `FrontendDependencies` payload owned by its `UnitySceneTransition`.
Frontend controller/lifecycle components remain non-interactive until the
transition has loaded the Frontend, pushed those dependencies through explicit
`Configure(...)` methods and enabled their subscriptions; only then may routing
report success. Scene reload pushes each current composition's instances once,
and unload/shutdown removes every binding before the owning services stop.

Replace the Task 5 Settings placeholder with a truthful local settings surface
implemented by `FrontendSettingsPanel`. It reads and writes the injected
`SettingsService`, persists device-local changes atomically, reflects external
setting changes without duplicate callbacks and removes the placeholder copy.
It must not imply account sync or cloud persistence.

Task 6 intentionally supersedes only the placeholder content inside the
approved Settings modal. The immutable Frontend background, title, main menu,
modal frame/material language, Close control, motion behavior, Credits,
Privacy and disabled Continue states remain binding. The real authority is
`FrontendVisualRoot.prefab`, so Task 6 modifies it and its motion contract as
well as the scene instance; it does not hand-edit prefab or scene YAML.

`JssInputActions.inputactions` is the one project-wide action asset and the
exact asset referenced by `InputSystemUIInputModule` and `InputRouter`. Its UI
map contains `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `RightClick`,
`MiddleClick`, `ScrollWheel`, `TrackedDevicePosition` and
`TrackedDeviceOrientation`. `Cancel` is the only product Back source and emits
`InputRouter.BackRequested`. Surface, Flight and Lens each contain the same
semantic `Move`, `Look`, `Primary`, `Secondary`, `Pause`, `Lens`, `PhotoMode`
and `Recenter` actions. UI remains enabled after service initialization; zero
or exactly one gameplay map may be enabled, and switching disables the prior
gameplay map before enabling the next. Keyboard/mouse and gamepad bindings are
authored now; UI pointer supports touch. Later gameplay HUDs bind touch through
these semantic actions rather than introducing another asset. Left-handed
settings swap the published movement/action screen sides without renaming or
rebinding semantic actions; Task 6 does not invent the later Surface/Flight HUD.

- [x] **Step 4: Test left-handed control swap and mode-map switching**

Expected: swapping layout changes screen placement, not semantic action names;
only the active gameplay map produces commands. Tests also prove root Back,
panel-close Back and disable/reenable behavior are delivered exactly once by
InputRouter, the EventSystem uses `JssInputActions`, and the temporary actions
asset and lifecycle-owned InputAction are absent. Controller/scene/real-launch
tests prove injection happens before the Frontend becomes interactive, uses the
exact composition-owned instances, survives reload with one subscription,
unbinds on teardown and leaves no static locator or second authority. Settings
tests exercise real controls, device-local persistence, re-open/reload state and
the absence of Task 5's placeholder wording.

Replace the development Settings and Input role registrations in the sole
composition installer with `SettingsService` and `InputRouter`; remove their two
development service types without adding another `BeforeSceneLoad` factory
writer. Evolve `DevelopmentBootstrapInstallerTests` to expect those two concrete
types while retaining exact roles/order/identity, cancellation and reverse
cleanup coverage. Its runtime-initializer audit must identify calls to the
`CompositionFactory` setter and assert exactly one `BeforeSceneLoad` writer;
unrelated `BeforeSceneLoad` callbacks are permitted.

- [ ] **Step 5: Commit**

```bash
test ! -e Assets/_JustSomeStars/Art/UI/Generated \
  && test ! -e Assets/_JustSomeStars/Art/UI/Generated.meta
git add -A Assets/_JustSomeStars/Editor/JustSomeStars.Editor.asmdef Assets/_JustSomeStars/Prefabs/UI/FrontendVisualRoot.prefab Assets/_JustSomeStars/Runtime/AssemblyInfo.cs Assets/_JustSomeStars/Runtime/Core Assets/_JustSomeStars/Runtime/Accessibility Assets/_JustSomeStars/Runtime/Input Assets/_JustSomeStars/Runtime/Development Assets/_JustSomeStars/Runtime/UI Assets/_JustSomeStars/Scenes/Core/Frontend.unity Assets/_JustSomeStars/Art/UI/Generated Assets/_JustSomeStars/Art/UI/Generated.meta Assets/_JustSomeStars/Tests ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: add semantic input and independent accessibility settings"
```

### Task 7: Implement versioned local saves and recovery

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Saving/GameSave.cs`
- Create: `Assets/_JustSomeStars/Runtime/Saving.meta`
- Create: `Assets/_JustSomeStars/Runtime/Saving/ISaveService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Saving/LocalSaveService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Saving/SaveMigrator.cs`
- Create: `Assets/_JustSomeStars/Runtime/Saving/SaveMerge.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/LocalSaveServiceTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/SaveMigratorTests.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Development/DevelopmentBootstrapInstaller.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Development/DevelopmentRequiredServices.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/DevelopmentBootstrapInstallerTests.cs`

**Interfaces:**
- Produces: `LoadAsync`, `SaveCheckpointAsync`, `RecoverAsync` and `Merge(GameSave local, GameSave cloud)`.
- Produces: schema version `1` containing story, Captain, discoveries, cosmetics, Atlas, birthday and metadata.
- Persists to the version-independent `jss-save.json` path with sibling
  `jss-save.json.tmp` and `jss-save.json.backup` files so a schema upgrade does
  not strand the previous save behind a versioned filename.
- `LoadSaveResult` distinguishes `Missing`, `LoadedPrimary`,
  `RecoveredBackup`, `Unreadable` and `StorageUnavailable`. Recovery and
  failure states carry concise player-readable copy. Missing or unreadable
  local data never makes required-service startup unavailable, and unreadable
  primary/backup bytes remain untouched until an explicit successful
  checkpoint replaces them.
- Version `1` is the first deployed format. The production migration registry
  therefore performs an identity pass for version `1`, rejects future or
  unsupported versions, and exposes ordered pure migration steps for fixture
  tests and later schemas; it must not invent a fake version `0` format.
- Merge policy is deterministic and preserves local-only photograph metadata:
  story keeps the higher checkpoint ordinal; equal ordinals with different
  checkpoint identities are a typed conflict; discovery, cosmetic and Atlas
  identifiers are sorted unions; Captain appearance keeps the greater explicit
  edit timestamp and equal-timestamp disagreement is a typed conflict; matching
  birthdays merge claim history while differing dates are a typed conflict.
  `SaveMergeConflictException` is the Task 21 seam for presenting a player
  choice instead of silently overwriting genuinely incompatible state.

- [x] **Step 1: Write tests for atomic write, backup recovery and deterministic merge**

```csharp
[Test]
public void Merge_UnionsDiscoveriesAndKeepsFurthestCheckpoint()
{
    var merged = SaveMerge.Combine(local, cloud);
    Assert.That(merged.Story.CheckpointOrdinal, Is.EqualTo(8));
    Assert.That(merged.DiscoveryIds, Is.EquivalentTo(new[] { "mirra.wind", "koro.geyser" }));
}
```

- [x] **Step 2: Implement JSON serialization through a replaceable serializer**

Write `save.tmp`, flush, move the current save to `save.backup`, then atomically replace it. Never overwrite the backup with unreadable data.

- [x] **Step 3: Implement corruption recovery and migration registry**

Malformed primary data loads the last-known-good backup and records a user-readable recovery result. Migrations are ordered, pure transformations and covered by fixture tests.

- [x] **Step 4: Run tests including simulated write interruption**

Expected: the last complete checkpoint always survives.

Replace the development LocalSave role registration in the sole composition
installer with `LocalSaveService` and remove the development LocalSave type.
Update `DevelopmentBootstrapInstallerTests` so the exact canonical composition,
cancellation and reverse-cleanup expectations use `LocalSaveService` while the
remaining development roles stay explicit.

- [x] **Step 5: Commit**

```bash
git add -A -- Assets/_JustSomeStars/Runtime/Saving.meta Assets/_JustSomeStars/Runtime/Saving Assets/_JustSomeStars/Runtime/Development Assets/_JustSomeStars/Tests docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
git commit -m "feat: add recoverable versioned save system"
```

### Task 8: Implement game modes and additive scene streaming

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Core/GameMode.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/GameModeController.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/SceneCatalog.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/SceneStreamService.cs`
- Create: `Assets/_JustSomeStars/Content.meta`
- Create: `Assets/_JustSomeStars/Content/SceneCatalog.asset`
- Create: `Assets/_JustSomeStars/Tests/EditMode/GameModeControllerTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/SceneStreamServiceTests.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/ApplicationBootstrapInstaller.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/ApplicationBootstrapInstaller.cs.meta`
- Rename: `Assets/_JustSomeStars/Tests/PlayMode/DevelopmentBootstrapInstallerTests.cs` to `Assets/_JustSomeStars/Tests/PlayMode/ApplicationBootstrapInstallerTests.cs`
- Rename: `Assets/_JustSomeStars/Tests/PlayMode/DevelopmentBootstrapInstallerTests.cs.meta` to `Assets/_JustSomeStars/Tests/PlayMode/ApplicationBootstrapInstallerTests.cs.meta`
- Rename: `Assets/_JustSomeStars/Tests/PlayMode/Task5LaunchIntegrationTests.cs` to `Assets/_JustSomeStars/Tests/PlayMode/ApplicationLaunchIntegrationTests.cs`
- Rename: `Assets/_JustSomeStars/Tests/PlayMode/Task5LaunchIntegrationTests.cs.meta` to `Assets/_JustSomeStars/Tests/PlayMode/ApplicationLaunchIntegrationTests.cs.meta`
- Delete: `Assets/_JustSomeStars/Runtime/Development/DevelopmentBootstrapInstaller.cs`
- Delete: `Assets/_JustSomeStars/Runtime/Development/DevelopmentBootstrapInstaller.cs.meta`
- Delete: `Assets/_JustSomeStars/Runtime/Development/DevelopmentRequiredServices.cs`
- Delete: `Assets/_JustSomeStars/Runtime/Development/DevelopmentRequiredServices.cs.meta`
- Delete: `Assets/_JustSomeStars/Runtime/Development.meta` after its final source is removed
- Modify: `Assets/_JustSomeStars/Runtime/JustSomeStars.Runtime.asmdef`
- Modify through Unity's Addressables Editor API: `Assets/AddressableAssetsData/AddressableAssetSettings.asset`
- Modify through Unity's Addressables Editor API: `Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset`

**Interfaces:**
- Produces: guarded transitions among Frontend, Customization, Clubhouse, Flight, Surface, Lens, Dialogue and Cinematic.
- Produces: `LoadDestinationAsync`, `UnloadDestinationAsync` and transition-progress events.

**Frozen Task 8 state contract:**

- `Frontend` is the initial base mode. The complete directed base-mode matrix is
  `Frontend -> Customization`; `Customization -> Frontend | Clubhouse`;
  `Clubhouse -> Customization | Flight`; `Flight -> Clubhouse | Surface`;
  `Surface -> Flight | Lens | Dialogue | Cinematic`; and
  `Lens | Dialogue | Cinematic -> Surface`. A same-mode request is an idempotent
  no-op. Every other edge is rejected before a runtime hook runs.
- Exactly one mode/overlay transition may be in flight. A concurrent or
  callback-reentrant request fails without changing state. A cancelled or
  failed runtime hook restores the exact prior mode, overlay, input map and
  camera-policy value before the operation settles.
- `Pause`, `PhotoMode` and `Settings` are overlays, not base modes. At most one
  overlay may be open; the same overlay is idempotent, replacement/nesting is
  rejected, base-mode transitions are blocked while an overlay is open, and
  close restores the exact underlying base mode. Settings is allowed in every
  base mode; Pause is allowed in Clubhouse, Flight, Surface and Lens; PhotoMode
  is allowed in Clubhouse, Flight and Surface.
- Input mapping is exact: Flight uses `GameplayInputMode.Flight`, Surface uses
  `GameplayInputMode.Surface`, Lens uses `GameplayInputMode.Lens`, every other
  base mode and every overlay uses `GameplayInputMode.None`. The InputRouter's
  UI map remains enabled. The mode runtime hook also receives an explicit
  camera-policy value so later camera implementations attach without becoming
  a second mode authority.

**Frozen Task 8 catalogue and streaming contract:**

- `SceneCatalog` is a version-1 `ScriptableObject`. Each entry has one trimmed,
  ordinal-stable destination ID, one trimmed Addressables scene address and one
  target `GameMode`. Empty IDs/addresses, duplicate IDs/addresses, duplicate
  mode/address records, invalid modes and unsupported schema versions reject
  the whole catalogue. The committed catalogue is acquired from the exact
  Addressables key `jss.scene-catalog`; the Default Local Group contains that
  exact asset entry. Destination entries remain empty until real scenes land,
  rather than shipping pretend gameplay.
- The current safe fallback is the existing `Frontend` scene and
  `GameMode.Frontend`. This is explicitly temporary until Task 26 creates the
  real Clubhouse scene; Task 26 must migrate the catalogue fallback to the ship
  hub. A failure records one structured diagnostic, settles and releases every
  issued operation, restores the prior active scene when possible, routes to
  the configured safe fallback and recovers the mode through the dedicated
  failure-recovery path. Cancellation performs cleanup but does not route to a
  fallback or report a content failure.
- Streaming has one owner and one in-flight operation. Concurrent load/unload
  calls fail closed. Loading an already-owned destination and unloading with no
  owned destination are idempotent results; another destination must be
  unloaded before a new one loads. Loads are additive and held before
  activation. Progress events are monotonic per operation and cover resolve,
  load, activation, mode commit and completion/cleanup.
- Cancellation before Addressables issue starts nothing. Cancellation after
  issue cannot pretend to stop Unity: the service awaits load/activation
  settlement, unloads any resulting scene, releases its handle exactly once,
  restores the prior active scene and then completes as cancelled. Load,
  activation or mode-hook failure follows the same exact cleanup before safe
  fallback. Shutdown cancels the current operation, awaits settlement, unloads
  an owned destination, releases catalog/scene handles once and is idempotent.
- `ApplicationBootstrapInstaller` constructs five distinct required services in
  exact order: `SettingsService`, `LocalSaveService`, `InputRouter`,
  `SceneStreamService`, `GameModeController`. The stream service receives the
  exact controller and scene transition instances by constructor; the
  controller receives the exact InputRouter through its runtime hook. Typed
  injection is composition-owned; `GameServiceRole` and `ServiceRegistry` are
  never used as locators.

- [x] **Step 1: Write illegal-transition and cancellation tests**

```csharp
[Test]
public void Surface_CannotJumpDirectlyToFrontendWithoutReturnFlow()
{
    var controller = GameModeController.CreateForTests(GameMode.Surface);
    Assert.That(controller.CanEnter(GameMode.Frontend), Is.False);
}
```

- [x] **Step 2: Implement the transition table and mode-owned input/camera hooks**

Pause, Photo Mode and settings are overlay states and must restore the underlying mode exactly.

- [x] **Step 3: Implement Addressables scene streaming with progress and cancellation**

Approach and landing masks can hold until destination scene activation succeeds. Failure returns safely to the ship hub and records a diagnostic.

- [x] **Step 4: Test repeated destination load/unload for leaked scenes and duplicate bootstrap objects**

Replace the final development ContentCatalogue and ModeController roles with
one explicit catalogue/streaming lifecycle owner and `GameModeController`.
Rename the sole composition root to `ApplicationBootstrapInstaller`, delete the
Development installer/services, and keep exactly one `BeforeSceneLoad` factory
writer. The permanent root owns typed dependency injection separately from
`GameServiceRole`, which remains lifecycle ordering rather than a service
locator.

Replace the development installer test rather than deleting its contract:
`ApplicationBootstrapInstallerTests` must lock the permanent exact service
types/order/identity, required-role no-bypass matrix, cancellation, fresh
composition instances, explicit-factory non-clobbering and reverse exact-once
cleanup. Its global initializer audit scans for `CompositionFactory` setter
calls and proves exactly one `BeforeSceneLoad` writer while allowing unrelated
callbacks. Migrate `Task5LaunchIntegrationTests` to
`ApplicationLaunchIntegrationTests`, use `ApplicationBootstrapInstaller`, and
preserve real Boot-to-Frontend activation, dependency injection, report
identity and leak-free teardown after the Development namespace is deleted.

- [x] **Step 5: Commit**

```bash
test ! -e Assets/_JustSomeStars/Runtime/Development \
  && test ! -e Assets/_JustSomeStars/Runtime/Development.meta
git add -A -- \
  "Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset" \
  Assets/_JustSomeStars/Content.meta Assets/_JustSomeStars/Content \
  Assets/_JustSomeStars/Runtime/Core \
  Assets/_JustSomeStars/Runtime/Development \
  Assets/_JustSomeStars/Runtime/Development.meta \
  Assets/_JustSomeStars/Runtime/JustSomeStars.Runtime.asmdef \
  Assets/_JustSomeStars/Tests \
  docs/issue-register.md \
  docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
git commit -m "feat: add explicit modes and additive destination streaming"
```

### Task 9: Create content IDs, typed events and editor validation

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Core/ContentId.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/ContentId.cs.meta`
- Create: `Assets/_JustSomeStars/Runtime/Core/GameEventBus.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/GameEventBus.cs.meta`
- Create: `Assets/_JustSomeStars/Runtime/Core/GameEvents.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/GameEvents.cs.meta`
- Create: `Assets/_JustSomeStars/Editor/Validation.meta`
- Create: `Assets/_JustSomeStars/Editor/Validation/ProjectContentValidator.cs`
- Create: `Assets/_JustSomeStars/Editor/Validation/ProjectContentValidator.cs.meta`
- Create: `Assets/_JustSomeStars/Editor/Validation/ValidationReport.cs`
- Create: `Assets/_JustSomeStars/Editor/Validation/ValidationReport.cs.meta`
- Create: `Assets/_JustSomeStars/Tests/EditMode/ContentValidationTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/ContentValidationTests.cs.meta`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/BootSceneTests.cs`

**Interfaces:**
- Produces: typed publish/subscribe without string event names.
- Produces: CLI-callable `ProjectContentValidator.ValidateForCi()` that exits nonzero on content errors.

- [x] **Step 1: Write tests for duplicate IDs, missing references and subscriber cleanup**

- [x] **Step 2: Implement immutable `ContentId` and typed event records**

```csharp
public readonly record struct ContentId(string Value);
public readonly record struct LandingCompleted(ContentId DestinationId);
public readonly record struct PhenomenonObserved(ContentId PhenomenonId);
public readonly record struct SignalFragmentRecovered(ContentId FragmentId);
```

The project is pinned to C# 9, so use semantically equivalent immutable
`readonly struct` value types with ordinal equality and validated constructors;
do not raise the language version solely to use C# 10 `record struct` syntax.

- [x] **Step 3: Implement project validators**

Validate duplicate IDs, mission links, dialogue references, science sources, Addressable keys, body-family cosmetic fits and store entitlement mappings.

- [x] **Step 4: Run validation through Unity CLI and confirm an intentionally broken fixture fails**

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -buildTarget Android \
  -projectPath "$PWD" \
  -executeMethod JustSomeStars.Editor.Validation.ProjectContentValidator.ValidateForCi
```

- [x] **Step 5: Remove the broken fixture, rerun green and commit**

```bash
git add -A -- \
  Assets/_JustSomeStars/Runtime/Core \
  Assets/_JustSomeStars/Editor/Validation.meta \
  Assets/_JustSomeStars/Editor/Validation \
  Assets/_JustSomeStars/Tests/EditMode \
  Assets/_JustSomeStars/Tests/PlayMode/BootSceneTests.cs \
  docs/issue-register.md \
  docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
git commit -m "feat: add typed events and content validation"
```

### Runtime foundation QA remediation: deterministic PlayMode and device inspection

This user-promoted remediation closes JSS-011 and JSS-012 after Task 9 without
starting Task 10.

**Files:**
- Modify: `.gitignore`
- Create: `tools/__init__.py`
- Create: `tools/qa/__init__.py`
- Create: `tools/qa/playmode-fixtures.txt`
- Create: `tools/qa/playmode_suite.py`
- Create: `tools/qa/device_inspector_session.py`
- Create: `tools/qa/README.md`
- Create: `tools/qa/tests/__init__.py`
- Create: `tools/qa/tests/test_playmode_suite.py`
- Create: `tools/qa/tests/test_device_inspector_session.py`
- Modify: `docs/tooling/unity-builds.md`
- Modify: `docs/tooling/agent-environment.md`
- Modify: `outputs/just-some-stars-technical-build-plan.md`
- Modify: `docs/issue-register.md`

**Interfaces:**
- Produces: exact source/manifest agreement and one fail-closed Unity process
  per PlayMode fixture, with strict NUnit validation and an atomic aggregate.
- Produces: an OS-locked inspector lease that prevents Argent and Limrun
  UIAutomator from owning the same paid emulator session concurrently.

- [x] **Step 1: Reproduce both missing orchestration contracts with dependency-free tests**

- [x] **Step 2: Implement isolated-fixture PlayMode orchestration and strict report validation**

- [x] **Step 3: Implement atomic, identity-bound single-inspector ownership**

- [x] **Step 4: Run tool tests and the complete real PlayMode manifest once**

- [x] **Step 5: Complete one bounded harsh-critic review and resolve only in-scope blockers**

- [x] **Step 6: Mark JSS-011/JSS-012 resolved, commit, and stop before Task 10**

```bash
git add -A -- \
  .gitignore \
  tools/qa \
  tools/__init__.py \
  docs/tooling/unity-builds.md \
  docs/tooling/agent-environment.md \
  docs/issue-register.md \
  docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md \
  outputs/just-some-stars-technical-build-plan.md
git commit -m "test: harden runtime foundation QA orchestration"
```

### Task 10: Produce and approve the character reference-sheet set

**Files:**
- Create: `Assets/_JustSomeStars/Art/Characters/References/master-style-sheet.png`
- Create: `Assets/_JustSomeStars/Art/Characters/References/crew-height-lineup.png`
- Create: `Assets/_JustSomeStars/Art/Characters/References/captain-body-families.png`
- Create: `Assets/_JustSomeStars/Art/Characters/References/captain-customization.png`
- Create: `Assets/_JustSomeStars/Art/Characters/References/{mira,juno,kai,bea,ori}.png`
- Create: `Assets/_JustSomeStars/Art/Characters/References/expressions.png`
- Create: `Assets/_JustSomeStars/Art/Characters/References/equipment.png`
- Create: `Assets/_JustSomeStars/Art/Characters/References/material-callouts.png`
- Create: `docs/art/character-reference-approval.md`

**Interfaces:**
- Produces: approved orthographic, scale-consistent identity, silhouette,
  equipment, expression, pivot/contact and material authority for 2.5D sprite
  production. These sheets may also support optional Blender reference work.

**Locked sheet ownership:**

- `master-style-sheet.png` is the visual rulebook for the **entire cast**, not
  one character. It must show a representative Captain, Mira, Juno, Kai, Bea
  and Ori examples; shared face, eye, child-proportion, hand, foot, hair,
  homemade-suit, Signal-glow, rendering, lighting, detail-density and
  mobile-silhouette rules. It establishes the common style but does not replace
  any character's final turnaround.
- `crew-height-lineup.png` shows the Captain, Mira, Juno, Kai, Bea and Ori on
  one floor line in neutral front poses, with numerical metre heights,
  head-height guides and body-width/silhouette comparison.
- `captain-body-families.png` covers only the Captain's three equal-capability
  body families. Each family requires neutral front, side and back views,
  exact proportions and matching shoulder, waist, hand, foot, clothing-fit,
  skeleton and collider landmarks.
- `captain-customization.png` covers only the Captain's interchangeable face,
  skin, eyes, hair, colors, clothing, patches, gloves, boots, helmets,
  backpacks and small accessories. It must demonstrate that shared clothing
  fits all three body families.
- `mira.png`, `juno.png`, `kai.png` and `bea.png` each cover only that named,
  bespoke crew member. Every sheet requires a neutral front/side/back
  turnaround, face close-up, hair construction, clothing layers, personal
  palette, signature equipment, exact height, matching landmarks and a
  mobile-size silhouette preview.
- `ori.png` covers only Ori. It requires front, side and back views plus a top
  view when needed, exact size beside a child silhouette, eye/display,
  antenna, movement joints, scanner, opening panels, Signal-reactive parts,
  lights, material separation and mechanical articulation for its dedicated
  frame-atlas contract.
- `expressions.png` is a cast-wide grid with separate rows for the Captain,
  Mira, Juno, Kai, Bea and Ori. Human rows cover neutral, happy, curious,
  worried, afraid, surprised, determined, sad, blink and compact speaking-mouth
  shapes. Ori's row communicates the same usable emotional range through its
  eye, light, antenna, head angle and body pose.
- `equipment.png` is the character-equipment sheet for the Captain's standard
  exploration kit, Mira's observation tools, Juno's repair tools, Kai's
  navigation/piloting gear, Bea's camera/Atlas gear, Ori's scanner/Signal
  components and shared helmets, backpacks, gloves, boots and handheld tools.
  It records scale, attachment position and owner; it does not cover ship or
  environment props.
- `material-callouts.png` is the cast-wide material/color rulebook for skin,
  hair, suit fabric, stitched sections, rubber, painted plastic, scratched
  metal, visor glass, patches, Ori's shell, screens, lights, Signal energy and
  each character's palette. It identifies soft, rough, glossy, metallic,
  transparent and emissive surfaces.

- [x] **Step 1: Generate the master style sheet and compare it with the approved quality image**

Acceptance: warm storybook faces, practical homemade suits, Signal accents, readable mobile silhouettes and no copied franchise design language.

User-approved art-direction lock (2026-08-23): cinematic storybook realism for
the five human characters, who read as 12–14 years old at approximately 6–6.5
heads tall, with soft believable
facial anatomy, slightly enlarged expressive eyes, modestly enlarged hands and
boots, premium believable materials, practical repaired homemade exploration
gear, restrained cyan/violet Signal accents and the warm-sunset/cool-starlight
duality of `outputs/just-some-stars-2.5d-gameplay-target-v1.png`. Avoid anime,
chibi, photoreal-adult, toy-plastic, tactical-military, superhero, generic
mobile-mascot and recognizable franchise design language. The final master
sheet shows the representative Captain, Mira, Juno, Kai, Bea and Ori together;
the user's approval is recorded in the Task 10 ledger.

- [x] **Step 2: Create the crew lineup and lock heights/body silhouettes**

Show all five kids and Ori on one ground plane with numerical metre-height callouts.

- [x] **Step 3: Create Captain and individual-character orthographic sheets**

Each sheet contains neutral front, side and back views with matching clothing landmarks and no perspective distortion.

- [x] **Step 4: Create expression, equipment and material sheets**

- [x] **Step 5: Record explicit approval for every sheet in `character-reference-approval.md`**

The approval file contains one row for all 12 images, recording filename,
characters covered, required views, scale consistency, visual-style
consistency, mobile readability, equipment consistency, approval status,
review notes and approval date. No downstream character-production task starts
for an unapproved row, and approval of the master sheet never substitutes for
approval of an individual character sheet.

Written labels, role descriptions, numerical dimensions, landmark diagrams and
control notes override incidental generated-image mistakes. Task 12 sprite
artists and Task 29 animators must correct those mismatches in the actual
atlases instead of tracing them; expression labels describe the required final
emotion even when a thumbnail is imperfect.

- [x] **Step 6: Commit source and approved exports**

```bash
git add Assets/_JustSomeStars/Art/Characters/References docs/art
git commit -m "art: lock character reference sheets"
```

### Task 11: Build the Blender MCP source and export pipeline

**2026-08-25 status:** completed and preserved as optional tooling. The 2.5D
pivot does not invalidate this verified pipeline, but Task 11 is no longer a
dependency for shipping characters or replacement Task 12.

**Files:**
- Create: `tools/blender/jss_scene_setup.py`
- Create: `tools/blender/validate_character.py`
- Create: `tools/blender/export_unity_fbx.py`
- Create: `tools/blender/generate_lods.py`
- Create: `tools/blender/README.md`
- Create: `tools/blender/tests/test_character_pipeline.py`
- Create: `Assets/_JustSomeStars/Editor/Importers/CharacterModelPostprocessor.cs`
- Create: `Assets/_JustSomeStars/Editor/Importers/CharacterModelPostprocessor.cs.meta`
- Create: `Assets/_JustSomeStars/Tests/EditMode/CharacterImportPolicyTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/CharacterImportPolicyTests.cs.meta`
- Create: `Assets/_JustSomeStars/Art/Characters/Source/Fixtures/task11-primitive.blend`
- Create: `Assets/_JustSomeStars/Art/Characters/Export/Fixtures/task11-primitive.fbx`
- Create: `Assets/_JustSomeStars/Art/Characters/Export/Fixtures/task11-primitive.jss-character.json`
- Create through Unity: the matching `Source`, `Export`, `Fixtures` and asset `.meta` files.

**Interfaces:**
- Produces: deterministic meter scale, `-Z` forward/`Y` up FBX exports, naming policy, skeleton checks and LOD outputs.

- [x] **Step 1: Connect Blender MCP on port 9876 with only Poly Haven enabled**

Expected: MCP can inspect the default scene and execute a harmless object-list operation.

- [x] **Step 2: Implement scene setup and validator scripts**

Validator must reject unapplied scale, non-manifold hero meshes, unnamed materials, unexpected bones, missing LOD collections and invalid object prefixes.

- [x] **Step 3: Implement batch FBX export**

```bash
blender -b Assets/_JustSomeStars/Art/Characters/Source/captain.blend \
  --python tools/blender/export_unity_fbx.py
```

Expected: clean FBX appears under `Assets/_JustSomeStars/Art/Characters/Export/` with a JSON validation report.

- [x] **Step 4: Implement Unity model postprocessing**

Set Humanoid where declared, import scale 1, material extraction policy, animation compression and LOD naming consistently.

- [x] **Step 5: Test one primitive rig round-trip before hero production**

Expected: Blender dimensions, Unity dimensions, forward direction, root motion and bone names match the report.

- [x] **Step 6: Commit scripts and importer**

```bash
git add -A -- \
  tools/blender \
  Assets/_JustSomeStars/Editor/Importers.meta \
  Assets/_JustSomeStars/Editor/Importers \
  Assets/_JustSomeStars/Art/Characters/Source.meta \
  Assets/_JustSomeStars/Art/Characters/Source \
  Assets/_JustSomeStars/Art/Characters/Export.meta \
  Assets/_JustSomeStars/Art/Characters/Export \
  Assets/_JustSomeStars/Tests/EditMode/CharacterImportPolicyTests.cs \
  Assets/_JustSomeStars/Tests/EditMode/CharacterImportPolicyTests.cs.meta \
  docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md \
  docs/issue-register.md
git diff --cached --check
git lfs ls-files --name-only | grep -Fx \
  'Assets/_JustSomeStars/Art/Characters/Source/Fixtures/task11-primitive.blend'
git lfs ls-files --name-only | grep -Fx \
  'Assets/_JustSomeStars/Art/Characters/Export/Fixtures/task11-primitive.fbx'
git commit -m "build: add Blender MCP asset validation pipeline"
```

### Task 12: Replace the unfinished 3D character path with the approved 2.5D production foundation

**Status:** the prior 3D Stages 1–3 are historical; prior Stage 4 rig/walk
work is stopped. Replacement Stages 0–5 passed and the user approved the final
Mirra result on 2026-08-28. No unfinished 3D file is deleted without separate
user approval.

**Authority:**
- Design: `docs/superpowers/specs/2026-08-25-2.5d-gameplay-pivot-design.md`
- Execution: `docs/superpowers/plans/2026-08-25-2.5d-gameplay-pivot.md`
- Visual target: `outputs/just-some-stars-2.5d-gameplay-target-v1.png`

**Interfaces:**
- Produces: a layered 2.5D Mirra runtime proof, deterministic coherent-strip
  sprite pipeline, three equal-capability Captain sprite families, bespoke
  crew/Ori atlases and one final-art Android acceptance route.
- Produces: `SurfaceMotor2D`, `CompositionCamera2D`,
  `SpriteAnimationClipDefinition`, `CharacterSpriteSet`,
  `SpriteAtlasAnimator` and the bounded `LayeredCharacterRenderer` contract.

- [x] **Stage 0: Lock the pivot, target and historical boundary**

- [x] **Stage 1: Prove layered 2.5D rendering, movement and composition camera with temporary art**

- [x] **Stage 2: Build and validate the deterministic coherent-strip/atlas pipeline**

- [x] **Stage 3: Produce the three modular Captain sprite families**

- [x] **Stage 4: Produce bespoke Mira, Juno, Kai, Bea and Ori sprite sets**

- [x] **Stage 5: Integrate the final-art Mirra excerpt and pass one package-boundary Android gate**

Task 13 begins only after replacement Stage 5 passes. The exact files, tests,
commands, visual checks and commit boundaries live in the execution plan above.

### Task 13: Implement the production surface motor and composition camera

**Files:**
- Modify: `Assets/_JustSomeStars/Runtime/Player/SurfaceMotor2D.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Player/SurfaceMotor2DConfig.cs`
- Create: `Assets/_JustSomeStars/Runtime/Player/BodySpriteCalibration.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Player/CompositionCamera2D.cs`
- Create: `Assets/_JustSomeStars/Prefabs/Characters/Captain2D.prefab`
- Modify: `Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity`
- Create: `Assets/_JustSomeStars/Tests/EditMode/Captain2DPrefabProductionTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/SurfaceMotor2DProductionTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/CompositionCamera2DProductionTests.cs`

**Interfaces:**
- Consumes: `InputRouter`, active `BodySpriteCalibration`, `GameSettings` and
  the Task 12 layered-scene contract.
- Produces: deterministic 2D move/jump/jet state and authored camera profiles.

- [x] **Step 1: Write focused tests for slopes, steps, wind, low gravity, moving platforms, the bounded recovery boundary, the production prefab and authored camera profiles**

The EditMode asset contract must load the real prefab and benchmark scene. The
benchmark scene must use an instance of the exact production prefab rather than
an inline lookalike. PlayMode tests exercise the real runtime components and
fixtures; missing or malformed assets fail closed.

- [x] **Step 2: Harden the fixed-step `Rigidbody2D` motor**

Expose persistent external acceleration for wind and derive moving-surface
velocity from the contacted `Rigidbody2D`. Keep speed, jump, jet and animation
cadence equal across body families. Calibration changes only visual scale and
pivot, collider/contact/shadow fit and a presentation-only camera framing
anchor. Family switching is atomic and the character root remains scale one.

- [x] **Step 3: Harden camera dead zone, look-ahead, bounds, zoom, composition targets and reduced motion**

There is no free orbit. Camera behavior must preserve the approved side-view
route and never reveal unowned layer edges. The production component owns a
validated serialized profile for every `GameCameraPolicy`. Each profile defines
dead zone, look-ahead, smoothing, zoom, primary/optional composition targets,
center-movement rails and separate content-safe bounds that account for the
orthographic viewport and zoom. Reduced motion removes velocity-driven motion
without disabling deterministic tracking.

- [x] **Step 4: Run the same traversal fixture with every body calibration**

Expected: all families complete the route within the same tolerance without
camera clipping, visible baseline drift or collision mismatch.

- [x] **Step 5: Perform the user-approved touch-control device test with visible input proof and commit**

The continuous capture must visibly demonstrate the same installed build
responding to run, jump, contextual Interact and Discovery Lens input; it must
also show slopes/steps, a moving platform or wind segment, the bounded fall
recovery, camera limits and reduced-motion behavior. A static stability video,
an input-injection log or isolated screenshots cannot substitute for this
interaction proof. Preserve the exact APK hash, device metrics and filtered
runtime log beside the recording.

```bash
git add -A -- Assets/_JustSomeStars/Runtime/Player \
  Assets/_JustSomeStars/Prefabs/Characters Assets/_JustSomeStars/Tests
git diff --cached --check
git commit -m "feat: productionize 2.5d surface movement and camera"
```

### Task 14: Implement contextual interactions and anchor reservations

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Interaction/InteractionDefinition.cs`
- Create: `Assets/_JustSomeStars/Runtime/Interaction/InteractionAnchor2D.cs`
- Create: `Assets/_JustSomeStars/Runtime/Interaction/InteractionReservationService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Interaction/InteractionRunner.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/InteractionReservationTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/InteractionRunnerTests.cs`

**Interfaces:**
- Produces: one interaction definition with player/crew/Ori 2D anchors, tool,
  frame-atlas clip, typed events and recovery.

- [x] **Step 1: Write tests proving two actors cannot reserve one exclusive anchor**

- [x] **Step 2: Implement reservation leases with cancellation and timeout recovery**

- [x] **Step 3: Implement contextual selection based on 2D distance, facing, layer/depth band, mode and required tool**

- [x] **Step 4: Build a probe-repair fixture involving Captain, Juno and Ori**

Expected: all three reach distinct anchors, play their authored actions and release reservations even after cancellation.

- [x] **Step 5: Commit**

```bash
git add Assets/_JustSomeStars/Runtime/Interaction Assets/_JustSomeStars/Tests
git commit -m "feat: add contextual interaction anchors"
```

### Task 15: Implement Crew Director and personality brains

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Crew/CrewDirector.cs`
- Create: `Assets/_JustSomeStars/Runtime/Crew/CrewBrain.cs`
- Create: `Assets/_JustSomeStars/Runtime/Crew/CrewPersonality.cs`
- Create: `Assets/_JustSomeStars/Runtime/Crew/CrewActionState.cs`
- Create: `Assets/_JustSomeStars/Runtime/Crew/CrewPerception.cs`
- Create: `Assets/_JustSomeStars/Runtime/Crew/CrewRecovery.cs`
- Create: `Assets/_JustSomeStars/Runtime/Crew/TraversalGraph2D.cs`
- Create: `Assets/_JustSomeStars/Runtime/Crew/DialogueToken.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/CrewUtilityTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/CrewRecoveryTests.cs`

**Interfaces:**
- Consumes: tagged perceptions, interaction reservations, story priority and camera visibility.
- Produces: selected action state and one dialogue-token owner.

- [x] **Step 1: Write utility-order tests**

```csharp
[Test]
public void MandatoryStoryAction_OutranksPersonalityObservation()
{
    var choice = CrewUtility.Select(new[] { personalityNotice, mandatoryRepair });
    Assert.That(choice.Id, Is.EqualTo(mandatoryRepair.Id));
}
```

- [x] **Step 2: Implement Director companion selection, formation and dialogue arbitration**

- [x] **Step 3: Implement states: join, follow, position, traverse, investigate, interact, react, speak, conversation, cinematic, wait and recover**

Traverse authored `TraversalGraph2D` nodes and declared depth-band transitions;
do not introduce a 3D NavMesh dependency.

- [x] **Step 4: Create Mira, Juno, Kai, Bea and Ori personality assets with their approved attention weights**

- [x] **Step 5: Test off-camera warp recovery, blocked navigation and dialogue contention**

Expected: no visible teleport, no permanent stuck state and no overlapping authored lines.

- [x] **Step 6: Profile decision ticks with two companions plus Ori and commit**

```bash
git add Assets/_JustSomeStars/Runtime/Crew Assets/_JustSomeStars/Content Assets/_JustSomeStars/Tests
git commit -m "feat: add authored crew intelligence"
```

### Task 16: Implement Discovery Lens and evidence gameplay

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Discovery/LensMode.cs`
- Create: `Assets/_JustSomeStars/Runtime/Discovery/InstrumentDefinition.cs`
- Create: `Assets/_JustSomeStars/Runtime/Discovery/PhenomenonDefinition.cs`
- Create: `Assets/_JustSomeStars/Runtime/Discovery/Prediction.cs`
- Create: `Assets/_JustSomeStars/Runtime/Discovery/EvidenceRecorder.cs`
- Create: `Assets/_JustSomeStars/Runtime/Discovery/DiscoveryLensController.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/EvidenceRecorderTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/DiscoveryLensTests.cs`

**Interfaces:**
- Produces: imaging, spectrum, temperature, atmosphere, motion and Signal modes.
- Publishes: `PhenomenonObserved`, `PredictionRecorded` and `InstrumentUsed` events.

- [x] **Step 1: Write tests proving incorrect predictions still record evidence and never block mission continuation**

- [x] **Step 2: Implement instrument/phenomenon compatibility and evidence records**

- [x] **Step 3: Implement layered Lens focus, aiming, reticle, scan progress and mode switching**

Lens focus resolves declared phenomena and depth bands inside the composition;
it does not switch into a free first-person or orbiting 3D camera.

- [x] **Step 4: Create three fixtures: Mirra temperature, Koro spectrum and Aster motion**

- [x] **Step 5: Verify Guided hints and Deep science detail do not alter scientific outcomes**

- [x] **Step 6: Commit**

```bash
git add Assets/_JustSomeStars/Runtime/Discovery Assets/_JustSomeStars/Content/Phenomena Assets/_JustSomeStars/Tests
git commit -m "feat: add evidence-driven Discovery Lens"
```

### Task 17: Implement 2.5D flight, assists, landing and recovery

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Flight/FlightMotor2D.cs`
- Create: `Assets/_JustSomeStars/Runtime/Flight/FlightModel2D.cs`
- Create: `Assets/_JustSomeStars/Runtime/Flight/FlightAssist.cs`
- Create: `Assets/_JustSomeStars/Runtime/Flight/GravityAssistVolume2D.cs`
- Create: `Assets/_JustSomeStars/Runtime/Flight/FlightDepthLane.cs`
- Create: `Assets/_JustSomeStars/Runtime/Flight/FlightCheckpoint.cs`
- Create: `Assets/_JustSomeStars/Runtime/Flight/LandingSequence.cs`
- Create: `Assets/_JustSomeStars/Art/2D/Ship/PlayerShip/`
- Create: `Assets/_JustSomeStars/Prefabs/Ship/PlayerShip2D.prefab`
- Create: `Assets/_JustSomeStars/Tests/EditMode/FlightModel2DTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/FlightRecovery2DTests.cs`

**Interfaces:**
- Consumes: semantic flight input and `PilotingAssist`.
- Produces: bounded-plane boost, brake, drift, momentum, gravity assist,
  authored depth-lane, prediction arc, checkpoint and landing state.

- [x] **Step 1: Write deterministic model tests for acceleration, braking and assist correction**

- [x] **Step 2: Implement the deterministic 2D simulation separately from ship presentation**

- [x] **Step 3: Implement Guided, Balanced and Ace correction profiles**

Guided widens viable routes and corrects steering; Ace reduces correction but never changes story access.

- [x] **Step 4: Implement checkpoint recovery and landing transition hooks**

- [x] **Step 5: Produce the original homemade ship as layered sprites and coherent frame atlases**

Preserve the approved contrast between patched child-built construction and
precise Signal technology. Validate engine, landing, door, cockpit-seat,
damage-state and cosmetic attachment pivots through the sprite pipeline. A
bounded optional 3D reference render is permitted, but no shipping feature may
depend on a ship rig, mesh LOD or free 3D flight space.

- [x] **Step 6: Build a 90-second graybox route and validate touch controls on device**

- [x] **Step 7: Commit**

```bash
git add Assets/_JustSomeStars/Runtime/Flight Assets/_JustSomeStars/Prefabs/Ship Assets/_JustSomeStars/Tests
git commit -m "feat: add assisted 2.5d flight and landing"
```

### Task 18: Implement mission graph, dialogue, hints and Atlas

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Missions.meta`
- Create: `Assets/_JustSomeStars/Runtime/Missions/MissionDefinition.cs`
- Create: `Assets/_JustSomeStars/Runtime/Missions/MissionNode.cs`
- Create: `Assets/_JustSomeStars/Runtime/Missions/MissionDirector.cs`
- Create: `Assets/_JustSomeStars/Runtime/Missions/Task18ProgressionContent.cs`
- Create: `Assets/_JustSomeStars/Runtime/Dialogue.meta`
- Create: `Assets/_JustSomeStars/Runtime/Dialogue/DialogueEntry.cs`
- Create: `Assets/_JustSomeStars/Runtime/Dialogue/DialogueDirector.cs`
- Create: `Assets/_JustSomeStars/Runtime/Dialogue/HintDirector.cs`
- Create: `Assets/_JustSomeStars/Runtime/Atlas.meta`
- Create: `Assets/_JustSomeStars/Runtime/Atlas/AtlasEntry.cs`
- Create: `Assets/_JustSomeStars/Runtime/Atlas/AtlasService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Atlas/LocalizedEnglishCatalog.cs`
- Create: `Assets/_JustSomeStars/Runtime/Atlas/ScienceSourceDefinition.cs`
- Create: `Assets/_JustSomeStars/Content/{Missions,Dialogue,Atlas,ScienceSources,Localization,Resources}/` plus Unity folder metas and authored Task 18 assets
- Create: `Assets/_JustSomeStars/Editor/Validation/Task18ContentValidationContributor.cs`
- Create: `Assets/_JustSomeStars/Editor/Validation/Task18ContentValidationContributor.cs.meta`
- Modify: `Assets/_JustSomeStars/Editor/Validation/ValidationReport.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Core/GameEvents.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Crew/DialogueToken.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Discovery/EvidenceRecorder.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Saving/{GameSave,SaveMerge,SaveMigrator}.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/MissionGraphTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/DialoguePriorityTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/AtlasTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/EditMode/{LocalSaveServiceTests,SaveMigratorTests}.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/FlightDiscoveryAtlasMissionTests.cs`

**Interfaces:**
- Consumes: typed game events and save checkpoints.
- Produces: deterministic mission advancement, authored dialogue/hints and discovery-driven Atlas entries.

- [x] **Step 1: Write graph tests for completion, optional branches, restart and recovery nodes**

- [x] **Step 2: Implement mission definitions and Director event subscriptions**

- [x] **Step 3: Implement dialogue priority, interruption, cooldown and crew token integration**

- [x] **Step 4: Implement behavior-based hints without timers that pressure the player**

- [x] **Step 5: Implement Atlas unlocks with short, balanced and deep localized text plus science-source IDs**

- [x] **Step 6: Validate a tiny flight-to-discovery-to-Atlas mission end to end and commit**

```bash
git add -A -- \
  Assets/_JustSomeStars/Runtime/Missions.meta \
  Assets/_JustSomeStars/Runtime/Missions \
  Assets/_JustSomeStars/Runtime/Dialogue.meta \
  Assets/_JustSomeStars/Runtime/Dialogue \
  Assets/_JustSomeStars/Runtime/Atlas.meta \
  Assets/_JustSomeStars/Runtime/Atlas \
  Assets/_JustSomeStars/Runtime/Core/GameEvents.cs \
  Assets/_JustSomeStars/Runtime/Crew/DialogueToken.cs \
  Assets/_JustSomeStars/Runtime/Discovery/EvidenceRecorder.cs \
  Assets/_JustSomeStars/Runtime/Saving/GameSave.cs \
  Assets/_JustSomeStars/Runtime/Saving/SaveMerge.cs \
  Assets/_JustSomeStars/Runtime/Saving/SaveMigrator.cs \
  Assets/_JustSomeStars/Editor/Validation/ValidationReport.cs \
  Assets/_JustSomeStars/Editor/Validation/Task18ContentValidationContributor.cs \
  Assets/_JustSomeStars/Editor/Validation/Task18ContentValidationContributor.cs.meta \
  Assets/_JustSomeStars/Content/Missions.meta \
  Assets/_JustSomeStars/Content/Missions \
  Assets/_JustSomeStars/Content/Dialogue.meta \
  Assets/_JustSomeStars/Content/Dialogue \
  Assets/_JustSomeStars/Content/Atlas.meta \
  Assets/_JustSomeStars/Content/Atlas \
  Assets/_JustSomeStars/Content/ScienceSources.meta \
  Assets/_JustSomeStars/Content/ScienceSources \
  Assets/_JustSomeStars/Content/Localization.meta \
  Assets/_JustSomeStars/Content/Localization \
  Assets/_JustSomeStars/Content/Resources.meta \
  Assets/_JustSomeStars/Content/Resources \
  Assets/_JustSomeStars/Tests/EditMode/MissionGraphTests.cs \
  Assets/_JustSomeStars/Tests/EditMode/MissionGraphTests.cs.meta \
  Assets/_JustSomeStars/Tests/EditMode/DialoguePriorityTests.cs \
  Assets/_JustSomeStars/Tests/EditMode/DialoguePriorityTests.cs.meta \
  Assets/_JustSomeStars/Tests/EditMode/AtlasTests.cs \
  Assets/_JustSomeStars/Tests/EditMode/AtlasTests.cs.meta \
  Assets/_JustSomeStars/Tests/EditMode/LocalSaveServiceTests.cs \
  Assets/_JustSomeStars/Tests/EditMode/SaveMigratorTests.cs \
  Assets/_JustSomeStars/Tests/PlayMode/FlightDiscoveryAtlasMissionTests.cs \
  Assets/_JustSomeStars/Tests/PlayMode/FlightDiscoveryAtlasMissionTests.cs.meta \
  docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
git commit -m "feat: add missions dialogue hints and Cosmic Atlas"
```

### Task 19: Build Mirra mechanics-complete vertical slice

**Files:**
- Create: `Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity` plus Unity
  folder/asset metas
- Create: `Assets/_JustSomeStars/Content/{Missions,Interactions,Dialogue,Resources}/`
  Task 19 assets and metas
- Create: `Assets/_JustSomeStars/Runtime/{Dialogue,Discovery,Interaction,Missions}/`
  Task 19 components and metas
- Create: `Assets/_JustSomeStars/Runtime/Crew/MirraCrew{ActorRuntime2D,Runtime2D}.cs`
  plus metas
- Create: `Assets/_JustSomeStars/Content/Crew/Traversal/` with the Mirra
  traversal graph, asset meta and folder meta
- Create: `Assets/_JustSomeStars/Tests/PlayMode/MirraMissionTests.cs` plus meta
- Modify: runtime Core, Flight, Player, Discovery, Interaction and mission
  integration seams; Task 17 Flight scene; scene catalogue; Mirra Addressables
  group; Task 18 English/progression assets; affected bootstrap/launch tests;
  `ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: flight, landing, surface, crew, interactions, Lens, missions and Atlas.
- Produces: one final-format layered 2.5D destination loop and the first Signal fragment.

- [x] **Step 1: Graybox the 2.5D approach, landing, twilight route, probe repair, evidence test and departure**

- [x] **Step 2: Write a PlayMode mission test that reaches the first Signal fragment through typed events**

- [x] **Step 3: Implement hot/cold zones, wind field and visual science cues**

- [x] **Step 4: Author Mira/Juno/Ori anchors, dialogue, hints and recovery**

- [x] **Step 5: Add checkpoints before landing, traversal, probe interaction and fragment reveal**

- [x] **Step 6: Complete the production route in Guided, Balanced and Ace,
  plus one exact-APK device playthrough**

The production PlayMode route executes the real scenes and every chapter
transition in Guided, Balanced and Ace. A bounded final exact-APK device
spot-check proves the corrected Mirra presentation and same-process resume.
Explicitly labeled checkpoint-resume fixtures are not described as touch
traversal; the automated production route owns the complete transition proof.

- [x] **Step 7: Commit mechanics-complete Mirra**

```bash
git add -A -- \
  Assets/AddressableAssetsData/AssetGroups/JSS\ Mirra\ Production.asset \
  Assets/_JustSomeStars/Content \
  Assets/_JustSomeStars/Runtime/Core \
  Assets/_JustSomeStars/Runtime/Crew/MirraCrewActorRuntime2D.cs \
  Assets/_JustSomeStars/Runtime/Crew/MirraCrewActorRuntime2D.cs.meta \
  Assets/_JustSomeStars/Runtime/Crew/MirraCrewRuntime2D.cs \
  Assets/_JustSomeStars/Runtime/Crew/MirraCrewRuntime2D.cs.meta \
  Assets/_JustSomeStars/Runtime/Dialogue \
  Assets/_JustSomeStars/Runtime/Discovery \
  Assets/_JustSomeStars/Runtime/Flight \
  Assets/_JustSomeStars/Runtime/Interaction \
  Assets/_JustSomeStars/Runtime/Missions \
  Assets/_JustSomeStars/Runtime/Player \
  Assets/_JustSomeStars/Content/Crew/Traversal.meta \
  Assets/_JustSomeStars/Content/Crew/Traversal \
  Assets/_JustSomeStars/Scenes/Benchmarks/Task17FlightGraybox.unity \
  Assets/_JustSomeStars/Scenes/Destinations.meta \
  Assets/_JustSomeStars/Scenes/Destinations \
  Assets/_JustSomeStars/Tests/PlayMode \
  ProjectSettings/EditorBuildSettings.asset \
  docs/issue-register.md \
  docs/progress/production-execution-ledger.md \
  docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
git commit -m "feat: complete Mirra gameplay slice"
```

### Task 20: Raise Mirra to the approved visual quality bar

**Files:**
- Create: `Assets/_JustSomeStars/Art/2D/Materials/Shared/`
- Create: `Assets/_JustSomeStars/Art/2D/Materials/Mirra/`
- Create: `Assets/_JustSomeStars/Art/2D/VFX/Mirra/`
- Create: `Assets/_JustSomeStars/Content/QualityProfiles/`
- Create: `Assets/_JustSomeStars/Runtime/Rendering2D/MirraQualityProfile.cs`
- Create: `Assets/_JustSomeStars/Runtime/Rendering2D/MirraQualityController2D.cs`
- Modify: `Assets/_JustSomeStars/Runtime/Accessibility/GameSettings.cs`
- Modify: `Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity`
- Modify: `Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity`
- Modify: `Assets/_JustSomeStars/Tests/EditMode/Mirra2DAssetValidationTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/MirraQualityAssetTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/MirraQualityControllerTests.cs`
- Create: `docs/art/mirra-quality-review.md`
- Create: `outputs/quality-reviews/mirra-device-capture.png`
- Modify: `docs/issue-register.md`

**Interfaces:**
- Produces: Performance, Balanced, Cinematic and High Frame Rate profiles.
- Produces: direct screenshot comparison against the canonical quality image.

- [x] **Step 1: Build shared 2D-lit Shader Graph foundations for sprite normals, emission, palette masks, fabric, metal, rock, ice, visor, hologram and atmosphere**

- [x] **Step 2: Establish Mirra's orange/blue divide with bounded 2D lights, baked gradients and layer-specific grading**

- [x] **Step 3: Add layered terrain, parallax atmosphere, sprite contact treatment, restrained bloom and Signal focal effects**

- [x] **Step 4: Stage the Captain, two companions, Ori and ship for silhouette/readability checks**

- [x] **Step 5: Capture the same representative camera on the supported 720×1616, 280-dpi Android validation target in Performance and Balanced**

Use the authenticated Limrun/Argent device route defined by the controlling agent
environment. Preserve the exact device model, resolution, density, renderer and APK
hash in evidence; never label hosted-device results as physical Realme Narzo evidence.
Physical Realme coverage is deferred to the later broad device/performance campaign.

- [x] **Step 6: Review side-by-side for material credibility, lighting, silhouettes, density, HUD clarity, focal point and emotional impact**

Record pass/fail evidence in `mirra-quality-review.md`; mechanics completion alone cannot pass this gate.

- [x] **Step 7: Commit the approved benchmark**

```bash
git add -A -- Assets/_JustSomeStars/Art/2D/Materials.meta Assets/_JustSomeStars/Art/2D/Materials Assets/_JustSomeStars/Art/2D/VFX.meta Assets/_JustSomeStars/Art/2D/VFX Assets/_JustSomeStars/Content/QualityProfiles.meta Assets/_JustSomeStars/Content/QualityProfiles Assets/_JustSomeStars/Runtime/Accessibility/GameSettings.cs Assets/_JustSomeStars/Runtime/Rendering2D/MirraQualityProfile.cs Assets/_JustSomeStars/Runtime/Rendering2D/MirraQualityProfile.cs.meta Assets/_JustSomeStars/Runtime/Rendering2D/MirraQualityController2D.cs Assets/_JustSomeStars/Runtime/Rendering2D/MirraQualityController2D.cs.meta Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity Assets/_JustSomeStars/Tests/EditMode/Mirra2DAssetValidationTests.cs Assets/_JustSomeStars/Tests/EditMode/MirraQualityAssetTests.cs Assets/_JustSomeStars/Tests/EditMode/MirraQualityAssetTests.cs.meta Assets/_JustSomeStars/Tests/PlayMode/MirraQualityControllerTests.cs Assets/_JustSomeStars/Tests/PlayMode/MirraQualityControllerTests.cs.meta docs/art/mirra-quality-review.md outputs/quality-reviews/mirra-device-capture.png docs/issue-register.md docs/superpowers/plans/2026-08-21-just-some-stars-implementation.md
git commit -m "art: reach approved Mirra mobile quality bar"
```

### Task 21: Implement optional Google accounts and cloud save

**Files:**
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendController.cs`
- Modify: `Assets/_JustSomeStars/Scenes/Core/Frontend.unity`
- Modify: `Assets/_JustSomeStars/Tests/EditMode/FrontendSceneAssetTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/FrontendControllerTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/ApplicationLaunchIntegrationTests.cs`
- Create: `Assets/_JustSomeStars/Runtime/Accounts/IAccountService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Accounts/GuestAccountService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Accounts/FirebaseAccountService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Saving/ICloudSaveService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Saving/FirestoreCloudSaveService.cs`
- Create: `firebase/firestore.rules`
- Create: `firebase/firestore.indexes.json`
- Create: `Assets/_JustSomeStars/Tests/EditMode/CloudSaveMergeTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/AccountLinkTests.cs`

**Interfaces:**
- Produces: guest identity, `LinkGoogleAsync`, `SignOutAsync`, `DeleteAccountAsync` and authenticated Firebase UID.
- Produces: upload/download/merge of versioned `GameSave`.

- [ ] **Step 1: Add Firebase Auth and Firestore Unity packages through their official distribution**

Register both Android package IDs in one Firebase project. Store `google-services.json` according to the repository's secret/config policy; never commit admin credentials.

- [ ] **Step 2: Write tests for guest preservation, Google link and union merge**

- [ ] **Step 3: Implement local guest flow and optional Google linking**

The first run never blocks on Firebase. Linking migrates the active local save under the authenticated UID.
Because this is the first task that introduces a real online service, replace
Task 5's `NO ONLINE SERVICES` footer with state-derived truthful copy. The
offline guest state and the available/linked/unavailable cloud states must each
describe only the service state actually in effect; controller, scene and real
launch tests must reject the stale Task 5 literal after Firebase lands.

- [ ] **Step 4: Implement Firestore documents and UID-scoped rules**

Rules allow a signed-in user to access only `/users/{uid}` where `request.auth.uid == uid`.

- [ ] **Step 5: Implement export, unlink and complete cloud deletion**

- [ ] **Step 6: Test offline edits, second-device merge and sign-out on physical Android**

- [ ] **Step 7: Commit code/rules and exclude local secrets**

```bash
git add Assets/_JustSomeStars/Runtime/Accounts Assets/_JustSomeStars/Runtime/Saving firebase Assets/_JustSomeStars/Tests .gitignore
git commit -m "feat: add optional Google cloud saves"
```

### Task 22: Implement private birthdays and annual gifts

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Accounts/Birthday.cs`
- Create: `Assets/_JustSomeStars/Runtime/Accounts/BirthdayGiftService.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/Account/BirthdaySetupController.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/Account/GrownUpConfirmationController.cs`
- Create: `firebase/functions/src/birthdayGift.ts`
- Create: `firebase/functions/src/index.ts`
- Create: `firebase/functions/package.json`
- Create: `firebase/functions/tsconfig.json`
- Create: `Assets/_JustSomeStars/Content/Cosmetics/birthday/`
- Create: `Assets/_JustSomeStars/Tests/EditMode/BirthdayTests.cs`
- Create: `firebase/functions/test/birthdayGift.test.ts`

**Interfaces:**
- Produces: private day/month/year, derived age band, 30-day claim window and one gift per account/year.

- [ ] **Step 1: Write leap-day, timezone, claim-window and repeated-claim tests**

```typescript
it("grants one gift for the account in the active birthday window", async () => {
  const first = await claimBirthdayGift(fixtureRequest);
  const second = await claimBirthdayGift(fixtureRequest);
  expect(first.granted).toBe(true);
  expect(second.granted).toBe(false);
});
```

- [ ] **Step 2: Implement the neutral day/month/year setup screen, local validation and one correction allowance**

Derive child, teen or adult privacy state without suggesting which birth date unlocks a less restricted flow. Guest play continues immediately after setup; cloud link, purchases and external links use the grown-up confirmation rules defined by the resulting age state.

- [ ] **Step 3: Implement the authenticated server-time grant function and transactional `lastBirthdayGiftYear` update**

- [ ] **Step 4: Build the Clubhouse celebration, Ori delivery and no-purchase presentation**

- [ ] **Step 5: Verify guest-local and cloud-secure paths without sending DOB to analytics**

- [ ] **Step 6: Commit**

```bash
git add Assets/_JustSomeStars/Runtime/Accounts Assets/_JustSomeStars/Content/Cosmetics/birthday Assets/_JustSomeStars/Tests firebase/functions
git commit -m "feat: add private birthday celebrations"
```

### Task 23: Integrate RevenueCat Test Store and Google Play commerce

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Commerce/IStoreService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Commerce/StoreProduct.cs`
- Create: `Assets/_JustSomeStars/Runtime/Commerce/EntitlementSnapshot.cs`
- Create: `Assets/_JustSomeStars/Runtime/Commerce/RevenueCatStoreService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Commerce/OfflineEntitlementCache.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/Shop/ShopController.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/EntitlementCacheTests.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/ShopFlowTests.cs`
- Create: `docs/release/revenuecat-product-map.md`

**Interfaces:**
- Produces: initialize, get offerings, purchase, restore, refresh and cached entitlement checks.
- Uses Firebase UID after login and RevenueCat anonymous identity for guests.

- [ ] **Step 1: Install the official RevenueCat Unity SDK and add Development/Test Store configuration**

- [ ] **Step 2: Create stable entitlements and Test Store products from the technical spec**

Record each store product, RevenueCat product, package and entitlement mapping in `revenuecat-product-map.md`.

- [ ] **Step 3: Write fake-store tests for success, cancel, interruption, restore, offline and unavailable states**

- [ ] **Step 4: Implement `RevenueCatStoreService` and durable verified cache**

Never grant from UI success alone; consume the refreshed entitlement snapshot.

- [ ] **Step 5: Implement transparent shop UI, grown-up confirmation and Restore Purchases**

- [ ] **Step 6: Complete Test Store purchase, upload billing-enabled Google build, create real products and complete licensed Google test purchase**

- [ ] **Step 7: Confirm customer, transaction and entitlement in RevenueCat, then commit**

```bash
git add Assets/_JustSomeStars/Runtime/Commerce Assets/_JustSomeStars/Runtime/UI/Shop Assets/_JustSomeStars/Tests docs/release
git commit -m "feat: add RevenueCat Google commerce"
```

### Task 24: Implement and isolate Galaxy Store commerce

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Commerce/GalaxyStoreService.cs`
- Create: `Assets/Plugins/Android/jss-galaxy-billing/`
- Create: `Assets/_JustSomeStars/Tests/EditMode/StoreVariantIsolationTests.cs`
- Create: `docs/release/galaxy-product-map.md`

**Interfaces:**
- Implements the same `IStoreService` contract through the native RevenueCat Galaxy module.
- Falls back to the separately tested Samsung Unity IAP adapter only if the native bridge cannot meet release verification.

- [ ] **Step 1: Add the Galaxy native Android module in the Galaxy-only Gradle dependency path**

- [ ] **Step 2: Implement the Kotlin bridge for configure, offerings/products, purchase, restore and error callbacks**

- [ ] **Step 3: Implement the C# adapter and map Galaxy products to the shared entitlements**

- [ ] **Step 4: Write assembly/build tests proving Google billing is absent from Galaxy and Galaxy billing is absent from Google**

- [ ] **Step 5: Test Galaxy success and forced-failure modes with a licensed tester on a physical Samsung device**

- [ ] **Step 6: Record the production-mode checklist and commit**

```bash
git add Assets/Plugins/Android Assets/_JustSomeStars/Runtime/Commerce Assets/_JustSomeStars/Tests docs/release
git commit -m "feat: add isolated Galaxy commerce adapter"
```

### Task 25: Build Koro/Vesper and the second Signal fragment

**Files:**
- Create: `Assets/_JustSomeStars/Scenes/Destinations/KoroVesper.unity`
- Create: `Assets/_JustSomeStars/Content/Missions/koro-vesper-chapter.asset`
- Create: `Assets/_JustSomeStars/Art/2D/Environments/KoroVesper/`
- Create: `Assets/_JustSomeStars/Runtime/Discovery/GeyserController.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/KoroVesperMissionTests.cs`

**Interfaces:**
- Produces: Vesper gravity route, Koro landing, low-gravity traversal, spectra comparison and second fragment.

- [ ] **Step 1: Graybox the complete route and checkpoint graph**

- [ ] **Step 2: Write an end-to-end mission test from Vesper arrival through second-fragment recovery**

- [ ] **Step 3: Build layered 2.5D low-gravity traversal, geyser timing and spectrum evidence**

- [ ] **Step 4: Author companions, dialogue, optional observations and Atlas records**

- [ ] **Step 5: Produce ice-cyan/violet art, lighting and VFX at Mirra's measured quality**

- [ ] **Step 6: Complete device runs across assistance/science-depth combinations and commit**

```bash
git add Assets/_JustSomeStars/Scenes/Destinations/KoroVesper.unity Assets/_JustSomeStars/Content Assets/_JustSomeStars/Art/2D/Environments/KoroVesper Assets/_JustSomeStars/Runtime/Discovery Assets/_JustSomeStars/Tests
git commit -m "feat: complete Koro and Vesper chapter"
```

### Task 26: Build Aster Veil, finale and dinner ending

**Files:**
- Create: `Assets/_JustSomeStars/Scenes/Destinations/AsterVeil.unity`
- Create: `Assets/_JustSomeStars/Content/Missions/aster-veil-chapter.asset`
- Create: `Assets/_JustSomeStars/Runtime/Flight/DebrisFieldController.cs`
- Create: `Assets/_JustSomeStars/Art/2D/Environments/AsterVeil/`
- Create: `Assets/_JustSomeStars/Scenes/Cinematics/SignalReassembly.unity`
- Create: `Assets/_JustSomeStars/Scenes/Core/Clubhouse.unity`
- Create: `Assets/_JustSomeStars/Scenes/Cinematics/Opening.unity`
- Create: `Assets/_JustSomeStars/Scenes/Cinematics/DinnerEnding.unity`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/ChapterOneCompletionTests.cs`

**Interfaces:**
- Produces: third fragment, star-map reveal, return, dinner line, Ori pulse and Chapter Two hook.

- [ ] **Step 1: Graybox the 2.5D momentum/gravity-assist route and authored shifting-debris lanes**

- [ ] **Step 2: Write an end-to-end Chapter One test from clean save to credits flag**

- [ ] **Step 3: Implement deterministic seeded debris lanes and recovery checkpoints**

- [ ] **Step 4: Author crew trust payoff, fragment reconstruction and escape**

- [ ] **Step 5: Build the reusable Clubhouse, opening promise and departure sequence**

The opening introduces all five kids and Ori, records the Captain customization, establishes the parents' “back before dinner” permission and launches the transformed homemade ship.

- [ ] **Step 6: Build the Clubhouse crash-return and dinner ending as real-time scenes where the customized Captain is visible**

- [ ] **Step 7: Add final pulse and save the completed Chapter One state before credits**

- [ ] **Step 8: Complete full Guided/Balanced/Ace device runs and commit**

```bash
git add Assets/_JustSomeStars/Scenes Assets/_JustSomeStars/Content Assets/_JustSomeStars/Runtime/Flight Assets/_JustSomeStars/Art/2D/Environments/AsterVeil Assets/_JustSomeStars/Tests
git commit -m "feat: complete Aster Veil and dinner finale"
```

### Task 27: Build the 100-plus cosmetic catalogue and editions

**Files:**
- Create: `Assets/_JustSomeStars/Content/Cosmetics/CosmeticCatalog.asset`
- Create: `Assets/_JustSomeStars/Content/Cosmetics/{Captain,Ori,Ship,Lens,Clubhouse,Photo,Crew}/`
- Create: `Assets/_JustSomeStars/Runtime/Cosmetics/CosmeticCatalog.cs`
- Create: `Assets/_JustSomeStars/Runtime/Cosmetics/OwnershipResolver.cs`
- Create: `Assets/_JustSomeStars/Runtime/Cosmetics/EditionFeatureService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Missions/ExpeditionReplayService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Missions/ExpeditionModifier.cs`
- Create: `Assets/_JustSomeStars/Runtime/Atlas/DevelopmentArchiveService.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/SoundtrackJukeboxController.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/CosmeticCatalogTests.cs`
- Create: `Assets/_JustSomeStars/Tests/EditMode/EditionFeatureTests.cs`
- Create: `docs/product/cosmetic-catalog.csv`

**Interfaces:**
- Consumes: gameplay awards, birthday grants and RevenueCat entitlements.
- Produces: visible inventory, ownership source and compatible equipped loadouts.

- [ ] **Step 1: Define every launch item and pack in `cosmetic-catalog.csv`**

Each row includes stable ID, category, free/earned/paid source, pack, body fits, asset references and entitlement.

- [ ] **Step 2: Write validation tests for unique IDs, 100-plus count, pack membership, ownership and all required body fits**

- [ ] **Step 3: Produce bounded sprite layers, palette masks, icons and effects through the approved art pipeline**

Every silhouette-changing Captain item supplies compatible atlas rows for all
three body families. Color-only variants use palette masks; they do not clone
complete atlases. Crew, Ori, ship and environment cosmetics declare their own
sprite attachment and frame-event compatibility.

- [ ] **Step 4: Implement ownership resolution with precedence: earned, birthday, edition, individual purchase**

- [ ] **Step 5: Implement Explorer Edition features and Founder's/Launch pack presentation**

`EditionFeatureService` gates Expedition Replay, advanced cinematic modifiers, the development/science archive and soundtrack jukebox from the single `explorer_edition` entitlement. Tests must prove that losing network access does not remove a previously verified edition and that base story, Atlas science and standard Photo Mode remain available without it.

- [ ] **Step 6: Test equip/save/cloud/restore across all categories and commit**

```bash
git add Assets/_JustSomeStars/Content/Cosmetics Assets/_JustSomeStars/Runtime/Cosmetics Assets/_JustSomeStars/Art Assets/_JustSomeStars/Tests docs/product
git commit -m "feat: add launch cosmetic catalogue and editions"
```

### Task 28: Complete UI, localization and accessibility

**Files:**
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendController.cs`
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendView.cs`
- Modify: `Assets/_JustSomeStars/Runtime/UI/FrontendContracts.cs`
- Modify: `Assets/_JustSomeStars/Scenes/Core/Frontend.unity`
- Modify: `Assets/_JustSomeStars/Tests/EditMode/FrontendSceneAssetTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/FrontendControllerTests.cs`
- Modify: `Assets/_JustSomeStars/Tests/PlayMode/ApplicationLaunchIntegrationTests.cs`
- Create: `Assets/_JustSomeStars/Runtime/UI/PhotoModeController.cs`
- Create: `Assets/_JustSomeStars/Runtime/Accessibility/AccessibilityApplier.cs`
- Create: `Assets/_JustSomeStars/Content/Localization/English/`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/AccessibilityUiTests.cs`
- Create: `docs/qa/accessibility-matrix.md`

**Interfaces:**
- Consumes: settings, mode, mission, dialogue, account, store and catalogue state.
- Produces: responsive touch HUD, subtitles, menus, customization, Atlas, shop, account and parental flows.

- [ ] **Step 1: Create the homemade/Signal UI token set and reusable components**

- [ ] **Step 2: Implement responsive safe-area layouts for common phone and foldable aspect ratios**

- [ ] **Step 3: Move every player-facing Task 5 and later string to English localization tables**

This includes the Frontend title/status/version wrapper, disabled Continue
explanation, Settings, Privacy, the Task-21 state-derived footer and the
`Credits & Licenses` title and product-credit wrapper. Keep both canonical
license `TextAsset` dependencies—the Liberation Sans OFL and Apache License
2.0—immutable and verbatim: do not translate or copy either into a string table.
The localized credits wrapper must identify the covered components, concatenate
both raw license texts and preserve the same readable scroll access.

Replace the Task 5 `Development Flight` status and disabled placeholder
Continue state once real launch content exists. Present truthful New Game and
Continue controls driven by the injected local-save/content state: New Game
navigates into the real opening/customization route, while Continue is enabled
only for a valid recoverable local save and navigates to its real checkpoint.
No button may remain decorative or claim unavailable content. Controller,
scene, integration and accessibility tests cover no-save, valid-save, corrupt
save/recovery and unavailable-content states without weakening verbatim OFL
scroll access.

- [ ] **Step 4: Implement text scale, font choice, captions, speaker labels, color-safe symbols, contrast, reduced effects and left-handed controls**

- [ ] **Step 5: Implement base Photo Mode and Explorer Edition extensions**

Base Photo Mode provides pause, bounded pan/zoom, depth-layer focus, exposure,
clean HUD and earned frames. Explorer Edition adds cinematic lenses, expanded
poses, advanced framing and saved presets without introducing a free 3D orbit
or reducing base-game screenshot quality.

- [ ] **Step 6: Write automated navigation tests with maximum text size and combined accessibility options**

- [ ] **Step 7: Complete the manual matrix before the opening cinematic and in every game mode**

- [ ] **Step 8: Commit**

```bash
git add Assets/_JustSomeStars/Runtime/UI Assets/_JustSomeStars/Runtime/Accessibility Assets/_JustSomeStars/Content/Localization Assets/_JustSomeStars/Tests docs/qa
git commit -m "feat: complete accessible localized mobile UI"
```

### Task 29: Complete frame-atlas animation, audio and cinematic media

**Files:**
- Create: `Assets/_JustSomeStars/Art/2D/Characters/Animations/`
- Create: `Assets/_JustSomeStars/Audio/Music/`
- Create: `Assets/_JustSomeStars/Audio/SFX/`
- Create: `Assets/_JustSomeStars/Runtime/Core/CinematicDirector.cs`
- Create: `Assets/_JustSomeStars/Runtime/Core/AudioDirector.cs`
- Create: `Assets/_JustSomeStars/Content/Cinematics/`
- Create: `docs/media/media-rights-ledger.csv`

**Interfaces:**
- Produces: real-time customized-Captain sprite cinematics, optional
  pre-rendered establishing shots, layered music and caption-safe dialogue timing.

- [ ] **Step 1: Import/author coherent frame-atlas locomotion, interaction, reaction, conversation and cinematic clips**

- [ ] **Step 2: Implement deterministic frame events and facial-expression/viseme atlas control**

The same event contract drives foot contacts, tools, audio, VFX, captions and
interaction release. Missing frames, mismatched pivots or unsynchronized
Captain layers fail closed rather than silently substituting skeletal motion.

- [ ] **Step 3: Generate approved Flow shots only where the customized Captain is absent**

- [ ] **Step 4: Generate Lyria/Flowmusic material and edit it into loops, stems and intensity variants**

- [ ] **Step 5: Implement Signal motif progression and music state transitions**

- [ ] **Step 6: Record source, license, generation tool and edit status for every external media asset**

- [ ] **Step 7: Test missing-video fallback, subtitle timing and independent audio volumes, then commit**

```bash
git add Assets/_JustSomeStars/Art/2D/Characters/Animations Assets/_JustSomeStars/Audio Assets/_JustSomeStars/Runtime/Core Assets/_JustSomeStars/Content/Cinematics docs/media
git commit -m "feat: complete animation audio and cinematics"
```

### Task 30: Meet performance, memory and thermal gates

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Core/QualityProfileService.cs`
- Create: `Assets/_JustSomeStars/Editor/Validation/PerformanceBudgetValidator.cs`
- Create: `Assets/_JustSomeStars/Tests/PlayMode/PerformanceSmokeTests.cs`
- Create: `docs/qa/device-matrix.md`
- Create: `docs/qa/performance-results.md`

**Interfaces:**
- Produces: selectable Performance, Balanced, Cinematic and High Frame Rate runtime profiles with dynamic resolution.

- [ ] **Step 1: Encode texture residency, atlas count, transparent overdraw, active-character, 2D-light, VFX and memory budgets into validator rules**

- [ ] **Step 2: Add CPU/GPU frame markers around player, crew, flight, Lens, UI and streaming systems**

- [ ] **Step 3: Run automated representative-scene smoke tests**

- [ ] **Step 4: Run 20-minute Realme Narzo Mirra, flight and Aster thermal soaks**

Record median, 1% low, memory peak, thermal behavior and battery change in `performance-results.md`.

- [ ] **Step 5: Fix measured bottlenecks using atlas partitioning, import max-size/ASTC settings, batching, pooling, culling, Addressables, streaming and profile scaling**

- [ ] **Step 6: Repeat until Performance sustains 30 FPS and no destination exceeds its memory budget**

- [ ] **Step 7: Capture the content-lock quality comparison and commit**

```bash
git add Assets/_JustSomeStars/Runtime/Core Assets/_JustSomeStars/Editor/Validation Assets/_JustSomeStars/Tests docs/qa outputs/quality-reviews
git commit -m "perf: meet Android quality and thermal budgets"
```

### Task 31: Harden Codemagic and connect Limrun, Argent and development assistants

**Files:**
- Modify: `codemagic.yaml`
- Modify: `docs/tooling/codemagic.md`
- Create: `docs/tooling/limrun.md`
- Create: `docs/tooling/argent.md`
- Create: `docs/tooling/junie.md`

**Interfaces:**
- Consumes: Unity CLI, validators and signed-build variables.
- Produces: reproducible tests/build artifacts and external device-agent verification.

- [ ] **Step 1: Add encrypted signing and store variables to the existing Codemagic project**

- [ ] **Step 2: Implement workflows for validation, EditMode, PlayMode, Addressables, Google and Galaxy builds**

The targeted launch smoke now filters
`JustSomeStars.Tests.PlayMode.ApplicationLaunchIntegrationTests`; do not retain
the deleted Task 5 development test name after Task 8's installer migration.
Retain or deliberately evolve Task 5's fail-closed Git LFS hydration and pointer
checks, exact OFL and Apache payload checks, EmojiOne exclusion, effective
GameActivity contract, EmojiCompat-initializer rejection and preserved
ProcessLifecycle initializer. A later workflow must never silently weaken those
artifact gates while adding store signing or publishing.

- [ ] **Step 3: Verify clean Google and Galaxy CI builds match local package ID, version and symbols**

- [ ] **Step 4: Use Limrun credits for Android install, launch, smoke, screenshot and demo validation**

- [ ] **Step 5: Use the Task 0 Argent CLI/MCP/skills setup to run the app, observe one seeded failure, fix it and preserve QA evidence**

- [ ] **Step 6: Configure Junie for bounded C#, editor and test assistance; require normal diff review and Unity tests for its output**

- [ ] **Step 7: Commit public workflows and redacted instructions**

```bash
git add codemagic.yaml docs/tooling
git commit -m "ci: connect mobile build and agent test services"
```

### Task 32: Activate family-safe growth and notification services

**Files:**
- Create: `Assets/_JustSomeStars/Runtime/Platform/IAnalyticsService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Platform/INotificationService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Platform/LayersAnalyticsService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Platform/TenjinAnalyticsService.cs`
- Create: `Assets/_JustSomeStars/Runtime/Platform/OneSignalNotificationService.cs`
- Create: `docs/growth/layers-growth-loop.md`
- Create: `docs/growth/tenjin-attribution.md`
- Create: `docs/growth/noise-launch.md`
- Modify: `outputs/just-some-stars-technical-build-plan.md`

**Interfaces:**
- Consumes: age/privacy state and explicit consent.
- Produces: optional analytics/notification behavior that initializes only when permitted.

- [ ] **Step 1: Finish Layers onboarding with repository and store context**

Define one audience, hypothesis, channel, expected behavior and measurable signal in `layers-growth-loop.md`; install and verify the Layers SDK if its mixed-audience configuration passes the privacy review.

- [ ] **Step 2: Register the real app in Tenjin and document allowed adult/eligible attribution events**

Never send DOB, child advertising ID or story content.

- [ ] **Step 3: Activate OneSignal only after billing/company requirements are accepted**

Call consent-required before SDK initialization. Use notifications for opted-in birthday-gift availability and major updates, with in-game opt-out.

- [ ] **Step 4: Prepare Noise creator formats after the public app URL exists and the $5 validation/spend is approved**

- [ ] **Step 5: Test child, unknown, teen and adult startup paths with network capture**

Expected: disallowed SDKs send no data before the required age/consent state.

- [ ] **Step 6: Update the ShipKit ledger with actual activation evidence and commit**

```bash
git add Assets/_JustSomeStars/Runtime/Platform docs/growth outputs/just-some-stars-technical-build-plan.md
git commit -m "feat: add consent-gated growth services"
```

### Task 33: Complete store verification, public release and milestone claims

**Files:**
- Create: `docs/release/release-candidate-checklist.md`
- Create: `docs/release/store-review-notes.md`
- Create: `docs/release/live-purchase-evidence.md`
- Create: `docs/release/shipkit-milestones.md`
- Create: `outputs/quality-reviews/release-candidate-capture.png`
- Modify: `outputs/just-some-stars-technical-build-plan.md`

**Interfaces:**
- Consumes: complete Chapter One, signed builds, live products and store accounts.
- Produces: at least one publicly downloadable qualifying store release and one verified real RevenueCat purchase.

- [ ] **Step 1: Run the full automated suite and content validator from a clean checkout**

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -buildTarget Android -projectPath "$PWD" \
  -executeMethod JustSomeStars.Editor.Validation.ProjectContentValidator.ValidateForCi
"$JSS_UNITY_EDITOR" -batchmode -nographics -buildTarget Android -projectPath "$PWD" \
  -runTests -testPlatform editmode -assemblyNames JustSomeStars.EditModeTests \
  -testResults Builds/TestResults/release-edit.xml
"$JSS_UNITY_EDITOR" -batchmode -nographics -buildTarget Android -projectPath "$PWD" \
  -runTests -testPlatform playmode -assemblyNames JustSomeStars.PlayModeTests \
  -testResults Builds/TestResults/release-play.xml
```

- [ ] **Step 2: Execute the release matrix**

Verify clean install, upgrade, airplane-mode completion, every checkpoint resume, corrupted-save recovery, account link/delete, birthday, purchase cancel/interruption/restore, Guided/Balanced/Ace, accessibility combinations and store-adapter isolation.

- [ ] **Step 3: Capture the release-candidate quality comparison on device**

Compare against `outputs/just-some-stars-2.5d-gameplay-target-v1.png`; record
the capture and sign-off in the release checklist.

- [ ] **Step 4: Submit Galaxy and Google candidates with truthful data-safety, purchase and optional-login review notes**

- [ ] **Step 5: Respond to review findings, publish through the first approved qualifying store and verify public installation**

- [ ] **Step 6: Complete a real purchase and record store receipt reference, RevenueCat customer, entitlement and restoration evidence without committing private receipt data**

- [ ] **Step 7: Refresh Shipaton email after release and first revenue**

Claim newly unlocked free/no-card benefits, claim Stripe's processing credit when the stated milestone is satisfied, preserve Lance for a deliberate iOS expansion and update `shipkit-milestones.md`.

- [ ] **Step 8: Tag the release commit**

```bash
git add docs/release outputs/quality-reviews outputs/just-some-stars-technical-build-plan.md
git commit -m "release: ship Just Some Stars chapter one"
git tag -a v1.0.0 -m "Just Some Stars Chapter One"
```

Expected: the public listing is downloadable before the Shipaton deadline, Chapter One works offline, RevenueCat shows the real transaction and the implementation checklist contains no unchecked release blocker.
