# Codemagic Android Internal workflow

Status as of 2026-08-22: **the Codemagic personal account, GitHub application,
public repository and `setup/task-0-environment` workflow are connected;
Codemagic found `codemagic.yaml`, and the account reports 500 free macOS minutes
with 0 used**.

The account has Unity Personal and no Plus/Pro serial. Codemagic's documented
cloud Unity route requires Plus/Pro, so the user explicitly deferred remote
execution rather than spending minutes on a guaranteed activation failure. No
remote build, Unity seat action or store action is claimed.

## Scope

`codemagic.yaml` defines one deliberately narrow workflow,
`android-internal`. It reproduces the existing Android Internal CLI contract; it
does not sign or upload a Google Play or Galaxy release.

Codemagic provisions the requested macOS Unity environment before workflow
scripts. The workflow scripts then run, in order:

1. A pre-activation Git LFS gate requires `git-lfs`, installs repository-local
   skip-smudge configuration, explicitly pulls and checks out every tracked LFS
   object, and rejects pointers or object mismatches.
2. The pinned editor activates an encrypted Unity Plus or Pro seat without a
   retained activation log.
3. A clean-checkout/toolchain preflight checks project version
   `6000.3.22f1 (1c726e1fb402)`, the editor's logged revision and its bundled
   Android Player, OpenJDK, SDK, NDK and `apkanalyzer` paths.
4. The complete project-owned EditMode assembly runs and its XML must describe
   exactly 211 passed tests with no failed, skipped or inconclusive tests.
5. The real Task 5 Boot-to-Frontend PlayMode smoke class runs and its XML must
   describe exactly one passed test with no failed, skipped or inconclusive
   tests.
6. `BuildCli.BuildAndroidInternal` runs with Android already selected.
7. The exact APK is checked for ZIP integrity, identity, version, debug state,
   ARM64, canonical font-license bytes, unused EmojiOne absence and SHA-256.
8. Codemagic's post-build `publishing.scripts` hook returns the Unity seat after
   a success or failure.

Task 9's `ProjectContentValidator.ValidateForCi` does not exist yet and is not
called or imitated here. Task 31 owns the later signed, store-specific,
Addressables-validation, full-PlayMode and device-agent hardening.

## Pinned macOS provisioning route

The workflow uses Codemagic's current macOS quick-install field:

```yaml
instance_type: mac_mini_m2
environment:
  unity: 6000.3.22f1
```

Codemagic documents this route as installing the requested editor through its
macOS Unity/Hub provisioning and setting `UNITY_HOME` to
`/Applications/Unity/Hub/Editor/<version>/Unity.app`. Its documented macOS Unity
images include Android support. This workflow does not assume a licensed or
preactivated Linux editor supplied by an opaque path.

Provisioning is still fail-closed. The job requires the exact expected
`UNITY_HOME`, checks `ProjectVersion.txt`, starts the editor and requires
`6000.3.22f1 (1c726e1fb402)` in its probe log. It also requires these bundled
module locations before tests:

```text
$UNITY_HOME/Contents/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java
$UNITY_HOME/Contents/PlaybackEngines/AndroidPlayer/SDK
$UNITY_HOME/Contents/PlaybackEngines/AndroidPlayer/NDK
$UNITY_HOME/Contents/PlaybackEngines/AndroidPlayer/SDK/cmdline-tools/16.0/bin/apkanalyzer
```

The revision probe log lives in the runner's temporary directory and is removed
on success or failure; only the matched non-secret version line reaches the job
log.

The account exposes 500 free macOS minutes, but no runner was allocated and the
exact `mac_mini_m2`/Unity provisioner path was not exercised. Those checks are
deferred with the remote run; do not substitute another patch, revision, host,
Android toolchain or fake license value merely to make the job start.

