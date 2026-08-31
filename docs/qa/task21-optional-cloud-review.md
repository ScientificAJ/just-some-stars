# Task 21 optional account and cloud review

Date: 2026-08-31

## Local completion boundary

Task 21 completes the locally executable account/cloud foundation without
pretending that external credentials or a deployed Firebase project exist.
The checked-in Android build remains guest-first and fully playable offline.
Its production composition deliberately injects unavailable Firebase gateways,
so the Frontend truthfully says that Google backup is unavailable.

The external activation gate is JSS-021. It owns Firebase project registration,
both Android client records and signing fingerprints, ignored client
configuration, a maintained Google ID-token provider, concrete Firebase SDK
gateway wiring, rules/index deployment and real two-device credentialed proof.
Adding `google-services.json` alone does not activate the current build, and no
admin credential, client secret, signing material or account token is in Git.

## Implemented local contract

- A random 128-bit guest identity is created atomically and survives restart.
- Local play initializes successfully even when Firebase is absent or offline.
- Account linking is modeled as an explicit operation that merges the guest and
  cloud saves, preserves the guest ID and commits both copies.
- Compatible story progress keeps the furthest valid checkpoint. When story or
  mission data genuinely conflicts, the selected device/cloud branch supplies
  both story and mission atomically, while discovery, cosmetic and Atlas IDs
  still union, the newest compatible Captain customization wins, birthday
  claim history merges, and local photographs remain local.
- Photos and device settings remain local. Genuine incompatible saves require
  `Use this device` or `Use cloud` rather than silent overwrite.
- Upload is conditional on the observed remote version. A competing write
  leaves the local copy safe and exposes a retryable state.
- Sign-out never deletes local progress. Export, Google unlink and complete
  cloud/account deletion are separate explicit operations; deleting cloud data
  still preserves the device save.
- Cloud documents are bounded to `users/{uid}` and validated against the same
  IDs/counts/schema expected by the rules. Firestore rules deny collection
  listing, cross-user access, unknown fields, photos, settings, revision reuse
  and creation-time rewriting.
- A local checkpoint is committed before any optional linked-account sync.
  Interrupted or failed cloud work never rolls back the device save or escapes
  the checkpoint boundary.
- Settings owns eight truthful account controls and state copy. Delete
  confirmation is disarmed whenever the panel closes, and pending/conflict
  states expose only operations that are valid for an authenticated session.
  Cancellation and recoverable post-auth failures restore an honest idle state
  that still reflects any persisted Firebase UID.
  The old
  `NO ONLINE SERVICES` claim is rejected by asset, controller and launch tests.

## Package and privacy provenance

The repository pins official local Unity packages and their exact SHA-256:

| Package | Version | SHA-256 |
|---|---:|---|
| External Dependency Manager | 1.2.186 | `46684b475c2a39844c44c07945b5aee02895c41a9bff97d5cd4b5d9e85e021d8` |
| Firebase App | 13.16.0 | `691f7ef26d080de43a011ce7846567fa72ceede5bdf4917edc0dc7a715c38dd4` |
| Firebase Auth | 13.16.0 | `5718553c264ab8a971f7ee12628b19f4e767c7156fe7a80d0107c2e0859229e4` |
| Cloud Firestore | 13.16.0 | `d5613461ac91b1cd01a18de31e5647cca1c647e57e8de8eefd2a05d6fbf49db1` |

`Packages/FirebasePackages/UPSTREAM_PROVENANCE.md` records the exact official
Google download URL, byte count and repository mapping for every archive. A
fresh 2026-08-31 download of all four archives was byte-for-byte identical to
the checked-in files. The release authorities are Firebase Unity SDK
`v13.16.0`, EDM4U `v1.2.186` and Google's Unity package archive.

External Dependency Manager records the Android dependency set in
`ProjectSettings/AndroidResolverDependencies.xml` and regenerates the local
Maven repository and Gradle templates from the pinned packages. Those derived
`Assets/GeneratedLocalRepo` and template files are ignored rather than shipped
as a second source of truth.

