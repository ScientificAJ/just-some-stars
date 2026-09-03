# Agent environment and ShipKit services

Last verified: 2026-08-25 (2.5D production pivot; Task 0 tool inventory retained)

This file is the source of truth for tools already available to agents working on *Just Some Stars*. Check it before installing duplicates or claiming that a service is unavailable. Keep credentials out of this repository.

## Repository

- Remote: `https://github.com/ScientificAJ/just-some-stars.git`
- Canonical checkout and work directory: `/mnt/unity-data/JustSomeStars`
- Environment branch: `setup/task-0-environment`
- Storage rule: every game/repository file must stay under `/mnt/unity-data/JustSomeStars`, including the Git worktree, Unity project, `Library`, generated or imported assets, Android builds and caches. The system-partition Codex thread workspace is only a lightweight conversation/planning shell, not an active game worktree.
- Data volume: `/dev/sdb1` is an ext4 filesystem mounted at `/mnt/unity-data`; its existing partition and filesystem were expanded in place to 200 GiB on 2026-08-21 and verified with approximately 156 GiB free. No replacement data disk or second game partition was created.
- Safety copy: `/home/john/Documents/Codex/JustSomeStars-pre-resize-backup` is a verified pre-resize copy of the initial 836 KB repository state. It is a backup only, never the active worktree.
- Git: 2.53.0
- Git LFS: 3.7.1, installed on the user PATH and initialized for this checkout
- GitHub CLI: 2.93.0; authenticated as `ScientificAJ` with HTTPS Git operations, verified through `gh api user`

## Core production tools

| Tool | State | Version or path | Agent usage |
|---|---|---|---|
| Unity Editor | Ready | `6000.3.22f1` at `/mnt/unity-data/Unity/Hub/Editor/6000.3.22f1/Editor/Unity`; launcher: `unity-editor` | Use batchmode/CLI for deterministic project creation, tests and Android builds. |
| Unity Android support | Ready | `/mnt/unity-data/Unity/Hub/Editor/6000.3.22f1/Editor/Data/PlaybackEngines/AndroidPlayer` | Bundled OpenJDK 17.0.18, SDK tools 16.0, ADB 36.0.0 and NDK r27c are installed and verified. |
| Host Android SDK | Ready | `/home/john/Android/Sdk` | Host ADB and diagnostics; Unity builds should prefer Unity's bundled toolchain. |
| ADB | Ready | 37.0.1, available as `adb` | Used for Limrun's session-bound Android tunnel when low-level logs, files or shell access are needed. The physical Realme Narzo is intentionally not part of the primary workflow. |
| Blender | Ready, optional after 2.5D pivot | 5.2.0 LTS at `/home/john/.local/bin/blender` | Preserved Task 11 tooling, limited prop/reference work and future experiments; not the shipping character-animation pipeline. |
| Blender MCP | Ready, optional after 2.5D pivot | `blender-mcp`, Python 3.11, `localhost:9876` | Poly Haven enabled; Rodin, Sketchfab and Hunyuan remain disabled. Do not start it for sprite production. |
| Node.js | Ready | 24.19.0 | Required by Limrun and Argent. |
| pnpm | Ready | 11.19.0 | User-level package runner for mobile-agent tools. |

Blender MCP is configured in `/home/john/.codex/config.toml` with telemetry disabled. In Blender, keep the addon port at `9876` and click **Connect to MCP server** before an agent expects live scene control.

On 2026-08-25 the user approved a true layered 2.5D production pivot. The
shipping character pipeline now targets deterministic frame atlases and the
gameplay renderer targets URP 2D. Blender remains installed and Task 11 remains
valid historical tooling, but agents must not resume the unfinished Task 12
Humanoid rig/weight/LOD work unless the user explicitly reverses the pivot.
The active design and restart stage are documented in
`docs/superpowers/specs/2026-08-25-2.5d-gameplay-pivot-design.md`.

## Limrun

Status: **blocked — no remaining credits; do not create an instance**.

- Account authentication is stored locally by the official CLI in `~/.lim/config.yaml`; never print or commit that file.
- CLI: `lim` 0.28.6 on the user PATH.
- Authentication was verified with the read-only `lim android list` command.
- Balance: zero remaining credits. Limrun's organization analytics, queried on
  2026-09-03, report that the original 2,000-credit ShipKit grant was billed in
  full across 67 Android instances between 2026-08-22 and 2026-09-01.
- Metering shown by Limrun: one Android or Xcode minute uses one credit; one iOS minute uses two credits.
- Official project-scoped Codex skills are installed in `.codex/skills/`: `limrun-android-emulator`, `limrun-gradle`, `limrun-maestro-testing`, `limrun-xcode`, `limrun-ios-simulator`, and `limrun-expo-development`.

Do not create a Limrun instance unless legitimate capacity is restored. If it is
restored, use one instance only, preflight the APK and exact test flow locally,
set a short bounded timeout, reinstall corrected APKs onto that same instance
where possible, and explicitly delete and verify the instance in cleanup. Never
use a hard timeout as the normal cleanup mechanism and never overlap instances.

The user selected Limrun instead of the physical Realme Narzo as the primary Android runtime on 2026-08-21. Argent is the preferred control and QA layer over the available Android target; Limrun supplies the cloud device. `lim android list` was verified authenticated with zero running instances, so no credits were being consumed. Do not create an instance before an APK or an immediate device test requires one.

