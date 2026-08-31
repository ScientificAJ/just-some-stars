# Firebase Unity package provenance

Verified: 2026-08-31

These archives are stored byte-for-byte as downloaded from Google's official
Unity package registry. No extraction, repacking or content transformation is
performed. The repository path is the official filename under
`Packages/FirebasePackages/`.

| Repository archive | Official source | Bytes | SHA-256 |
|---|---|---:|---|
| `com.google.external-dependency-manager-1.2.186.tgz` | `https://dl.google.com/games/registry/unity/com.google.external-dependency-manager/com.google.external-dependency-manager-1.2.186.tgz` | 420750 | `46684b475c2a39844c44c07945b5aee02895c41a9bff97d5cd4b5d9e85e021d8` |
| `com.google.firebase.app-13.16.0.tgz` | `https://dl.google.com/games/registry/unity/com.google.firebase.app/com.google.firebase.app-13.16.0.tgz` | 61721869 | `691f7ef26d080de43a011ce7846567fa72ceede5bdf4917edc0dc7a715c38dd4` |
| `com.google.firebase.auth-13.16.0.tgz` | `https://dl.google.com/games/registry/unity/com.google.firebase.auth/com.google.firebase.auth-13.16.0.tgz` | 2650552 | `5718553c264ab8a971f7ee12628b19f4e767c7156fe7a80d0107c2e0859229e4` |
| `com.google.firebase.firestore-13.16.0.tgz` | `https://dl.google.com/games/registry/unity/com.google.firebase.firestore/com.google.firebase.firestore-13.16.0.tgz` | 13137768 | `d5613461ac91b1cd01a18de31e5647cca1c647e57e8de8eefd2a05d6fbf49db1` |

Authority:

- Firebase Unity SDK release: `https://github.com/firebase/firebase-unity-sdk/releases/tag/v13.16.0`
- EDM4U release: `https://github.com/googlesamples/unity-jar-resolver/releases/tag/v1.2.186`
- Google Unity package archive: `https://developers.google.com/unity/archive`

Verification procedure:

1. Download each official URL to a separate temporary directory on the data
   volume.
2. Compare its byte count and SHA-256 with the repository archive.
3. Require byte-for-byte equality (`cmp`) before accepting the package update.
4. Run the focused Unity package/privacy tests and rebuild the Android artifact.

The official downloads independently obtained on 2026-08-31 were byte-for-byte
identical to all four repository archives.
