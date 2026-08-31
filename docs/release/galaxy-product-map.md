# Galaxy commerce product map

Status: **local Samsung fallback implemented; transaction activation PENDING**.

The Galaxy package is `com.scientificaj.justsomestars.galaxy`. Galaxy commerce
uses **Samsung Unity IAP 6.5.2**, not RevenueCat. The official Samsung binary is
governed by the Samsung IAP License Agreement; the repository currently stages
only reproducible Gradle acquisition metadata and does not redistribute the SDK
binary.

## Products

| Local content ID | Seller Item ID | Internal entitlement |
|---|---|---|
| `store.explorer-edition` | `jss.edition.explorer` | `explorer_edition` |
| `store.founders-constellation` | `jss.pack.founders_constellation` | `founders_constellation` |
| `store.complete-launch-collection` | `jss.pack.complete_launch_collection` | `complete_launch_collection` |
| `store.mirra-collection` | `jss.collection.mirra` | `mirra_collection` |
| `store.koro-vesper-collection` | `jss.collection.koro_vesper` | `koro_vesper_collection` |
| `store.aster-veil-collection` | `jss.collection.aster_veil` | `aster_veil_collection` |

All six Seller products are permanent, non-repurchaseable `Item` products.
After trusted verification and durable entitlement persistence, the client uses
`AcknowledgePurchases`. It must **never ConsumePurchasedItems** for these Items.
Unknown products and incomplete store metadata are hidden.

## Authority and ordering

The Samsung payment callback and `GetOwnedList` entries are untrusted input.
The release flow is:

1. persist an identity-bound pending marker;
2. open Samsung payment with domain-separated obfuscated account/profile IDs;
3. send the purchase ID to the trusted receipt verifier;
4. require success, exact package, allowlisted item, `PRODUCTION` mode, matching
   obfuscated identity, non-replay, and a signed authority;
5. persist that verified authority idempotently;
6. grant the local entitlement;
7. call `AcknowledgePurchases`, retaining failures for launch/resume retry.

No receipt verifier means no paid grants; the complete story remains available.
An automatic launch `GetOwnedList` reconciliation refreshes only ownership
already bound to the active app identity. The explicit grown-up Restore action
is the only cross-install/profile adoption path.

## Build isolation

- Google: RevenueCat + Google Billing; no Samsung module, classes or permission.
- Galaxy: Samsung IAP 6.5.2; no RevenueCat commerce, BillingClient, Play billing
  permission, RevenueCat configuration, Test Store key or Google API key.
- `BuildGalaxyRelease` is fixed to `OPERATION_MODE_PRODUCTION`.
- `OPERATION_MODE_TEST` and `OPERATION_MODE_TEST_FAILURE` are evidence-only and
  must never enter a beta or production artifact.

## External PENDING evidence

- Owner/legal acceptance of the Samsung IAP SDK agreement.
- Commercial Seller status, tax/payout setup, app record, signing and upload.
- Creation, pricing and activation of all six Seller Items.
- Deployment of the receipt verifier and privacy/Data Safety review.
- Licensed tester on a physical Samsung device.
- Physical TEST success and TEST_FAILURE, cancellation, interruption,
  background/resume, offline, Restore, acknowledgement retry and identity-switch
  evidence.
- Closed-beta `PRODUCTION` install through the Samsung beta URL and signed-artifact
  purchase/restore evidence.

An emulator or mocked callback is never transaction proof.