This follows Codemagic's official
[Unity build guidance](https://docs.codemagic.io/yaml-quick-start/building-a-unity-app/)
and [macOS Unity provisioning guidance](https://docs.codemagic.io/knowledge-others/install-unity-version/).
The remote account must still validate the configuration before it is treated
as operational.

## Fail-closed Git LFS checkout

The first script runs before Unity activation so a missing binary, failed fetch,
pointer-only checkout or corrupt LFS object cannot consume a Unity seat. It:

```text
requires git-lfs
git lfs install --local --skip-smudge --skip-repo
git lfs pull --include="" --exclude=""
git lfs checkout
git lfs ls-files --json
```

`--skip-smudge` makes hydration deliberate rather than relying on checkout
defaults. The empty include/exclude arguments override any narrower LFS fetch
configuration, and the explicit `checkout` is retained even though `pull`
normally performs one. `--skip-repo` avoids installing an unused CI pre-push
hook while keeping filter configuration local to the ephemeral checkout. Git
terminal prompting is disabled so missing remote authorization fails instead
of stalling a non-interactive runner.

The JSON listing is the authoritative enumeration of tracked LFS paths. A
Python standard-library verifier requires a nonempty list and, for every entry:

- a regular non-symlink file contained by the checkout;
- `checkout` and `downloaded` both true;
- no valid Git LFS pointer remaining in the worktree;
- actual byte length equal to the indexed LFS size; and
- actual SHA-256 equal to the indexed LFS object ID.

It additionally requires
`Assets/TextMesh Pro/Sprites/EmojiOne.png` to be an enumerated LFS path and to
start with a real PNG signature followed by a valid nonzero `IHDR`. This source
image remains outside `Resources` and is intentionally absent from the player,
but the repository must retain the genuine attributed source file rather than
pointer text. The temporary LFS manifest is removed on success or failure.

The embedded verifier passed an isolated hydrated-object fixture and rejected
an isolated EmojiOne LFS-pointer fixture for the expected reason. Those fixture
results do not prove that a future Codemagic checkout can authenticate to the
LFS remote or download the repository's real objects; remote hydration remains
an external gate.

## Encrypted Unity credential group

Before an authorized run, create an encrypted Codemagic variable group named
`jss_unity_ci` containing exactly the cloud-build license inputs:

```text
UNITY_SERIAL
UNITY_EMAIL
UNITY_PASSWORD
```

Codemagic's Unity guidance requires a valid Unity Plus or Pro license for cloud
builds; a Personal license is not represented as sufficient here. Mark every
variable **Secret** in Codemagic. Do not put values, `UNITY_HOME`, editor paths,
store signing material or service credentials in the group.

Unity's documented serial activation and return commands necessarily receive
the three values as environment-backed process arguments. The workflow keeps
only their variable references in YAML, disables shell tracing, never echoes
them and sends both activation and return logs to `/dev/null`. Do not change
those invocations to `-logFile -`, enable `set -x`, dump the environment or
retain process listings.

The `publishing.scripts` block is cleanup, not store publication. Codemagic
documents that this hook runs after both successful and failed builds, but it
does **not** run when a build is manually cancelled before publishing. A
cancelled run may therefore strand a seat and block later builds. Before
cancelling, prefer allowing the return hook to run; after a cancellation, the
account owner must inspect the Unity dashboard and manually return the seat if
necessary before retrying.

## Canonical test contract

Both Unity Test Framework processes omit `-quit`, select Android before Unity
opens and limit discovery to a project-owned assembly:

```text
-runTests -testPlatform editmode -assemblyNames JustSomeStars.EditModeTests
-runTests -testPlatform playmode -assemblyNames JustSomeStars.PlayModeTests -testFilter JustSomeStars.Tests.PlayMode.Task5LaunchIntegrationTests
```

| Report | Required result | Selected/total/passed | Failed | Skipped | Inconclusive |
|---|---:|---:|---:|---:|---:|
| `Builds/TestResults/codemagic-editmode.xml` | `Passed` | `211 / 211 / 211` | `0` | `0` | `0` |
| `Builds/TestResults/codemagic-task5-playmode.xml` | `Passed` | `1 / 1 / 1` | `0` | `0` | `0` |

Each step parses the NUnit XML with Python's standard library and also counts
the concrete `test-case` elements. A nonempty or merely well-formed file is not
enough. Each script has a `test_report` field so Codemagic can expose the XML in
the build overview, following its
[test-report guidance](https://docs.codemagic.io/yaml-testing/testing/).

The EditMode expectation includes the four responsive-layout tests, the
empty-asset-directory test, and the immutable redesign target/prefab contract.
The final local Android-active suite must pass exactly `211 / 211`, but that
local result does not satisfy the remote gate. Do not
complete the Codemagic test gate until a newly generated remote report meets
every count above.

The build process is a separate Unity invocation and does use `-quit`:

```text
-buildTarget Android -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildAndroidInternal
```

`JSS_BUILD_NUMBER` is explicitly unset, so the Android Internal default remains
version code `1`. Release workflows must use a separate monotonically increasing
input and must not reuse this internal job.

## Exact artifact and payload contract

| Evidence | Exact relative path |
|---|---|
| Internal APK | `Builds/AndroidInternal/JustSomeStars-internal.apk` |
| APK digest | `Builds/AndroidInternal/JustSomeStars-internal.apk.sha256` |
| Effective APK manifest | `Builds/AndroidInternal/JustSomeStars-internal.manifest.xml` |
| Compiled Android resources | `Builds/AndroidInternal/JustSomeStars-internal.resources.txt` |
| EditMode XML | `Builds/TestResults/codemagic-editmode.xml` |
| Task 5 PlayMode XML | `Builds/TestResults/codemagic-task5-playmode.xml` |
| EditMode log | `Builds/Logs/codemagic-editmode.log` |
| Task 5 PlayMode log | `Builds/Logs/codemagic-task5-playmode.log` |
| Internal build log | `Builds/Logs/codemagic-android-internal.log` |

The inspected APK must have:

- package ID `com.scientificaj.justsomestars`;
- version name `1.0` and version code `1`;
- a debuggable Android Internal manifest;
- at least one `lib/arm64-v8a/` entry;
- valid, nonempty ZIP content;
- the full canonical 4,469-byte Liberation Sans OFL payload whose source
  SHA-256 is
  `37f8552e9a874ec10710dc0ede6a9adf168e6609fbd02a507f35629373b85a48`;
- the full canonical 11,358-byte Apache License 2.0 payload whose source
  SHA-256 is
  `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30`;
- no `EmojiOne` player name and neither the removed sprite-asset GUID
  `c41005c129ba4d66911b75229fd70b45` nor source-image GUID
  `dffef66376be4fa480fb02b19edbe903`, in text or raw GUID bytes;
- exactly one compiled `UnityPlayerGameActivity` with the pinned launcher,
  configuration, safe-area/notch, freeform, layout and predictive-Back
  contract;
- exactly one canonical AndroidX `InitializationProvider`, no
  `androidx.emoji2.text.EmojiCompatInitializer` anywhere in the effective
  manifest, and exactly one retained
  `androidx.lifecycle.ProcessLifecycleInitializer`.

The workflow saves `unzip -Z1` to
`Builds/AndroidInternal/JustSomeStars-internal.files.txt` and runs the ARM64
`grep` against that completed file. It intentionally does not use an
`unzip | grep -q` pipeline, so `pipefail` cannot turn an early `grep` exit into a
producer-side SIGPIPE failure.

The license inspection compares both repository files against their pinned
lengths/digests and searches every decompressed APK member for each complete
byte sequence. It separately rejects the UTF-8/UTF-16 EmojiOne name plus
textual and binary forms of both EmojiOne GUIDs. `apkanalyzer manifest print`
and `aapt dump resources` are retained as artifacts; the parser resolves
compiled enum and resource-reference forms before enforcing the effective
GameActivity/startup contract. A failed artifact inspection renames the
canonical APK to `.rejected`, preventing the exact artifact declaration from
publishing it.

“Reproduces” means the same source revision, Unity patch/revision, package,
version, variant, tests and canonical artifact path. Unity debug APK bytes and
the machine-local debug certificate do not need to be identical to a local
build.

## External activation ledger

Connection/configuration work is complete. License-dependent rows are
intentionally deferred, not failed or silently waived.

| Gate | Status | Evidence to add after completion |
|---|---|---|
| Push the reviewed Task 5 revision | Complete | Branch `setup/task-0-environment`; Frontend commit `be24595` |
| Connect the GitHub repository to Codemagic | Complete | `ScientificAJ/just-some-stars` application connected |
| Scan and accept `codemagic.yaml` | Complete | Workflow detected from `setup/task-0-environment` |
| Confirm account allowance | Complete | Personal account reports 500 free macOS minutes, 0 used |
| Configure encrypted `jss_unity_ci` variables | Deferred | No Plus/Pro serial exists; never invent values |
| Hydrate and verify every tracked Git LFS object | Deferred | Requires an authorized remote run |
| Confirm a free Unity Plus/Pro seat | Deferred | Account has Unity Personal only |
| Provision the exact editor and Android modules | Deferred | Requires an authorized remote run |
| Start the first remote run | Deferred | User approved deferral; spend no minutes |
| Tests pass with exact expected counts | Deferred | Local evidence does not claim a remote pass |
| Internal APK passes remote inspection | Deferred | Local APK remains the authoritative current artifact |
| Unity seat returns after the run | Not applicable | No seat was activated |
| Clean runner leaves no tracked mutation | Deferred | No runner was allocated |

Do not mark a row complete from a local run. If the account rejects the runner,
schema, provisioned version or licensing method, stop and review the proposed
change rather than weakening the pinned, counted or secret-safe contract.

## Failure and security rules

- Any Git LFS, provisioning, activation, test, XML, build or inspection failure
  blocks the canonical APK.
- Never retain the canonical APK or digest after artifact validation fails.
- Never print credentials, license files, signing material or the environment.
- Do not add release keystores or store publishing to this workflow.
- Do not restore or save `Library/Bee`, `Library/BuildPlayerData`,
  `Library/Il2cppBuildCache`, `Library/PlayerDataCache`, `Temp` or Gradle state
  after credentials enter a future signed job.
- A future signed release job must use an ephemeral workspace, a separate
  encrypted store-specific group, residue scanning and the existing Google or
  Galaxy CLI entry point.