The generated Gradle project excludes `firebase-analytics`. The final APK
manifest contains no `AD_ID`, either AdServices permission, Analytics
connector, AppMeasurement receiver/service/job service, or EmojiCompat startup
initializer. It retains Firebase Auth, Firestore and Android lifecycle startup.
The artifact dependency-insight query found no Firebase Analytics dependency.
Android backup is disabled at the application boundary, and both Android 12+
data-extraction rules and the legacy full-backup policy explicitly exclude all
device-local files, databases, shared preferences, external files and device-
protected storage.

## Focused verification

| Gate | Result |
|---|---:|
| Cloud save merge and conditional-version contract | `7/7` |
| Firebase package provenance, manifest and backup privacy | `5/5` |
| Frontend account bindings | `1/1` |
| Frontend mobile target inventory | `1/1` |
| Account link, conflict, sync, unlink and destructive lifecycle | `13/13` |
| Frontend controller | `11/11` |
| Frontend dependency injection and checkpoint sync | `5/5` |
| Bootstrap installer | `10/10` |
| Real Boot launch integration | `1/1` |
| Firestore Emulator rules | `4/4` |

Only affected fixtures and exact asset methods were rerun. The unrelated
pre-existing empty `Assets/Resources/` assertion is not represented as a Task
21 failure or as part of this focused gate.

## Android artifact

- Path: `Builds/AndroidInternal/JustSomeStars-internal.apk`
- Size: `284,132,467` bytes
- SHA-256: `3fe9fdebfb4d4b14f9fe243d57fde495fdfbc8a62776b5635cf3f14732515ef9`
- Package: `com.scientificaj.justsomestars`, version `1.0` / code `1`
- Android: minimum 25, target 36, ARM64 only
- Signing: valid APK Signature Scheme v2, one Android Debug signer
- Build: Unity `6000.3.22f1`, Android internal, `Succeeded`, two warnings,
  zero errors

## Exact-device no-configuration evidence

One replacement Limrun Android instance installed the exact post-critic APK
above. The device itself computed the same SHA-256 from its installed
`base.apk`. Device metrics were 720 x 1616 at 280 dpi with font scale 1.0; the
Unity surface settled at 1616 x 720. A force-stop and absent PID plus cleared
logcat preceded the launch, proving a cold process start. Home/background and
foreground resume kept the same PID `17054`. Root Back returned to the launcher
and removed PID `17054`, then Unity completed
`onDestroy`, `DestroyInstance`, `FinalDestroyInstance`, reported no leftover
JNI references and exited the process.

The app-PID log contains no Firebase Auth, Firestore, Analytics, measurement,
AD_ID, fatal or managed-exception match. `FirebaseInitProvider` truthfully
reports that default options are absent and FirebaseApp initialization is
unavailable. An unrelated `com.argent.androiddevtools` watchdog exception was
recorded only after the inspector disconnected; it is not an app-process
failure. This is deliberately a no-config startup/lifecycle proof, not a
cloud-link or second-device claim. The internal build routes to the Task 17
flight proof, so Frontend cloud controls are covered by the focused Unity
asset/controller/integration tests rather than fabricated device interaction.

The exact sanitized transcript and screenshots are retained under
`Builds/DeviceEvidence/task21-final-postcritic/`. The Task 21 upload, inspector
lease, ADB tunnel and paid instance were removed; the locally started Argent
server was stopped, and the final Limrun instance list was empty.

## Final critic correction provenance

The final bounded critic identified conflict-selection and error-recovery
defects that were reproduced before correction. The focused account RED was
`9/13`; a narrower branch-pair RED was `1/2`. The corrected account suite is
`13/13` in `task21-final-critic-account-final-green.xml`: player choice now
selects the story/mission branch without discarding safe mergeable data,
recoverable post-auth failures expose persisted identity truthfully, and an
optional cloud failure cannot escape after the local checkpoint is durable.
The same critic's final verdict was `PROCEED`.
