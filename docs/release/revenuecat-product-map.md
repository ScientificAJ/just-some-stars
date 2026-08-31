# RevenueCat product and entitlement map

Status: local Task 23 commerce boundary complete; dashboard/store activation
is deferred to JSS-023.

## Player promise

Chapter One, its science, accessibility options and story completion are free.
Every mapped purchase is an optional, one-time, non-consumable cosmetic or
edition entitlement. The game has no advertisements, premium currency, random
loot, paid power, energy, story gate or science gate.

The client never invents a title, description or price. Those values come from
the active store product returned by RevenueCat. A purchase callback, receipt,
button state or locally cached product does not grant content. Only an active,
allowlisted entitlement in verified RevenueCat CustomerInfo can grant it.

## Stable launch mapping

RevenueCat offering: `launch_cosmetics`

| Local content ID | Android store product | RevenueCat package | RevenueCat entitlement |
|---|---|---|---|
| `store.explorer-edition` | `jss.edition.explorer` | `explorer_edition_package` | `explorer_edition` |
| `store.founders-constellation` | `jss.pack.founders_constellation` | `founders_constellation_package` | `founders_constellation` |
| `store.complete-launch-collection` | `jss.pack.complete_launch_collection` | `complete_launch_collection_package` | `complete_launch_collection` |
| `store.mirra-collection` | `jss.collection.mirra` | `mirra_collection_package` | `mirra_collection` |
| `store.koro-vesper-collection` | `jss.collection.koro_vesper` | `koro_vesper_collection_package` | `koro_vesper_collection` |
| `store.aster-veil-collection` | `jss.collection.aster_veil` | `aster_veil_collection_package` | `aster_veil_collection` |

These identifiers are reserved in code. They are not claimed as remotely
configured until JSS-023 records dashboard and licensed-store evidence.
Unknown offerings, packages, products and entitlements are hidden and cannot
grant ownership.

## Identity and restore rules

- A guest starts with RevenueCat's anonymous App User ID; the client never
  fabricates one.
- After Firebase login, the exact validated Firebase UID is passed directly to
  RevenueCat `LogIn`. UID-to-UID transitions also use direct `LogIn` so an
  unnecessary anonymous identity is not created between accounts.
- Sign-out calls RevenueCat `LogOut` and receives a new anonymous identity.
- Identity transition immediately hides the former identity's entitlements.
  Late callbacks from the former generation cannot publish.
- Restore is an explicit grown-up-confirmed action. Startup, opening the shop
  and app resume never call Restore automatically.
- A pending, ambiguous or interrupted purchase sets an identity-bound refresh
  marker. Resume coalesces that marker into one CustomerInfo refresh and never
  guesses ownership.
- The RevenueCat dashboard restore behavior must be reviewed and tested before
  activation. The intended behavior is transfer to the current App User ID,
  with the exact dashboard choice and cross-profile result recorded in
  JSS-023.

## Offline ownership

The cache stores only an exact verified entitlement snapshot. It is bound to
RevenueCat identity, SDK-key fingerprint, environment and Android package.
A newer verified snapshot replaces the old set, including verified-empty
revocation. Transient network failure preserves the last matching verified
set; corrupt, future-schema or cross-identity data fails closed. Paid
entitlements never enter the cloud-unioned `GameSave.EarnedCosmeticIds` field.

## Build variants and keys

| Variant | Commerce behavior | Required input |
|---|---|---|
| Android Internal | RevenueCat Test Store only when explicitly enabled; otherwise optional store unavailable | optional `JSS_REVENUECAT_TEST_STORE_API_KEY` with `test_` prefix |
| Google Play | RevenueCat Google Play adapter and Google Play Billing | required `JSS_REVENUECAT_GOOGLE_API_KEY` with `goog_` prefix plus release signing inputs |
| Galaxy | Samsung IAP 6.5.2 through the isolated Task 24 adapter; no RevenueCat or Google BillingClient | both RevenueCat Test Store and Google keys forbidden |

The key-specific JSON exists only in ignored
`Assets/_JustSomeStars/GeneratedCommerce/` during the player build. The build
lease removes stale input before use, imports the exact temporary resource,
and removes the resource, metas and containing generated directory on success
or failure before publishing an artifact. Public SDK keys are never committed
or printed.

## SDK and local evidence

- Official RevenueCat Unity SDK `9.9.1`, upstream revision
  `a8e805ecde13f2fc9aad711ea64bd7ede9deafed`.
- Vendored official OpenUPM archive: 67,939 bytes, SHA-256
  `6014a539443b8c2f2c1baf834476957ee891e06730781a1574efbffd2333d313`.
- Verified-entitlement/cache tests: `6/6`.
- Store and grown-up flow tests: `13/13`, including identity-transition,
  delayed-callback and post-native-launch cancellation reconciliation.
- Build-key/manifest/variant tests: `4/4`; the affected build configuration
  and orchestrator integration filter passes `35/35`.
- Bootstrap integration tests: `10/10`; commerce is optional and an absent
  configuration cannot block Frontend or story startup.
- No-key internal APK: SHA-256
  `c3fc001c7c906019be818611b3d92bdc65a98208048d34171396e842af591c15`,
  351,906,210 bytes. It contains `com.android.vending.BILLING`, the RevenueCat
  Android assembly/dependency path, package `com.scientificaj.justsomestars`,
  min SDK 25, target SDK 36, ARM64 and a valid debug v2 signature. It contains
  no generated RevenueCat configuration, Test Store key or Google key.
- The final merged Android launcher keeps `UnityPlayerGameActivity` at
  `singleTop`, as required by RevenueCat's purchase-resume flow. The generated
  Gradle-project processor verifies that mode and idempotently excludes the
  Google RevenueCat/Billing dependency path from Galaxy builds; the fixture
  contract rejects stale or duplicate isolation markers.

This APK proves clean local Android integration, not a purchase. Test Store
transaction, Google product creation, signed AAB upload, licensed purchase,
RevenueCat customer/transaction/entitlement confirmation and restore proof
remain JSS-023. Task 28 owns the final localized, responsive shop view; Task 23
provides its tested controller, live metadata, transparent copy, grown-up gate
and explicit Restore behavior.
