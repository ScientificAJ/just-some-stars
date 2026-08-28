# Unity Android CLI builds

All local and CI Android builds use the same three Unity entry points. Every
invocation must pass the exact Unity option `-buildTarget Android`; the build
code deliberately refuses to switch targets inside the Editor process.

## Tests

Unity Test Framework `1.6.0` is reliable for a single PlayMode fixture in this
project, but its unfiltered or multi-fixture batch path can discover the full
suite and serialize zero results. The canonical full PlayMode command therefore
uses the repository-owned isolated-fixture runner:

```bash
python3 tools/qa/playmode_suite.py \
  --unity-editor /mnt/unity-data/Unity/Hub/Editor/6000.3.22f1/Editor/Unity \
  --project-path /mnt/unity-data/JustSomeStars
```

Do not retry the invalid unfiltered aggregate or patch Unity's package cache.
The runner verifies the committed manifest against the PlayMode source tree,
uses one Android-active Unity process per fixture, strictly parses every NUnit
XML, and writes an atomic summary. It keeps the graphics device enabled only
for `LayeredCharacterRendererTests`, whose real pixel-readback contract cannot
run on Unity's null graphics device; all other fixtures remain `-nographics`.
Focused development runs may still call one exact fixture or method directly.
Unity test invocations omit `-quit`.

The orchestration contract and device-inspector guard are documented in
`tools/qa/README.md`; their dependency-free tests run with:

```bash
python3 -m unittest discover -s tools/qa/tests -v
```

## Builds

Use Unity `6000.3.22f1` from the canonical project checkout:

```bash
/mnt/unity-data/Unity/Hub/Editor/6000.3.22f1/Editor/Unity \
  -batchmode -nographics -quit -buildTarget Android \
  -projectPath /mnt/unity-data/JustSomeStars \
  -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildAndroidInternal
```

The release entry points are:

```text
JustSomeStars.Editor.Build.BuildCli.BuildGooglePlayRelease
JustSomeStars.Editor.Build.BuildCli.BuildGalaxyRelease
```

## Inputs

`JSS_BUILD_NUMBER` is an unsigned base-10 Android version code in the inclusive
range `1..2100000000`. Internal builds use the deterministic default `1` when it
is unset; release builds require it. Whitespace, signs, non-numeric text,
overflow and out-of-range values fail the invocation.

Google Play release signing uses only:

```text
JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PATH
JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PASSWORD
JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS
JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS_PASSWORD
```

Galaxy release signing uses only:

```text
JSS_GALAXY_ANDROID_KEYSTORE_PATH
JSS_GALAXY_ANDROID_KEYSTORE_PASSWORD
JSS_GALAXY_ANDROID_KEY_ALIAS
JSS_GALAXY_ANDROID_KEY_ALIAS_PASSWORD
```

All four variables for the selected release store are required, and every
variable for the other store must be unset. Internal builds ignore both store
sets and explicitly use Unity debug signing. Supply secrets through the process
environment only. Never put real credentials in this document, command-line
arguments, source control or retained logs.

Both the keystore password and key-alias password must contain at least 12
characters. This minimum also keeps the bounded residue scrub specific enough
to avoid deleting unrelated volatile cache files for trivially short matches.

## Outputs and failure behavior

```text
Builds/AndroidInternal/JustSomeStars-internal.apk
Builds/GooglePlay/JustSomeStars-google-play.aab
Builds/Galaxy/JustSomeStars-galaxy.aab
```

Unity may also create the ignored internal-development symbol directory
`Builds/AndroidInternal/JustSomeStars_BurstDebugInformation_DoNotShip/`. Keep it
for local debugging when useful, but never upload it to a store or interpret it
as build success. Automation publishes and retains only the exact APK/AAB paths
listed above.

Each build writes a deterministic same-directory staging artifact whose name
ends in the real package extension, for example
`JustSomeStars-internal.jss-staging.apk`. The canonical and staging paths are
invalidated at invocation start. Publication is a same-filesystem rename and
occurs only after Addressables, the player report, artifact validation, scene
cleanup, player-setting restoration, signing restoration and signing-residue
cleanup all succeed. Every failed invocation leaves neither path behind.

Variant symbols are invocation-local through
`BuildPlayerOptions.extraScriptingDefines`; do not persist `JSS_DEVELOPMENT`,
`JSS_GOOGLE_PLAY` or `JSS_GALAXY` in Android PlayerSettings. Addressables is
built explicitly with the same variant symbols and remains configured as
`DoNotBuildWithPlayer`.

## CI workspace and cache rule

Release jobs must use an ephemeral checkout/workspace and destroy it after the
job. Do not persist or restore secret-bearing Unity/Gradle build state from a
release job, especially `Library/Bee`, `Library/BuildPlayerData`,
`Library/Il2cppBuildCache`, `Library/PlayerDataCache` or `Temp`. General Unity
dependency caches may be restored only before credentials enter the process and
must never be saved after a signed build. After Unity exits, CI must scan the
entire workspace and retained logs for injected sentinel credentials; any match
fails the job and forbids artifact retention.
