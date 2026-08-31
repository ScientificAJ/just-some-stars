# Task 24 Galaxy commerce review

Status: **local checkpoint complete; external activation pending JSS-024**.

## Local implementation

- Galaxy builds use package `com.scientificaj.justsomestars.galaxy` and the
  invocation-local `JSS_GALAXY` symbol.
- The generated Galaxy Gradle project removes RevenueCat and Google
  BillingClient, stages only `jssGalaxyBilling`, and places that module inside
  the real `dependencies` block. Google builds reject Samsung module state.
- The Galaxy module pins public Maven coordinate
  `com.samsung.developer:iap:6.5.2`; the repository does not redistribute the
  Samsung binary or claim license acceptance.
- Runtime installation is Galaxy-only. The Java gateway configures
  `PRODUCTION`; release builds reject Samsung TEST/TEST_FAILURE symbols.
- Payment callbacks and owned rows cannot grant. A trusted verifier must issue
  an exact package/item/mode/identity-bound signed authority first.
- Signed authorities, interrupted item IDs and acknowledgement retries use an
  atomic on-device ledger. Cached authorities are revalidated before use;
  `GetOwnedList` reconciles only active-identity known/pending purchases unless
  a grown-up explicitly chooses Restore.
- All six Seller products are permanent Items. The adapter verifies, persists,
  grants and then calls `AcknowledgePurchases`; it never consumes them and checks
  each acknowledgement result.

## Focused evidence

| Gate | Result |
|---|---|
| Authentic runtime/recovery RED | `task24-galaxy-runtime-recovery-red.xml`: `6/10`, exactly four intended failures |
| Source-final Galaxy isolation/runtime | `task24-galaxy-runtime-source-final-green.xml`: `10/10`, exit `0` |
| Affected commerce build configuration | `task24-commerce-build-config-post-runtime-green.xml`: `4/4`, exit `0` |
| Extreme bounded critic | `PROCEED` after the four correction targets were rechecked |

The REDs cover missing provider wiring, absent cached/pending restart recovery,
unused production-mode policy and incorrect Gradle dependency placement. The
final run additionally executes an actual atomic ledger close/reopen and
acknowledgement-retry lifecycle.

## External gates not claimed

- Samsung legal/Seller prerequisites and six live Item records;
- a deployed authenticated receipt verifier and privacy review;
- a signed Galaxy AAB whose resolved dependency graph is inspected;
- TEST/TEST_FAILURE evidence and a closed-beta PRODUCTION purchase, restore,
  interruption, background/resume and acknowledgement retry on a licensed
  physical Samsung device.

An emulator, fake callback or local source test is not transaction evidence.

Primary integration references:

- <https://developer.samsung.com/iap/samsung-iap-unity-plugin.html>
- <https://developer.samsung.com/iap/programming-guide/iap-helper-programming.html>
- <https://developer.samsung.com/iap/programming-guide/samsung-iap-server-api.html>
