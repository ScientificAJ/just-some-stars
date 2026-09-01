using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI.Shop;
using NUnit.Framework;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class ShopFlowTests
    {
        private const string Anonymous = "$RCAnonymousID:task23";
        private const string Fingerprint = "sha256:task23-app";
        private const string PackageName = "com.scientificaj.justsomestars";
        private readonly List<string> m_TemporaryRoots = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var root in m_TemporaryRoots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            m_TemporaryRoots.Clear();
        }

        [Test]
        public async Task CommerceUnavailable_DoesNotBlockFrontend()
        {
            var store = new UnavailableStoreService();
            var startup = await store.InitializeAsync(CancellationToken.None);

            Assert.That(startup.State, Is.EqualTo(StartupResultState.Unavailable));
            Assert.That(store.Availability,
                Is.EqualTo(StoreAvailability.UnavailableConfiguration));
            Assert.That(await store.GetProductsAsync(CancellationToken.None), Is.Empty);
            Assert.That(
                (await store.PurchaseAsync(
                    new ContentId("store.explorer-edition"),
                    CancellationToken.None)).Status,
                Is.EqualTo(PurchaseStatus.Unavailable));
            StringAssert.Contains("story", store.StatusMessage.ToLowerInvariant());
            Assert.DoesNotThrowAsync(async () => await store.ShutdownAsync());
        }

        [Test]
        public async Task Purchase_CallbackSuccessWithoutVerifiedMappedEntitlement_DoesNotGrant()
        {
            var account = new FakeAccountService();
            var gateway = new FakeRevenueCatGateway();
            var store = CreateStore(account, gateway);
            await store.InitializeAsync(CancellationToken.None);
            await store.GetProductsAsync(CancellationToken.None);
            var product = new ContentId("store.explorer-edition");

            foreach (var info in new[]
            {
                Info(Anonymous, EntitlementVerification.NotRequested,
                    Entitlement("explorer_edition", true,
                        EntitlementVerification.NotRequested)),
                Info(Anonymous, EntitlementVerification.Failed,
                    Entitlement("explorer_edition", true,
                        EntitlementVerification.Failed)),
                Info(Anonymous, EntitlementVerification.Verified,
                    Entitlement("explorer_edition", false,
                        EntitlementVerification.Verified)),
                Info(Anonymous, EntitlementVerification.Verified,
                    Entitlement("unknown_entitlement", true,
                        EntitlementVerification.Verified)),
            })
            {
                gateway.NextPurchase = Success(info);
                var result = await store.PurchaseAsync(
                    product,
                    CancellationToken.None);
                Assert.That(result.Status, Is.EqualTo(PurchaseStatus.Failed));
                Assert.That(store.CurrentEntitlements.ActiveEntitlements, Is.Empty);
            }

            gateway.NextPurchase = Success(Info(
                Anonymous,
                EntitlementVerification.VerifiedOnDevice,
                Entitlement(
                    "explorer_edition",
                    true,
                    EntitlementVerification.VerifiedOnDevice)));
            var verified = await store.PurchaseAsync(product, CancellationToken.None);
            Assert.That(verified.Status, Is.EqualTo(PurchaseStatus.Purchased));
            Assert.That(
                verified.Entitlements.Owns(new ContentId("explorer_edition")),
                Is.True);
            await store.ShutdownAsync();
        }

        [Test]
        public async Task Purchase_ConcurrentCancelFailPending_AreDistinct()
        {
            var gateway = new FakeRevenueCatGateway();
            var store = CreateStore(new FakeAccountService(), gateway);
            await store.InitializeAsync(CancellationToken.None);
            await store.GetProductsAsync(CancellationToken.None);
            var product = new ContentId("store.explorer-edition");
            var deferred = new TaskCompletionSource<RevenueCatGatewayResult>();
            gateway.PurchaseHandler = (_, __) =>
                new ValueTask<RevenueCatGatewayResult>(
                deferred.Task);

            var first = store.PurchaseAsync(product, CancellationToken.None).AsTask();
            await WaitUntil(() => gateway.PurchaseCount == 1);
            var overlapping = await store.PurchaseAsync(
                product,
                CancellationToken.None);
            Assert.That(overlapping.Status, Is.EqualTo(PurchaseStatus.Pending));
            Assert.That(gateway.PurchaseCount, Is.EqualTo(1));

            deferred.SetResult(new RevenueCatGatewayResult(
                RevenueCatGatewayResultStatus.Cancelled,
                null,
                string.Empty));
            Assert.That((await first).Status, Is.EqualTo(PurchaseStatus.Cancelled));

            gateway.PurchaseHandler = null;
            gateway.NextPurchase = new RevenueCatGatewayResult(
                RevenueCatGatewayResultStatus.Failed,
                null,
                "ambiguous store error");
            Assert.That(
                (await store.PurchaseAsync(product, CancellationToken.None)).Status,
                Is.EqualTo(PurchaseStatus.Failed));

            gateway.NextPurchase = new RevenueCatGatewayResult(
                RevenueCatGatewayResultStatus.Pending,
                null,
                "pending");
            Assert.That(
                (await store.PurchaseAsync(product, CancellationToken.None)).Status,
                Is.EqualTo(PurchaseStatus.Pending));
            Assert.That(store.CurrentEntitlements.ActiveEntitlements, Is.Empty);
            await store.ShutdownAsync();
        }

        [Test]
        public async Task IdentityChange_InvalidatesInFlightAndOldCache()
        {
            var account = new FakeAccountService();
            var gateway = new FakeRevenueCatGateway();
            var store = CreateStore(account, gateway);
            await store.InitializeAsync(CancellationToken.None);
            await store.GetProductsAsync(CancellationToken.None);
            var deferred = new TaskCompletionSource<RevenueCatGatewayResult>();
            gateway.PurchaseHandler = (_, __) =>
                new ValueTask<RevenueCatGatewayResult>(
                deferred.Task);

            var purchase = store.PurchaseAsync(
                new ContentId("store.explorer-edition"),
                CancellationToken.None).AsTask();
            await WaitUntil(() => gateway.PurchaseCount == 1);
            account.SetFirebaseUser("firebase-user-a");
            Assert.That(store.CurrentEntitlements.ActiveEntitlements, Is.Empty,
                "Former-user ownership must disappear synchronously at transition start.");

            deferred.SetResult(Success(Info(
                Anonymous,
                EntitlementVerification.Verified,
                Entitlement(
                    "explorer_edition",
                    true,
                    EntitlementVerification.Verified))));
            Assert.That((await purchase).Status,
                Is.EqualTo(PurchaseStatus.Unavailable));
            await store.WaitForIdentityIdleAsync();
            Assert.That(gateway.CurrentAppUserId, Is.EqualTo("firebase-user-a"));
            Assert.That(store.CurrentEntitlements.ActiveEntitlements, Is.Empty);
            Assert.That(gateway.LogInIds, Is.EqualTo(new[] { "firebase-user-a" }));
            await store.ShutdownAsync();
        }

        [Test]
        public async Task IdentityTransitions_UseDirectLogInAndNewAnonymousLogout()
        {
            var account = new FakeAccountService();
            var gateway = new FakeRevenueCatGateway();
            var store = CreateStore(account, gateway);
            await store.InitializeAsync(CancellationToken.None);

            account.SetFirebaseUser("firebase-user-a");
            await store.WaitForIdentityIdleAsync();
            account.SetFirebaseUser("firebase-user-b");
            await store.WaitForIdentityIdleAsync();
            account.SetFirebaseUser(string.Empty);
            await store.WaitForIdentityIdleAsync();

            Assert.That(gateway.LogInIds,
                Is.EqualTo(new[] { "firebase-user-a", "firebase-user-b" }));
            Assert.That(gateway.LogOutCount, Is.EqualTo(1),
                "Anonymous-to-UID and UID-to-UID must never log out first.");
            Assert.That(gateway.CurrentAppUserId,
                Does.StartWith("$RCAnonymousID:new-"));

            account.SetFirebaseUser(new string('x', 101));
            await store.WaitForIdentityIdleAsync();
            Assert.That(store.Availability,
                Is.EqualTo(StoreAvailability.UnavailableConfiguration));
            Assert.That(gateway.LogInIds, Has.Count.EqualTo(2),
                "Unsafe or oversized UIDs must not be truncated or sent.");
            await store.ShutdownAsync();
        }

        [Test]
        public async Task FailedIdentityTransition_HidesFormerOwnershipAndFailsClosed()
        {
            var account = new FakeAccountService();
            var gateway = new FakeRevenueCatGateway();
            var store = CreateStore(account, gateway);
            await store.InitializeAsync(CancellationToken.None);
            gateway.Publish(Info(
                Anonymous,
                EntitlementVerification.Verified,
                Entitlement(
                    "explorer_edition",
                    true,
                    EntitlementVerification.Verified)));
            Assert.That(
                store.CurrentEntitlements.Owns(new ContentId("explorer_edition")),
                Is.True);
            gateway.LogInHandler = (_, __) =>
                new ValueTask<RevenueCatGatewayResult>(
                    new RevenueCatGatewayResult(
                        RevenueCatGatewayResultStatus.Failed,
                        null,
                        "login failed"));

            account.SetFirebaseUser("firebase-user-a");
            await store.WaitForIdentityIdleAsync();

            Assert.That(store.CurrentEntitlements.ActiveEntitlements, Is.Empty);
            Assert.That(store.Availability, Is.EqualTo(StoreAvailability.Failed));
            Assert.That(gateway.CurrentAppUserId, Is.EqualTo(Anonymous));
            await store.ShutdownAsync();
        }

        [Test]
        public async Task InterruptedPurchase_ResumeRefreshesExactlyOnce()
        {
            var gateway = new FakeRevenueCatGateway
            {
                NextPurchase = new RevenueCatGatewayResult(
                    RevenueCatGatewayResultStatus.Pending,
                    null,
                    "pending"),
            };
            var store = CreateStore(new FakeAccountService(), gateway);
            await store.InitializeAsync(CancellationToken.None);
            await store.GetProductsAsync(CancellationToken.None);
            var pending = await store.PurchaseAsync(
                new ContentId("store.explorer-edition"),
                CancellationToken.None);
            Assert.That(pending.Status, Is.EqualTo(PurchaseStatus.Pending));
            Assert.That(store.CurrentEntitlements.ActiveEntitlements, Is.Empty);

            var deferred = new TaskCompletionSource<RevenueCatGatewayResult>();
            gateway.RefreshHandler = _ => new ValueTask<RevenueCatGatewayResult>(
                deferred.Task);
            var firstResume = store.ResumeAsync(CancellationToken.None).AsTask();
            await WaitUntil(() => gateway.RefreshCount == 1);
            var secondResume = store.ResumeAsync(CancellationToken.None).AsTask();
            deferred.SetResult(Success(Info(
                Anonymous,
                EntitlementVerification.Verified,
                Entitlement(
                    "explorer_edition",
                    true,
                    EntitlementVerification.Verified))));
            await Task.WhenAll(firstResume, secondResume);

            Assert.That(gateway.RefreshCount, Is.EqualTo(1));
            Assert.That(gateway.RestoreCount, Is.EqualTo(0),
                "Resume refresh must never trigger the OS-login Restore flow.");
            Assert.That(
                store.CurrentEntitlements.Owns(new ContentId("explorer_edition")),
                Is.True);
            await store.ShutdownAsync();
        }

        [Test]
        public async Task CancelledLocalWaitAfterNativePurchase_RefreshesOnResume()
        {
            var gateway = new FakeRevenueCatGateway();
            var store = CreateStore(new FakeAccountService(), gateway);
            await store.InitializeAsync(CancellationToken.None);
            await store.GetProductsAsync(CancellationToken.None);
            gateway.PurchaseHandler = (_, token) =>
            {
                var completion = new TaskCompletionSource<RevenueCatGatewayResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() => completion.TrySetCanceled(token));
                return new ValueTask<RevenueCatGatewayResult>(completion.Task);
            };
            using var cancelled = new CancellationTokenSource();
            var purchase = store.PurchaseAsync(
                new ContentId("store.explorer-edition"),
                cancelled.Token).AsTask();
            await WaitUntil(() => gateway.PurchaseCount == 1);
            cancelled.Cancel();
            try
            {
                await purchase;
                Assert.Fail("The cancelled local wait must surface cancellation.");
            }
            catch (OperationCanceledException)
            {
                // Expected. Await directly so Unity's PlayMode synchronization
                // context is not blocked by NUnit's synchronous ThrowsAsync path.
            }

            gateway.RefreshHandler = _ =>
                new ValueTask<RevenueCatGatewayResult>(Success(Info(
                    Anonymous,
                    EntitlementVerification.Verified,
                    Entitlement(
                        "explorer_edition",
                        true,
                        EntitlementVerification.Verified))));
            await store.ResumeAsync(CancellationToken.None);

            Assert.That(gateway.RefreshCount, Is.EqualTo(1));
            Assert.That(
                store.CurrentEntitlements.Owns(new ContentId("explorer_edition")),
                Is.True);
            await store.ShutdownAsync();
        }

        [Test]
        public async Task BackgroundedNativePurchase_CompletesWithoutFalseNoChargeClaim()
        {
            var account = new FakeAccountService();
            var gateway = new FakeRevenueCatGateway();
            var store = CreateStore(account, gateway);
            await store.InitializeAsync(CancellationToken.None);
            var gate = new RecordingGrownUpGate { Allowed = true };
            using var controller = new ShopController(store, gate);
            await controller.OpenAsync(CancellationToken.None);
            var deferred = new TaskCompletionSource<RevenueCatGatewayResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            gateway.PurchaseHandler = (_, __) =>
                new ValueTask<RevenueCatGatewayResult>(deferred.Task);

            var purchase = controller.PurchaseAsync(
                new ContentId("store.explorer-edition"),
                BirthdayAgeBand.Adult,
                CancellationToken.None).AsTask();
            await WaitUntil(() => gateway.PurchaseCount == 1);
            controller.NotifyBackgrounded();
            deferred.SetResult(Success(Info(
                Anonymous,
                EntitlementVerification.Verified,
                Entitlement(
                    "explorer_edition",
                    true,
                    EntitlementVerification.Verified))));
            var result = await purchase;

            Assert.That(result.Status, Is.EqualTo(PurchaseStatus.Cancelled));
            Assert.That(result.Message, Does.Not.Contain("Nothing was charged"));
            Assert.That(
                store.CurrentEntitlements.Owns(new ContentId("explorer_edition")),
                Is.True,
                "The service must reconcile the native result even after the shop " +
                "surface is disarmed.");
            await store.ShutdownAsync();
        }

        [Test]
        public async Task Restore_RequiresExplicitGrownUpAction()
        {
            var product = Product();
            var store = new RecordingStoreService(product);
            var gate = new RecordingGrownUpGate { Allowed = false };
            using var controller = new ShopController(store, gate);
            await controller.OpenAsync(CancellationToken.None);

            var denied = await controller.RestoreAsync(
                BirthdayAgeBand.Child,
                CancellationToken.None);
            Assert.That(gate.Calls, Is.EqualTo(1));
            Assert.That(store.RestoreCount, Is.EqualTo(0));
            Assert.That(denied.ActiveEntitlements, Is.Empty);

            gate.Allowed = true;
            await controller.RestoreAsync(
                BirthdayAgeBand.Adult,
                CancellationToken.None);
            Assert.That(gate.Calls, Is.EqualTo(2));
            Assert.That(store.RestoreCount, Is.EqualTo(1));
            Assert.That(store.ResumeCount, Is.EqualTo(0),
                "Opening/resuming the app must never call Restore automatically.");
        }

        [Test]
        public async Task RevenueCatRestore_AdoptsEveryIndividualCosmeticEntitlement()
        {
            var individualEntitlements = new[]
            {
                "jss.cosmetic.captain.launch-navigator",
                "jss.cosmetic.captain.launch-planetary",
                "jss.cosmetic.captain.launch-starlight",
                "jss.cosmetic.captain.star-charm",
                "jss.cosmetic.captain.birthday-charm",
                "jss.cosmetic.captain.ori-wristlink",
                "jss.cosmetic.ori.festival-canopy",
                "jss.cosmetic.ori.moon-chimes",
                "jss.cosmetic.ori.comet-trail",
                "jss.cosmetic.ship.builder-rig",
                "jss.cosmetic.ship.signal-tower",
                "jss.cosmetic.ship.comet-launch",
                "jss.cosmetic.lens.rocket-window",
                "jss.cosmetic.lens.starlight-compass",
                "jss.cosmetic.clubhouse.moon-chair",
                "jss.cosmetic.clubhouse.ori-radio",
                "jss.cosmetic.photo.captain-pose",
                "jss.cosmetic.photo.stargazer-pose",
                "jss.cosmetic.crew.launch-homecoming",
                "jss.cosmetic.crew.birthday-expedition",
            };
            var gateway = new FakeRevenueCatGateway
            {
                NextRestore = Success(Info(
                    Anonymous,
                    EntitlementVerification.Verified,
                    individualEntitlements.Select(value => Entitlement(
                        value,
                        true,
                        EntitlementVerification.Verified)).ToArray())),
            };
            var store = CreateStore(new FakeAccountService(), gateway);
            await store.InitializeAsync(CancellationToken.None);

            var restored = await store.RestoreAsync(CancellationToken.None);

            Assert.That(gateway.RestoreCount, Is.EqualTo(1));
            foreach (var entitlement in individualEntitlements)
            {
                Assert.That(restored.Owns(new ContentId(entitlement)),
                    Is.True,
                    entitlement);
            }
            await store.ShutdownAsync();
        }

        [Test]
        public async Task Shop_GrownUpPolicyFailsClosed()
        {
            var response = new GrownUpChallengeResponse();
            var presenter = new RecordingChallengePresenter(
                challenge => response = new GrownUpChallengeResponse(
                    challenge.Id,
                    confirmed: true,
                    answer: challenge.LeftOperand + challenge.RightOperand + 1));
            var now = new DateTime(638922816000000000L, DateTimeKind.Utc);
            var gate = new GrownUpPurchaseGate(presenter, () => now);

            Assert.That(await gate.AuthorizeAsync(
                BirthdayAgeBand.Unknown,
                GrownUpAction.Purchase,
                CancellationToken.None), Is.False);
            Assert.That(presenter.LastChallenge.RequiresArithmetic, Is.True);

            presenter.Factory = challenge => new GrownUpChallengeResponse(
                Guid.NewGuid(),
                confirmed: true,
                answer: challenge.LeftOperand + challenge.RightOperand);
            Assert.That(await gate.AuthorizeAsync(
                BirthdayAgeBand.Teen,
                GrownUpAction.Purchase,
                CancellationToken.None), Is.False,
                "A replayed or mismatched challenge response must fail closed.");

            presenter.Factory = challenge => new GrownUpChallengeResponse(
                challenge.Id,
                confirmed: false,
                answer: 0);
            Assert.That(await gate.AuthorizeAsync(
                BirthdayAgeBand.Adult,
                GrownUpAction.Purchase,
                CancellationToken.None), Is.False);

            presenter.Factory = challenge => new GrownUpChallengeResponse(
                challenge.Id,
                confirmed: true,
                answer: challenge.LeftOperand + challenge.RightOperand);
            Assert.That(await gate.AuthorizeAsync(
                BirthdayAgeBand.Child,
                GrownUpAction.Purchase,
                CancellationToken.None), Is.True);
            Assert.That(response.Confirmed, Is.True);
        }

        [Test]
        public async Task Offerings_UnknownPackagesHiddenAndLivePricePreserved()
        {
            var gateway = new FakeRevenueCatGateway();
            gateway.Products.Add(new RevenueCatGatewayProduct(
                "unknown.product",
                "launch_cosmetics",
                "unknown_package",
                "Mystery",
                "Unknown",
                "$0.01",
                "USD"));
            var store = CreateStore(new FakeAccountService(), gateway);
            await store.InitializeAsync(CancellationToken.None);
            var products = await store.GetProductsAsync(CancellationToken.None);

            Assert.That(products, Has.Count.EqualTo(1));
            Assert.That(products[0].Title, Is.EqualTo("Explorer Edition"));
            Assert.That(products[0].FormattedPrice, Is.EqualTo("₹1,299.00"));
            Assert.That(products[0].CurrencyCode, Is.EqualTo("INR"));
            Assert.That(products[0].IsOneTimeNonConsumable, Is.True);
            await store.ShutdownAsync();
        }

        [Test]
        public void PaidOwnership_NeverTouchesEarnedSave()
        {
            var save = GameSave.CreateNew(
                "device.task23",
                638922816000000000L);
            save.EarnedCosmeticIds = new[] { "cosmetic.earned.mirra" };
            var before = save.Copy();

            var commerceFields = typeof(RevenueCatStoreService)
                .GetFields(BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();
            Assert.That(commerceFields, Has.None.EqualTo(typeof(GameSave)));
            Assert.That(save, Is.EqualTo(before));
            Assert.That(save.EarnedCosmeticIds,
                Is.EqualTo(new[] { "cosmetic.earned.mirra" }));
        }

        private RevenueCatStoreService CreateStore(
            FakeAccountService account,
            FakeRevenueCatGateway gateway)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "JssTask23Shop",
                Guid.NewGuid().ToString("N"));
            m_TemporaryRoots.Add(root);
            return new RevenueCatStoreService(
                account,
                gateway,
                new OfflineEntitlementCache(Path.Combine(root, "cache.json")));
        }

        private static StoreProduct Product() => new StoreProduct(
            new ContentId("store.explorer-edition"),
            "jss.edition.explorer",
            "launch_cosmetics",
            "explorer_edition_package",
            new ContentId("explorer_edition"),
            "Explorer Edition",
            "Optional cosmetic and replay extras.",
            "₹1,299.00",
            "INR");

        private static RevenueCatEntitlement Entitlement(
            string id,
            bool active,
            EntitlementVerification verification) =>
            new RevenueCatEntitlement(id, active, verification);

        private static RevenueCatCustomerInfo Info(
            string user,
            EntitlementVerification verification,
            params RevenueCatEntitlement[] entitlements) =>
            new RevenueCatCustomerInfo(
                user,
                verification,
                new DateTime(638922816000000000L, DateTimeKind.Utc),
                entitlements);

        private static RevenueCatGatewayResult Success(
            RevenueCatCustomerInfo info) => new RevenueCatGatewayResult(
                RevenueCatGatewayResultStatus.Succeeded,
                info,
                string.Empty);

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            {
                await Task.Yield();
            }

            Assert.That(condition(), Is.True, "Timed out waiting for async fake.");
        }

        private sealed class FakeAccountService : IAccountService
        {
            public FakeAccountService()
            {
                Current = State(string.Empty);
            }

            public AccountState Current { get; private set; }

            public event Action<AccountState> StateChanged;

            public void SetFirebaseUser(string userId)
            {
                Current = State(userId);
                StateChanged?.Invoke(Current);
            }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<AccountLinkResult> LinkGoogleAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<AccountLinkResult>(
                    new AccountLinkResult(AccountLinkStatus.Unavailable));

            public ValueTask<AccountLinkResult> ResolveConflictAsync(
                AccountConflictChoice choice,
                CancellationToken cancellationToken) =>
                new ValueTask<AccountLinkResult>(
                    new AccountLinkResult(AccountLinkStatus.Failed));

            public ValueTask<CloudSyncResult> SyncAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<CloudSyncResult>(
                    new CloudSyncResult(false, false, string.Empty));

            public ValueTask<AccountExportResult> ExportDataAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<AccountExportResult>(
                    new AccountExportResult(false, string.Empty, string.Empty));

            public ValueTask<AccountUnlinkResult> UnlinkGoogleAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<AccountUnlinkResult>(new AccountUnlinkResult(
                    AccountUnlinkStatus.NotLinked,
                    string.Empty));

            public ValueTask SignOutAsync(CancellationToken cancellationToken) =>
                default;

            public ValueTask DeleteAccountAsync(
                CancellationToken cancellationToken) => default;

            private static AccountState State(string userId) => new AccountState(
                string.IsNullOrEmpty(userId)
                    ? AccountConnection.CloudAvailable
                    : AccountConnection.Linked,
                AccountCapability.Available,
                AccountSyncState.LocalOnly,
                AccountOperation.None,
                "guest.00000000000000000000000000000000",
                userId,
                string.Empty);
        }

        private sealed class FakeRevenueCatGateway : IRevenueCatGateway
        {
            private int m_AnonymousOrdinal;

            public FakeRevenueCatGateway()
            {
                Products.Add(new RevenueCatGatewayProduct(
                    "jss.edition.explorer",
                    "launch_cosmetics",
                    "explorer_edition_package",
                    "Explorer Edition",
                    "Optional cosmetic and replay extras.",
                    "₹1,299.00",
                    "INR"));
            }

            public bool IsConfigured { get; set; } = true;

            public string AppFingerprint => Fingerprint;

            public StoreEnvironment Environment =>
                StoreEnvironment.RevenueCatTestStore;

            public string AndroidPackageId => PackageName;

            public string CurrentAppUserId { get; private set; } = Anonymous;

            public List<RevenueCatGatewayProduct> Products { get; } =
                new List<RevenueCatGatewayProduct>();

            public List<string> LogInIds { get; } = new List<string>();

            public int LogOutCount { get; private set; }

            public int PurchaseCount { get; private set; }

            public int RestoreCount { get; private set; }

            public int RefreshCount { get; private set; }

            public RevenueCatGatewayResult NextPurchase { get; set; } =
                new RevenueCatGatewayResult(
                    RevenueCatGatewayResultStatus.Cancelled,
                    null,
                    string.Empty);

            public RevenueCatGatewayResult NextRestore { get; set; }

            public Func<string, CancellationToken,
                ValueTask<RevenueCatGatewayResult>> PurchaseHandler
            {
                get;
                set;
            }

            public Func<CancellationToken, ValueTask<RevenueCatGatewayResult>>
                RefreshHandler { get; set; }

            public Func<string, CancellationToken,
                ValueTask<RevenueCatGatewayResult>> LogInHandler { get; set; }

            public event Action<RevenueCatCustomerInfo> CustomerInfoUpdated;

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(IsConfigured
                    ? StartupResult.Available()
                    : StartupResult.Unavailable("not configured"));
            }

            public ValueTask ShutdownAsync() => default;

            public ValueTask<IReadOnlyList<RevenueCatGatewayProduct>>
                GetProductsAsync(CancellationToken cancellationToken) =>
                new ValueTask<IReadOnlyList<RevenueCatGatewayProduct>>(Products);

            public ValueTask<RevenueCatGatewayResult> PurchaseAsync(
                string storeProductId,
                CancellationToken cancellationToken)
            {
                PurchaseCount++;
                return PurchaseHandler != null
                    ? PurchaseHandler(storeProductId, cancellationToken)
                    : new ValueTask<RevenueCatGatewayResult>(NextPurchase);
            }

            public ValueTask<RevenueCatGatewayResult> RestoreAsync(
                CancellationToken cancellationToken)
            {
                RestoreCount++;
                return new ValueTask<RevenueCatGatewayResult>(NextRestore ??
                    new RevenueCatGatewayResult(
                        RevenueCatGatewayResultStatus.Succeeded,
                        Info(
                            CurrentAppUserId,
                            EntitlementVerification.Verified),
                        string.Empty));
            }

            public ValueTask<RevenueCatGatewayResult> RefreshAsync(
                CancellationToken cancellationToken)
            {
                RefreshCount++;
                return RefreshHandler != null
                    ? RefreshHandler(cancellationToken)
                    : new ValueTask<RevenueCatGatewayResult>(Success(Info(
                        CurrentAppUserId,
                        EntitlementVerification.Verified)));
            }

            public ValueTask<RevenueCatGatewayResult> LogInAsync(
                string firebaseUserId,
                CancellationToken cancellationToken)
            {
                LogInIds.Add(firebaseUserId);
                if (LogInHandler != null)
                {
                    return LogInHandler(firebaseUserId, cancellationToken);
                }

                CurrentAppUserId = firebaseUserId;
                return new ValueTask<RevenueCatGatewayResult>(Success(Info(
                    CurrentAppUserId,
                    EntitlementVerification.Verified)));
            }

            public ValueTask<RevenueCatGatewayResult> LogOutAsync(
                CancellationToken cancellationToken)
            {
                LogOutCount++;
                CurrentAppUserId = "$RCAnonymousID:new-" + (++m_AnonymousOrdinal);
                return new ValueTask<RevenueCatGatewayResult>(Success(Info(
                    CurrentAppUserId,
                    EntitlementVerification.Verified)));
            }

            public void Publish(RevenueCatCustomerInfo info) =>
                CustomerInfoUpdated?.Invoke(info);
        }

        private sealed class RecordingGrownUpGate : IGrownUpPurchaseGate
        {
            public bool Allowed { get; set; }

            public int Calls { get; private set; }

            public ValueTask<bool> AuthorizeAsync(
                BirthdayAgeBand ageBand,
                GrownUpAction action,
                CancellationToken cancellationToken)
            {
                Calls++;
                return new ValueTask<bool>(Allowed);
            }
        }

        private sealed class RecordingChallengePresenter :
            IGrownUpChallengePresenter
        {
            public RecordingChallengePresenter(
                Func<GrownUpChallenge, GrownUpChallengeResponse> factory)
            {
                Factory = factory;
            }

            public Func<GrownUpChallenge, GrownUpChallengeResponse> Factory
            {
                get;
                set;
            }

            public GrownUpChallenge LastChallenge { get; private set; }

            public ValueTask<GrownUpChallengeResponse> PresentAsync(
                GrownUpChallenge challenge,
                CancellationToken cancellationToken)
            {
                LastChallenge = challenge;
                return new ValueTask<GrownUpChallengeResponse>(Factory(challenge));
            }
        }

        private sealed class RecordingStoreService : IStoreService
        {
            private readonly IReadOnlyList<StoreProduct> m_Products;

            public RecordingStoreService(StoreProduct product)
            {
                m_Products = new[] { product };
            }

            public StoreAvailability Availability => StoreAvailability.Available;

            public IReadOnlyList<StoreProduct> Products => m_Products;

            public EntitlementSnapshot CurrentEntitlements =>
                EntitlementSnapshot.Empty;

            public string StatusMessage => "Ready";

            public int RestoreCount { get; private set; }

            public int ResumeCount { get; private set; }

            public event Action StateChanged;

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<IReadOnlyList<StoreProduct>>(m_Products);

            public ValueTask<PurchaseResult> PurchaseAsync(
                ContentId productId,
                CancellationToken cancellationToken) =>
                new ValueTask<PurchaseResult>(new PurchaseResult(
                    PurchaseStatus.Cancelled,
                    EntitlementSnapshot.Empty,
                    string.Empty));

            public ValueTask<EntitlementSnapshot> RestoreAsync(
                CancellationToken cancellationToken)
            {
                RestoreCount++;
                return new ValueTask<EntitlementSnapshot>(
                    EntitlementSnapshot.Empty);
            }

            public ValueTask<EntitlementSnapshot> RefreshEntitlementsAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<EntitlementSnapshot>(EntitlementSnapshot.Empty);

            public ValueTask ResumeAsync(CancellationToken cancellationToken)
            {
                ResumeCount++;
                return default;
            }
        }
    }
}
