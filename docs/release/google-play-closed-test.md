# Google Play closed-test runway

Status as of 2026-08-22: **prepared and explicitly deferred to the Growth and
release package (Tasks 31–33), when a playable release candidate exists**.

This is a redacted operational ledger, not proof of a Play Console app, upload,
published closed release, tester opt-in, or elapsed testing period. No Google
Play mutation, signed release build, tester enrollment, or credential use was
performed while this file was created.

## Confirmed repository contract

| Item | Confirmed value |
|---|---|
| Product | `Just Some Stars` |
| Google package ID | `com.scientificaj.justsomestars` |
| Unity release entry point | `JustSomeStars.Editor.Build.BuildCli.BuildGooglePlayRelease` |
| Expected AAB | `Builds/GooglePlay/JustSomeStars-google-play.aab` |
| Current version name | `1.0` |
| Version-code input | `JSS_BUILD_NUMBER`, required and monotonically increasing |
| Build variant | `JSS_GOOGLE_PLAY`, invocation-local |

The internal artifact
`Builds/AndroidInternal/JustSomeStars-internal.apk` is a debuggable device-test
APK. It must never be uploaded as the Google closed-test artifact.

The Task 5 `codemagic.yaml` workflow also produces only that internal debug APK.
Its remote Unity run is deliberately deferred because the account has Unity
Personal and no Plus/Pro serial; even a future successful internal CI run would
not prove that a signed Google AAB exists or that Play Console accepted it.
`UNITY_SERIAL`, `UNITY_EMAIL` and `UNITY_PASSWORD` are CI license inputs, not
Google signing credentials, and must remain separated from the four variables
below.

No internal or future signed CI artifact is valid evidence if a tracked Git LFS
path remained pointer text. The pre-activation workflow must enumerate and
hydrate every indexed LFS object, match its size and SHA-256, and verify that the
retained `Assets/TextMesh Pro/Sprites/EmojiOne.png` is a genuine PNG. This
repository-integrity check does not put EmojiOne into the player and does not
substitute for signed-AAB inspection. Remote LFS authentication and hydration
are deferred with the Codemagic run.

## Decisions and prerequisites for Task 33

The authorized account owner must resolve these before a signed build or console
mutation:

- Which Play developer account owns the app and whether its current account
  type is subject to a closed-testing production-access requirement.
- Google Play App Signing participation and long-term upload-key custody.
- The next unused Android version code.
- A reviewed public HTTPS privacy-policy URL and matching in-app/store copy.
- Developer contact details, target-audience/family declarations, data-safety
  answers, ads declaration, content rating, and other required listing forms.
- Who coordinates legitimate testers and keeps personally identifying tester
  data outside Git.

Testing thresholds and eligibility rules can change. Re-check the current
official Play Console guidance at the authorized execution gate; do not infer
account applicability from this planning document alone.

## Signed-build gate

The four Google-specific signing variables are:

```text
JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PATH
JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PASSWORD
JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS
JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS_PASSWORD
```

Provide them only through the approved encrypted process environment. Never
write values into this repository or a retained command line. With the approved
unique `JSS_BUILD_NUMBER` already present securely, the canonical invocation is:

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -buildTarget Android \
  -projectPath "$PWD" \
  -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildGooglePlayRelease
```

Before upload, retain only non-secret evidence:

- exact commit SHA;
- version name and version code;
- AAB byte size and SHA-256;
- package ID and non-debug inspection result;
- upload-certificate fingerprint when the owner approves recording it;
- clean build exit and redacted log paths.

Do not retain keystore paths, aliases, passwords, private certificate material,
Play credentials, receipt data, or complete console exports.

## Deferred console and closed-test ledger

Every row below is intentionally deferred until the playable release-candidate
gate. A `Pending` row is not a current Release Runway failure.

| Gate | Status | Redacted evidence |
|---|---|---|
| Developer-account owner confirmed | Pending | None recorded |
| Account-specific testing rule verified | Pending | Applicability unknown |
| App Signing/upload-key decision approved | Pending | None recorded |
| Play app record created | Pending | No application record claimed |
| Store declarations completed | Pending | No declarations claimed |
| Google AAB built and inspected | Pending | No signed AAB claimed |
| AAB uploaded to closed track | Pending | No upload claimed |
| Closed release reviewed and published | Pending | No published release claimed |
| Tester invitation group configured | Pending | No tester list in Git |
| Required opted-in count reached | Pending | Count not recorded |
| Continuous-testing clock started | Pending | Start time not recorded |
| Continuous-testing requirement completed | Pending | Not claimed |

## Tester and clock semantics

If the authorized account is confirmed to be subject to the plan's qualifying
new-personal-account rule, use at least 12 legitimate testers or the greater
current console requirement. Merely adding an address to a tester list does not
make that person opted in.

The clock can be recorded as started only after all of the following are true:

1. The closed-track release is published and available to testers.
2. Each counted tester has completed the opt-in flow.
3. The applicable minimum opted-in count is simultaneously present.

The count must remain continuously at or above the applicable minimum for the
required duration. If it drops below the threshold, record the console-observed
effect truthfully; do not preserve an earlier local timestamp as though the
requirement continued uninterrupted.

Keep tester email addresses, names, invitation links, and screenshots containing
personal data in an owner-controlled private location. This repository may
record only aggregate counts, UTC timestamps, and redacted/pseudonymous evidence
references.

## Aggregate observation log

Add rows only from verified console observations. Do not backfill estimates.

| Observed at (UTC) | Release status | Opted-in count | Required count | Continuous since (UTC) | Redacted evidence reference |
|---|---:|---:|---:|---|---|
| Pending | Not observed | Not observed | Not yet verified | Not started | None |