Limrun supplies device creation, APK installation and the ADB tunnel; Argent is
the sole UI discovery/interaction backend during the normal session. After an
instance and tunneled serial exist, begin the repository-owned inspector lease
described in `tools/qa/README.md`. Require that same lease before Argent MCP/CLI
batches, end it during cleanup, then stop the tunnel and delete the paid
instance. Never overlap Limrun UIAutomator/element-tree discovery with Argent.
The guarded `limrun-uiautomator` fallback is allowed only after the Argent lease
has ended.

## Argent

Status: **ready for agent use after the next full Codex desktop restart**.

- CLI: `argent` 0.21.0 on the user PATH.
- `argent tools` successfully exposes 74 device-control, debugging, profiling, recording and QA tools.
- Project MCP configuration is in `.codex/config.toml` and starts `argent mcp`. Because the desktop runtime did not register the project-local server by itself, Argent was also registered in Codex's user MCP configuration with the official `codex mcp add argent -- argent mcp` command on 2026-08-21. `codex mcp list` verifies it as enabled.
- Codex's restricted MCP process could not auto-bind Argent's local tool-server (`listen EPERM 127.0.0.1`). The supported shared server was therefore started on VM-only loopback at `http://127.0.0.1:3001` with `argent server start --detach --force --host 127.0.0.1 --port 3001 --no-auth`, then persisted with `argent link --host 127.0.0.1 --port 3001 --yes`. `argent server status --json` reports healthy and `argent tools` reports 74 tools. After a VM reboot, start the same detached server command if `argent server status` is not healthy.
- Sixteen official Argent skills are installed under `.agents/skills/`, including Android emulator setup, device interaction, screen recording, screenshot diff, repeatable flows and QA flows.
- Argent is free/open tooling. The separate hosted **Argent Cloud** product is still beta and is not required for local or Limrun-backed Android use.

After reopening the workspace, confirm `mcp__argent__*` tools are present. Before the first device action, read the relevant Argent skill, call `list-devices`, and prefer a running Android target. Argent can drive and inspect the Unity APK, but its React Native-only debugger/profiler features do not apply to Unity. Never guess tap coordinates from screenshots; use its discovery tree first.

Direct Argent MCP calls cannot be wrapped as child processes, so run
`device_inspector_session.py require` immediately before each bounded MCP
interaction batch. CLI calls should use its `run` subcommand, which holds the
OS lock for the complete process. The active state contains no credential and
lives only under ignored `Builds/DeviceSessions/`.

## ShipKit and platform availability

| Service | Current state | When an agent should use it |
|---|---|---|
| RevenueCat | Account/project ready; Test Store active | Add Unity SDK, Android apps, products, entitlements and paywall after the Unity project/package IDs exist. |
| Layers | Two months Pro claimed | Complete onboarding once the repository contains the real game site/store context. |
| Junie | 30 credits claimed through September 30 | Install/configure after a compatible JetBrains IDE is installed. No supported JetBrains IDE is present yet. |
| Codemagic | Account/repository/workflow connected; 500 free macOS minutes available | Remote Unity execution is deferred because the account has Unity Personal and no Plus/Pro serial. Revisit only if a valid CI license becomes available. |
| Lance | Organization and credits ready | Preserve for a later iOS/App Store path; not part of the Android launch. |
| Limrun | Authenticated but capacity exhausted; 2,000/2,000 ShipKit credits billed across 67 Android instances; zero active instances | Do not create an instance. Revisit only after legitimate capacity is restored, using the bounded single-instance cleanup contract above. |
| Argent | CLI, MCP declaration and skills configured | Run/inspect/control the Android app and create repeatable QA evidence. |
| Tenjin | Account/company ready | Add the actual app and privacy-gated attribution integration after package/store setup. |
| OneSignal | Not activated | Requires company billing details/payment method; defer unless the user chooses it. |
| Noise | Not claimed | Requires a validation payment and spend; defer. |
| Stripe | Milestone locked | Relevant only after the RevenueCat/Stripe Projects flow and a real purchase; not needed for Android store IAP. |
| Replit / Bitrig | Intentionally skipped | Paid-plan discounts; do not activate unless the user changes scope. |

Additional ShipKit perks unlock through later Shipaton milestone emails. Re-check the Shipaton email after project creation, test purchase, store setup and first real sale; document each newly unlocked service here before use.

## Remaining Task 0 handoffs

- [x] Unity Hub Android Build Support, OpenJDK, SDK/NDK tools and child modules installed successfully; bundled Java, SDK manager, ADB and NDK paths verified.
- [x] Android target decision: use Limrun cloud Android emulators instead of requiring the physical Realme Narzo; no USB passthrough handoff remains.
- [x] GitHub CLI authentication verified for `ScientificAJ`; pushing still requires an explicit user request.
- [x] Argent operational fallback verified: the repository is open from the correct `/mnt/unity-data/JustSomeStars` workspace, its Argent skills are loaded, the linked server is healthy with 74 tools, and `argent run list-devices` succeeds. Direct `mcp__argent__*` exposure remains desirable after a future full desktop restart but is not a Task 0 blocker because the official CLI reaches the same server.
- [x] Expand the existing `/mnt/unity-data` ext4 data volume in place from 100 GiB to 200 GiB; final verification showed a 200 GiB block device, approximately 197 GiB usable, and approximately 156 GiB free.

No Unity project, gameplay code, store listing, purchase catalog or deployment was created in Task 0.
