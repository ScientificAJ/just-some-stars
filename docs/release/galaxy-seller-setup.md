# Galaxy Seller setup runway

Status as of 2026-08-22: **prepared and explicitly deferred to the Growth and
release package (Tasks 31–33), when a playable release candidate exists**.

No Galaxy Store Seller Portal app record, commercial-seller enrollment, signed
Galaxy build, upload, review, or IAP setup was created or claimed while this
file was written.

## Confirmed repository contract

| Item | Confirmed value |
|---|---|
| Product | `Just Some Stars` |
| Galaxy package ID | `com.scientificaj.justsomestars.galaxy` |
| Unity release entry point | `JustSomeStars.Editor.Build.BuildCli.BuildGalaxyRelease` |
| Expected AAB | `Builds/Galaxy/JustSomeStars-galaxy.aab` |
| Current version name | `1.0` |
| Version-code input | `JSS_BUILD_NUMBER`, required and monotonically increasing |
| Build variant | `JSS_GALAXY`, invocation-local |

Task 5 creates only the truthful Development Flight and this setup ledger. It
does not implement Galaxy billing. The native RevenueCat Galaxy bridge and its
physical licensed-Samsung-device verification belong to Task 24.

The Task 5 `codemagic.yaml` workflow creates only the internal debug APK; its
remote Unity run is deliberately deferred because the account has Unity
Personal and no Plus/Pro serial. It does not build, sign, upload or validate the
Galaxy AAB. The CI-only
`UNITY_SERIAL`, `UNITY_EMAIL` and `UNITY_PASSWORD` values must not be treated as
Galaxy signing or Seller Portal credentials.

A future CI artifact is invalid if any tracked Git LFS path remains pointer
text. Before Unity activation, the internal workflow must enumerate and hydrate
all indexed LFS objects, match their size and SHA-256, and prove that retained
`Assets/TextMesh Pro/Sprites/EmojiOne.png` is a genuine PNG. This source-integrity
gate does not include EmojiOne in the player and is not evidence of a signed or
Seller-accepted Galaxy AAB. Remote LFS authentication and hydration are
deferred with that run.

## Seller and application record for Task 33

| Field | Current state |
|---|---|
| Samsung account owner | Pending owner confirmation |
| Seller Portal seller status | Not verified |
| Commercial-seller status | Not verified |
| Tax/payout prerequisites | Not verified; keep legal and banking data out of Git |
| Application record | Not created or not yet evidenced |
| Seller Portal application ID | Pending; record only if safe and non-secret |
| Package ID reserved in record | Pending |
| Distribution countries/regions | Pending owner decision |
| Target audience/content rating/privacy forms | Pending review |

Never infer approval from account registration alone. Record only redacted status
and public application identifiers; do not commit names, addresses, tax IDs,
bank details, authentication data, or complete portal exports.

## Signing decision

Planned direction, subject to explicit owner and current Seller Portal
confirmation: use Seller Portal-managed signing for the submitted AAB while the
owner retains the corresponding upload-key material required by the build
pipeline. This is not marked selected or enabled yet.

The Galaxy build currently requires these store-specific encrypted variables:

```text
JSS_GALAXY_ANDROID_KEYSTORE_PATH
JSS_GALAXY_ANDROID_KEYSTORE_PASSWORD
JSS_GALAXY_ANDROID_KEY_ALIAS
JSS_GALAXY_ANDROID_KEY_ALIAS_PASSWORD
```

Google signing variables must be absent from a Galaxy release-build process.
Never store signing values, keys, aliases, passwords, or private certificate
material in this repository. Once custody is approved and the next unique
`JSS_BUILD_NUMBER` is present securely, the canonical build invocation is:

```bash
"$JSS_UNITY_EDITOR" -batchmode -nographics -quit -buildTarget Android \
  -projectPath "$PWD" \
  -executeMethod JustSomeStars.Editor.Build.BuildCli.BuildGalaxyRelease
```

## Missing commercial and IAP prerequisites

The following remain intentionally unresolved in Task 5:

- verified commercial-seller enrollment, tax, payout, and required business
  details;
- a Seller Portal application record bound to the exact `.galaxy` package;
- approved signing/upload-key custody and recovery procedure;
- Galaxy product IDs and their shared entitlement mappings;
- the native RevenueCat Galaxy module or separately approved Samsung Unity IAP
  fallback;
- licensed-tester accounts and a physical Samsung test device;
- success, cancellation, interruption, restore, offline-cache, and forced-store-
  unavailable evidence;
- reviewed privacy, target-audience, content-rating, support, and store-listing
  declarations.

The free story must remain usable when Galaxy commerce is unavailable. An
emulator result is never evidence that Galaxy commerce works; the technical plan
requires licensed physical-device verification later.

## Deferred external activation ledger

Every row below is intentionally deferred until the playable release-candidate
gate. A `Pending` row is not a current Release Runway failure.

| Gate | Status | Redacted evidence to add after completion |
|---|---|---|
| Seller/account owner confirmed | Pending | Owner-controlled reference only |
| Commercial prerequisites reviewed | Pending | Status only; no legal/banking data |
| Signing choice and upload-key custody approved | Pending | Public fingerprint only if approved |
| Application record created | Pending | Safe application ID and UTC timestamp |
| Exact package ID registered | Pending | Redacted portal observation |
| Galaxy AAB built and inspected | Pending | Commit, version, size, SHA-256, package, non-debug state |
| AAB uploaded | Pending | Redacted submission ID and UTC timestamp |
| Store review/submission started | Pending | Redacted status only |
| Galaxy IAP prerequisites complete | Pending for Task 24 | Task 24 evidence reference |

Creating or changing any row's external state requires explicit authorization.
A locally written timestamp, planned identifier, or successful non-Galaxy APK
run is not Seller Portal evidence.
