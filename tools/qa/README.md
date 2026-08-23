# Just Some Stars QA orchestration

These dependency-free Python tools close two Unity/device failure modes without
patching Unity packages or spending a paid emulator session.

## Complete PlayMode suite

Unity Test Framework 1.6 can enter PlayMode for the unfiltered project assembly
yet serialize `90 discovered / 0 recorded`. Run the committed fixture manifest
instead:

```bash
python3 tools/qa/playmode_suite.py \
  --unity-editor /mnt/unity-data/Unity/Hub/Editor/6000.3.22f1/Editor/Unity \
  --project-path /mnt/unity-data/JustSomeStars
```

The runner:

- rejects an unsorted, duplicate, stale, or incomplete fixture manifest;
- discovers every convention-bound `*Tests.cs` fixture from the PlayMode source
  tree and requires an exact manifest match;
- starts one Android-active Unity process per fixture, without `-quit`;
- deletes each old report and log before its invocation;
- requires a nonzero, clean, exact-fixture NUnit result; and
- writes `Builds/TestResults/playmode-suite/summary.json` atomically.

The default is fail-fast. `--keep-going` is available for a deliberate
diagnostic pass, while still producing a failed summary if any fixture fails.

## One device inspector at a time

Limrun supplies the paid emulator, APK installation, and ADB tunnel. Argent is
the preferred and sole UI inspector during normal QA. Begin an explicit lease
after the instance and tunneled serial are known:

```bash
python3 tools/qa/device_inspector_session.py begin \
  --backend argent \
  --instance-id "$JSS_LIMRUN_INSTANCE_ID" \
  --serial "$JSS_ANDROID_SERIAL"
```

Before an Argent MCP/CLI interaction batch, fail closed if the lease does not
match:

```bash
python3 tools/qa/device_inspector_session.py require \
  --backend argent \
  --instance-id "$JSS_LIMRUN_INSTANCE_ID" \
  --serial "$JSS_ANDROID_SERIAL"
```

CLI interaction can be held under the same OS lock for its entire process:

```bash
python3 tools/qa/device_inspector_session.py run \
  --backend argent \
  --instance-id "$JSS_LIMRUN_INSTANCE_ID" \
  --serial "$JSS_ANDROID_SERIAL" \
  -- argent run list-devices
```

End the lease before stopping the tunnel and deleting the Limrun instance:

```bash
python3 tools/qa/device_inspector_session.py end \
  --backend argent \
  --instance-id "$JSS_LIMRUN_INSTANCE_ID" \
  --serial "$JSS_ANDROID_SERIAL"
```

While an Argent lease is active, do not call Limrun element-tree or other
UIAutomator-backed inspection directly. A deliberate Limrun UIAutomator
fallback must first end the Argent lease, begin a `limrun-uiautomator` lease,
and route the supported `lim` inspector command through `run`. The state lives
under ignored `Builds/DeviceSessions/`; it contains only backend and device
identity, never Limrun credentials.

## Tool tests

```bash
python3 -m unittest discover -s tools/qa/tests -v
```
