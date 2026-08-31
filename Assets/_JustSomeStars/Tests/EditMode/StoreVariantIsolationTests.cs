using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Editor.Build;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Commerce.Galaxy;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class StoreVariantIsolationTests
    {
        private const string GalaxyPackage =
            "com.scientificaj.justsomestars.galaxy";

        private string m_ProjectRoot;

        [SetUp]
        public void SetUp()
        {
            m_ProjectRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask24StoreIsolation",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_ProjectRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_ProjectRoot))
            {
                Directory.Delete(m_ProjectRoot, recursive: true);
            }
        }

        [Test]
        public void GeneratedProjects_HaveMutuallyExclusiveBillingGraphs()
        {
            var processorType = typeof(BuildCli).Assembly.GetType(
                "JustSomeStars.Editor.Build.RevenueCatAndroidBuildProcessor");
            Assert.That(processorType, Is.Not.Null);
            var patch = processorType.GetMethod(
                "PatchGeneratedAndroidProject",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(bool) },
                modifiers: null);
            Assert.That(patch, Is.Not.Null);

            var galaxy = CreateGeneratedProject("galaxy");
            patch.Invoke(null, new object[] { galaxy, true });
            var galaxyRoot = Directory.GetParent(galaxy)?.FullName;
            Assert.That(galaxyRoot, Is.Not.Null);
            var galaxyGradle = File.ReadAllText(Path.Combine(
                galaxy,
                "build.gradle"));
            var galaxySettings = File.ReadAllText(Path.Combine(
                galaxyRoot,
                "settings.gradle"));
            StringAssert.Contains(
                "implementation project(':jssGalaxyBilling')",
                galaxyGradle);
            var dependenciesStart = galaxyGradle.IndexOf(
                "dependencies {",
                StringComparison.Ordinal);
            var dependenciesEnd = galaxyGradle.IndexOf(
                "}\nandroid {",
                dependenciesStart,
                StringComparison.Ordinal);
            var moduleDependency = galaxyGradle.IndexOf(
                "implementation project(':jssGalaxyBilling')",
                StringComparison.Ordinal);
            Assert.That(moduleDependency, Is.GreaterThan(dependenciesStart));
            Assert.That(moduleDependency, Is.LessThan(dependenciesEnd),
                "The Galaxy project dependency must be inside dependencies, " +
                "not before the last brace in the Unity template.");
            StringAssert.Contains(
                "include ':jssGalaxyBilling'",
                galaxySettings);
            StringAssert.DoesNotContain("purchases-hybrid-common", galaxyGradle);
            StringAssert.DoesNotContain("com.revenuecat", galaxyGradle);
            StringAssert.DoesNotContain("com.android.billingclient", galaxyGradle);
            StringAssert.DoesNotContain("com.android.vending.BILLING", galaxyGradle);
            Assert.That(
                Directory.Exists(Path.Combine(galaxyRoot, "jssGalaxyBilling")),
                Is.True);

            var google = CreateGeneratedProject("google");
            patch.Invoke(null, new object[] { google, false });
            var googleRoot = Directory.GetParent(google)?.FullName;
            Assert.That(googleRoot, Is.Not.Null);
            var googleGradle = File.ReadAllText(Path.Combine(
                google,
                "build.gradle"));
            var googleSettings = File.ReadAllText(Path.Combine(
                googleRoot,
                "settings.gradle"));
            StringAssert.Contains("purchases-hybrid-common", googleGradle);
            StringAssert.DoesNotContain("jssGalaxyBilling", googleGradle);
            StringAssert.DoesNotContain("jssGalaxyBilling", googleSettings);
            StringAssert.DoesNotContain("com.samsung.developer:iap", googleGradle);
            Assert.That(
                Directory.Exists(Path.Combine(googleRoot, "jssGalaxyBilling")),
                Is.False);
        }

        [Test]
        public void GalaxyModule_PinsSamsung652AndExposesOnlyRequiredOperations()
        {
            var moduleRoot = Path.Combine(
                Application.dataPath,
                "Plugins",
                "Android",
                "jss-galaxy-billing");
            var gradlePath = Path.Combine(moduleRoot, "build.gradle");
            var manifestPath = Path.Combine(
                moduleRoot,
                "src",
                "main",
                "AndroidManifest.xml");
            var bridgePath = Path.Combine(
                moduleRoot,
                "src",
                "main",
                "java",
                "com",
                "scientificaj",
                "justsomestars",
                "galaxy",
                "JssSamsungIapBridge.java");

            Assert.That(File.Exists(gradlePath), Is.True);
            Assert.That(File.Exists(manifestPath), Is.True);
            Assert.That(File.Exists(bridgePath), Is.True);
            var gradle = File.ReadAllText(gradlePath);
            StringAssert.Contains("com.samsung.developer:iap:6.5.2", gradle);
            StringAssert.DoesNotContain("com.revenuecat", gradle);
            StringAssert.DoesNotContain("com.android.billingclient", gradle);

            var manifest = File.ReadAllText(manifestPath);
            StringAssert.Contains(
                "com.samsung.android.iap.permission.BILLING",
                manifest);
            StringAssert.DoesNotContain("com.android.vending.BILLING", manifest);

            var bridge = File.ReadAllText(bridgePath);
            foreach (var operation in new[]
            {
                "void configure(",
                "void getProductsDetails(",
                "void getOwnedList(",
                "void startPayment(",
                "void acknowledgePurchases(",
                "void dispose(",
            })
            {
                StringAssert.Contains(operation, bridge);
            }

            StringAssert.Contains("OPERATION_MODE_PRODUCTION", bridge);
            StringAssert.Contains("OPERATION_MODE_TEST", bridge);
            StringAssert.Contains("OPERATION_MODE_TEST_FAILURE", bridge);
            StringAssert.Contains("obfuscatedAccountId", bridge);
            StringAssert.Contains("obfuscatedProfileId", bridge);
            StringAssert.Contains("getStatusCode", File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(bridgePath),
                "GalaxyJson.java")));
            StringAssert.DoesNotContain("consumePurchasedItems", bridge);
            StringAssert.DoesNotContain("Purchases.configure", bridge);
        }

        [Test]
        public void GalaxyReleaseMode_IsCompileTimeSelectedAndReleaseSafe()
        {
            var policyType = typeof(BuildCli).Assembly.GetType(
                "JustSomeStars.Editor.Build.SamsungIapBuildModePolicy");
            Assert.That(policyType, Is.Not.Null);
            var resolve = policyType.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null);

            Assert.That(resolve.Invoke(null, new object[]
            {
                new[] { "JSS_GALAXY" },
            })?.ToString(), Is.EqualTo("Production"));
            Assert.Throws<TargetInvocationException>(() =>
                resolve.Invoke(null, new object[]
                {
                    new[] { "JSS_GALAXY", "JSS_GALAXY_IAP_TEST" },
                }));
            Assert.Throws<TargetInvocationException>(() =>
                resolve.Invoke(null, new object[]
                {
                    new[]
                    {
                        "JSS_GALAXY",
                        "JSS_GALAXY_IAP_TEST_FAILURE",
                    },
                }));

            var leaseSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_JustSomeStars",
                "Editor",
                "Build",
                "RevenueCatBuildConfigurationLease.cs"));
            StringAssert.Contains(
                "SamsungIapBuildModePolicy.Resolve(configuration.DefineSymbols)",
                leaseSource);
            var gatewaySource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_JustSomeStars",
                "Runtime",
                "Commerce",
                "Galaxy",
                "GalaxyAndroidJavaGateway.cs"));
            StringAssert.Contains("configure", gatewaySource);
            StringAssert.Contains("PRODUCTION", gatewaySource);
        }

        [Test]
        public void GalaxyReceiptPolicy_RejectsEveryUntrustedGrantBoundary()
        {
            var policyType = typeof(StoreProduct).Assembly.GetType(
                "JustSomeStars.Runtime.Commerce.GalaxyReceiptPolicy");
            Assert.That(policyType, Is.Not.Null);
            var accept = policyType.GetMethod(
                "Accept",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(bool),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(long),
                    typeof(long),
                    typeof(bool),
                    typeof(string),
                },
                modifiers: null);
            Assert.That(accept, Is.Not.Null);

            var valid = Arguments(
                verified: true,
                purchaseId: "purchase-1",
                itemId: "jss.collection.mirra",
                packageId: GalaxyPackage,
                mode: "PRODUCTION",
                accountId: "account-hash",
                profileId: "profile-hash",
                expectedGeneration: 7,
                callbackGeneration: 7,
                replayed: false,
                signedAuthority: "server-signed-authority");
            Assert.That((bool)accept.Invoke(null, valid), Is.True);

            foreach (var mutation in new Action<object[]>[]
            {
                args => args[0] = false,
                args => args[1] = string.Empty,
                args => args[2] = "unknown.item",
                args => args[3] = "com.example.wrong",
                args => args[4] = "TEST",
                args => args[5] = "wrong-account",
                args => args[6] = "wrong-profile",
                args => args[8] = 6L,
                args => args[9] = true,
                args => args[10] = string.Empty,
            })
            {
                var rejected = (object[])valid.Clone();
                mutation(rejected);
                Assert.That((bool)accept.Invoke(null, rejected), Is.False);
            }
        }

        [Test]
        public void GalaxyManagedAdapter_VerifiesPersistsThenAcknowledges()
        {
            var sourcePath = Path.Combine(
                Application.dataPath,
                "_JustSomeStars",
                "Runtime",
                "Commerce",
                "GalaxyStoreService.cs");
            Assert.That(File.Exists(sourcePath), Is.True);
            var source = File.ReadAllText(sourcePath);
            StringAssert.Contains("sealed class GalaxyStoreService : IStoreService", source);
            StringAssert.Contains("IGalaxyReceiptVerifier", source);
            StringAssert.Contains("GetOwnedListAsync", source);
            StringAssert.Contains("VerifyAsync", source);
            StringAssert.Contains("PersistVerifiedAsync", source);
            StringAssert.Contains("AcknowledgePurchasesAsync", source);
            StringAssert.Contains("RetryAcknowledgementsAsync", source);
            StringAssert.Contains("IdentityGeneration", source);
            StringAssert.Contains("SignedAuthority", source);
            var installerSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_JustSomeStars",
                "Runtime",
                "Commerce",
                "Galaxy",
                "GalaxyProviderInstaller.cs"));
            StringAssert.Contains("#if JSS_GALAXY", installerSource);
            StringAssert.DoesNotContain("RevenueCatStoreService", source);
            StringAssert.DoesNotContain("CustomerInfo", source);
            StringAssert.DoesNotContain("ConsumePurchasedItems", source);

            var verify = source.IndexOf("VerifyAsync", StringComparison.Ordinal);
            var persist = source.IndexOf(
                "PersistVerifiedAsync",
                verify,
                StringComparison.Ordinal);
            var acknowledge = source.IndexOf(
                "AcknowledgePurchasesAsync",
                persist,
                StringComparison.Ordinal);
            Assert.That(verify, Is.GreaterThanOrEqualTo(0));
            Assert.That(persist, Is.GreaterThan(verify));
            Assert.That(acknowledge, Is.GreaterThan(persist));
        }

        [Test]
        public async Task GalaxyRuntimeProvider_WiresRealGatewayLedgerAndFailClosedVerifier()
        {
            var root = Path.Combine(
                Application.dataPath,
                "_JustSomeStars",
                "Runtime",
                "Commerce",
                "Galaxy");
            var gateway = Path.Combine(root, "GalaxyAndroidJavaGateway.cs");
            var ledger = Path.Combine(root, "GalaxyFileEntitlementLedger.cs");
            var installer = Path.Combine(root, "GalaxyProviderInstaller.cs");
            Assert.That(File.Exists(gateway), Is.True);
            Assert.That(File.Exists(ledger), Is.True);
            Assert.That(File.Exists(installer), Is.True);

            var gatewaySource = File.ReadAllText(gateway);
            StringAssert.Contains("AndroidJavaClass", gatewaySource);
            StringAssert.Contains("JssSamsungIapBridge", gatewaySource);
            StringAssert.Contains("GetOwnedListAsync", gatewaySource);
            StringAssert.Contains("AcknowledgePurchasesAsync", gatewaySource);

            var ledgerSource = File.ReadAllText(ledger);
            StringAssert.Contains("SignedAuthority", ledgerSource);
            StringAssert.Contains("WriteAtomically", ledgerSource);
            StringAssert.Contains("LoadPendingAcknowledgementsAsync", ledgerSource);

            var installerSource = File.ReadAllText(installer);
            StringAssert.Contains("RuntimeInitializeOnLoadMethod", installerSource);
            StringAssert.Contains("new GalaxyAndroidJavaGateway", installerSource);
            StringAssert.Contains("new GalaxyFileEntitlementLedger", installerSource);
            StringAssert.Contains("UnavailableGalaxyReceiptVerifier", installerSource);

            var ledgerPath = Path.Combine(m_ProjectRoot, "galaxy-ledger.json");
            const string identity = "guest:ledger-fixture";
            var first = new GalaxyFileEntitlementLedger(ledgerPath);
            await first.PersistPendingAsync(
                identity,
                "jss.edition.explorer",
                3,
                CancellationToken.None);
            Assert.That(first.IsPendingItem(
                identity,
                "jss.edition.explorer"), Is.True);
            var authority = new GalaxyVerifiedAuthority(
                true,
                "ledger-purchase",
                "jss.edition.explorer",
                GalaxyPackage,
                "PRODUCTION",
                AccountHash(identity),
                ProfileHash(identity),
                "signed:ledger-purchase",
                DateTime.UtcNow);
            await first.PersistVerifiedAsync(
                identity,
                authority,
                new ContentId("explorer_edition"),
                CancellationToken.None);

            var reopened = new GalaxyFileEntitlementLedger(ledgerPath);
            Assert.That(reopened.IsKnownPurchase(
                identity,
                "ledger-purchase"), Is.True);
            Assert.That(reopened.IsPendingItem(
                identity,
                "jss.edition.explorer"), Is.False);
            var loaded = await reopened.LoadAuthoritiesAsync(
                identity,
                CancellationToken.None);
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(
                loaded[0].SignedAuthority,
                Is.EqualTo("signed:ledger-purchase"));
            var acknowledgements = await reopened
                .LoadPendingAcknowledgementsAsync(
                    identity,
                    CancellationToken.None);
            Assert.That(acknowledgements,
                Is.EquivalentTo(new[] { "ledger-purchase" }));
            await reopened.MarkAcknowledgedAsync(
                identity,
                "ledger-purchase",
                CancellationToken.None);
            var afterAcknowledge = new GalaxyFileEntitlementLedger(ledgerPath);
            Assert.That(await afterAcknowledge
                .LoadPendingAcknowledgementsAsync(
                    identity,
                    CancellationToken.None), Is.Empty);
        }

        [Test]
        public async Task GalaxyService_UntrustedReplayAndLateIdentityGrantNothing()
        {
            var events = new System.Collections.Generic.List<string>();
            var account = new FakeAccount("guest-a");
            var gateway = new FakeGalaxyGateway(events);
            var verifier = new FakeGalaxyVerifier(events);
            var ledger = new FakeGalaxyLedger(events);
            var service = new GalaxyStoreService(
                account,
                gateway,
                verifier,
                ledger);

            var startup = await service.InitializeAsync(CancellationToken.None);
            Assert.That(startup.IsAvailable, Is.True);
            Assert.That(gateway.OwnedCalls, Is.EqualTo(1),
                "Samsung requires GetOwnedList on every launch.");
            await service.GetProductsAsync(CancellationToken.None);

            gateway.NextPurchaseId = "purchase-untrusted";
            verifier.Verified = false;
            var untrusted = await service.PurchaseAsync(
                new ContentId("store.mirra-collection"),
                CancellationToken.None);
            Assert.That(untrusted.Status, Is.EqualTo(PurchaseStatus.Failed));
            Assert.That(ledger.VerifiedCount, Is.Zero);
            Assert.That(gateway.AcknowledgeCount, Is.Zero);

            gateway.NextPurchaseId = "purchase-verified";
            verifier.Verified = true;
            var verified = await service.PurchaseAsync(
                new ContentId("store.mirra-collection"),
                CancellationToken.None);
            Assert.That(verified.Status, Is.EqualTo(PurchaseStatus.Purchased));
            Assert.That(verified.Entitlements.Owns(
                new ContentId("mirra_collection")), Is.True);
            Assert.That(events.IndexOf("persist:purchase-verified"),
                Is.LessThan(events.IndexOf("ack:purchase-verified")));

            gateway.NextPurchaseId = "purchase-verified";
            var replay = await service.PurchaseAsync(
                new ContentId("store.mirra-collection"),
                CancellationToken.None);
            Assert.That(replay.Status, Is.EqualTo(PurchaseStatus.Failed));
            Assert.That(ledger.VerifiedCount, Is.EqualTo(1));
            Assert.That(gateway.AcknowledgeCount, Is.EqualTo(1));

            gateway.BlockPayment = true;
            gateway.NextPurchaseId = "purchase-late";
            var lateTask = service.PurchaseAsync(
                new ContentId("store.aster-veil-collection"),
                CancellationToken.None).AsTask();
            await gateway.PaymentStarted.Task;
            account.SetGuest("guest-b");
            gateway.CompletePayment();
            var late = await lateTask;
            Assert.That(late.Status, Is.EqualTo(PurchaseStatus.Failed));
            Assert.That(late.Entitlements.ActiveEntitlements, Is.Empty);
            Assert.That(ledger.VerifiedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GalaxyService_LaunchDoesNotAdoptUnknownButRestoreMayVerifyIt()
        {
            var events = new System.Collections.Generic.List<string>();
            var account = new FakeAccount("guest-a");
            var gateway = new FakeGalaxyGateway(events)
            {
                Owned = new[]
                {
                    new GalaxyNativePurchase(
                        GalaxyNativeStatus.Succeeded,
                        "owned-unknown",
                        "jss.edition.explorer",
                        AccountHash("guest:guest-a"),
                        ProfileHash("guest:guest-a")),
                },
            };
            var verifier = new FakeGalaxyVerifier(events) { Verified = true };
            var ledger = new FakeGalaxyLedger(events);
            var service = new GalaxyStoreService(
                account,
                gateway,
                verifier,
                ledger);

            await service.InitializeAsync(CancellationToken.None);
            Assert.That(verifier.VerifyCount, Is.Zero,
                "Launch may refresh only purchases already bound to this profile.");
            Assert.That(service.CurrentEntitlements.ActiveEntitlements, Is.Empty);

            var restored = await service.RestoreAsync(CancellationToken.None);
            Assert.That(verifier.VerifyCount, Is.EqualTo(1));
            Assert.That(restored.Owns(new ContentId("explorer_edition")), Is.True);
            Assert.That(gateway.AcknowledgeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GalaxyService_RestartRehydratesSignedAndPendingAuthority()
        {
            var events = new List<string>();
            var account = new FakeAccount("guest-a");
            var identity = "guest:guest-a";
            var cached = new GalaxyVerifiedAuthority(
                true,
                "cached-explorer",
                "jss.edition.explorer",
                GalaxyPackage,
                "PRODUCTION",
                AccountHash(identity),
                ProfileHash(identity),
                "signed:cached-explorer",
                DateTime.UtcNow);
            var ledger = new FakeGalaxyLedger(events);
            ledger.SeedAuthority(identity, cached);
            await ledger.PersistPendingAsync(
                identity,
                "jss.collection.mirra",
                0,
                CancellationToken.None);
            var gateway = new FakeGalaxyGateway(events)
            {
                Owned = new[]
                {
                    new GalaxyNativePurchase(
                        GalaxyNativeStatus.Succeeded,
                        "interrupted-mirra",
                        "jss.collection.mirra",
                        AccountHash(identity),
                        ProfileHash(identity)),
                },
            };
            var verifier = new FakeGalaxyVerifier(events) { Verified = true };
            var service = new GalaxyStoreService(
                account,
                gateway,
                verifier,
                ledger);

            await service.InitializeAsync(CancellationToken.None);

            Assert.That(verifier.CacheValidationCount, Is.EqualTo(1));
            Assert.That(verifier.VerifyCount, Is.EqualTo(1),
                "The owned entry matching the pending item must reconcile.");
            Assert.That(service.CurrentEntitlements.Owns(
                new ContentId("explorer_edition")), Is.True);
            Assert.That(service.CurrentEntitlements.Owns(
                new ContentId("mirra_collection")), Is.True);
        }

        [Test]
        public void GalaxyReleaseMap_KeepsExternalTransactionProofPending()
        {
            var mapPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "docs",
                "release",
                "galaxy-product-map.md"));
            Assert.That(File.Exists(mapPath), Is.True);
            var map = File.ReadAllText(mapPath);
            StringAssert.Contains(GalaxyPackage, map);
            StringAssert.Contains("Samsung Unity IAP 6.5.2", map);
            StringAssert.Contains("AcknowledgePurchases", map);
            StringAssert.Contains("never ConsumePurchasedItems", map);
            StringAssert.Contains("receipt verifier", map);
            StringAssert.Contains("physical Samsung", map);
            StringAssert.Contains("PENDING", map);
            StringAssert.DoesNotContain("galx_", map);
            StringAssert.DoesNotContain("verified in an emulator", map);
        }

        private static object[] Arguments(
            bool verified,
            string purchaseId,
            string itemId,
            string packageId,
            string mode,
            string accountId,
            string profileId,
            long expectedGeneration,
            long callbackGeneration,
            bool replayed,
            string signedAuthority) => new object[]
        {
            verified,
            purchaseId,
            itemId,
            packageId,
            mode,
            accountId,
            profileId,
            expectedGeneration,
            callbackGeneration,
            replayed,
            signedAuthority,
        };

        private string CreateGeneratedProject(string name)
        {
            var root = Path.Combine(m_ProjectRoot, name);
            var unityLibrary = Path.Combine(root, "unityLibrary");
            var manifestPath = Path.Combine(
                unityLibrary,
                "src",
                "main",
                "AndroidManifest.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.WriteAllText(
                manifestPath,
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">" +
                "<uses-permission android:name=\"com.android.vending.BILLING\" />" +
                "<application><activity " +
                "android:name=\"com.unity3d.player.UnityPlayerGameActivity\" " +
                "android:launchMode=\"singleTop\" /></application></manifest>");
            File.WriteAllText(
                Path.Combine(unityLibrary, "build.gradle"),
                "dependencies {\n" +
                "    implementation 'com.revenuecat.purchases:" +
                "purchases-hybrid-common:[18.33.1]'\n" +
                "    implementation 'com.android.billingclient:billing:8.0.0'\n" +
                "}\n" +
                "android {\n" +
                "    namespace 'com.scientificaj.fixture'\n" +
                "}\n");
            File.WriteAllText(
                Path.Combine(root, "settings.gradle"),
                "include ':launcher', ':unityLibrary'\n");
            return unityLibrary;
        }

        private static string AccountHash(string identity) =>
            Hash("jss-galaxy-account-v1", identity);

        private static string ProfileHash(string identity) =>
            Hash("jss-galaxy-profile-v1", identity);

        private static string Hash(string domain, string value)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    domain + "\n" + value)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private sealed class FakeAccount : IAccountService
        {
            private AccountState m_Current;

            public FakeAccount(string guestId)
            {
                SetGuest(guestId);
            }

            public AccountState Current => m_Current;
            public event Action<AccountState> StateChanged;

            public void SetGuest(string guestId)
            {
                m_Current = new AccountState(
                    AccountConnection.OfflineGuest,
                    AccountCapability.Offline,
                    AccountSyncState.LocalOnly,
                    AccountOperation.None,
                    guestId,
                    string.Empty,
                    string.Empty);
                StateChanged?.Invoke(m_Current);
            }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;
            public ValueTask<AccountLinkResult> LinkGoogleAsync(
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<AccountLinkResult> ResolveConflictAsync(
                AccountConflictChoice choice,
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<CloudSyncResult> SyncAsync(
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<AccountExportResult> ExportDataAsync(
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<AccountUnlinkResult> UnlinkGoogleAsync(
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask SignOutAsync(
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask DeleteAccountAsync(
                CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class FakeGalaxyGateway : IGalaxyIapGateway
        {
            private readonly System.Collections.Generic.List<string> m_Events;
            private TaskCompletionSource<GalaxyNativePurchase> m_Payment;

            public FakeGalaxyGateway(
                System.Collections.Generic.List<string> events)
            {
                m_Events = events;
            }

            public bool IsSupported => true;
            public string NextPurchaseId { get; set; } = "purchase";
            public bool BlockPayment { get; set; }
            public int OwnedCalls { get; private set; }
            public int AcknowledgeCount { get; private set; }
            public string LastAccountId { get; private set; }
            public string LastProfileId { get; private set; }
            public IReadOnlyList<GalaxyNativePurchase> Owned { get; set; } =
                Array.Empty<GalaxyNativePurchase>();
            public TaskCompletionSource<bool> PaymentStarted { get; private set; } =
                new TaskCompletionSource<bool>();

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<IReadOnlyList<GalaxyNativeProduct>>
                GetProductsDetailsAsync(
                    IReadOnlyList<string> itemIds,
                    CancellationToken cancellationToken) =>
                    new ValueTask<IReadOnlyList<GalaxyNativeProduct>>(
                        new[]
                        {
                            new GalaxyNativeProduct(
                                "jss.collection.mirra",
                                "Mirra",
                                "Optional Mirra cosmetics",
                                "$1.99",
                                "USD"),
                            new GalaxyNativeProduct(
                                "jss.collection.aster_veil",
                                "Aster & Veil",
                                "Optional Aster and Veil cosmetics",
                                "$1.99",
                                "USD"),
                        });

            public ValueTask<IReadOnlyList<GalaxyNativePurchase>>
                GetOwnedListAsync(CancellationToken cancellationToken)
            {
                OwnedCalls++;
                return new ValueTask<IReadOnlyList<GalaxyNativePurchase>>(Owned);
            }

            public ValueTask<GalaxyNativePurchase> StartPaymentAsync(
                string itemId,
                string obfuscatedAccountId,
                string obfuscatedProfileId,
                CancellationToken cancellationToken)
            {
                LastAccountId = obfuscatedAccountId;
                LastProfileId = obfuscatedProfileId;
                var result = new GalaxyNativePurchase(
                    GalaxyNativeStatus.Succeeded,
                    NextPurchaseId,
                    itemId,
                    obfuscatedAccountId,
                    obfuscatedProfileId);
                if (!BlockPayment)
                {
                    return new ValueTask<GalaxyNativePurchase>(result);
                }

                m_Payment = new TaskCompletionSource<GalaxyNativePurchase>();
                PaymentStarted.TrySetResult(true);
                return new ValueTask<GalaxyNativePurchase>(m_Payment.Task);
            }

            public void CompletePayment()
            {
                m_Payment.TrySetResult(new GalaxyNativePurchase(
                    GalaxyNativeStatus.Succeeded,
                    NextPurchaseId,
                    "jss.collection.aster_veil",
                    LastAccountId,
                    LastProfileId));
            }

            public ValueTask<bool> AcknowledgePurchasesAsync(
                string purchaseId,
                CancellationToken cancellationToken)
            {
                AcknowledgeCount++;
                m_Events.Add("ack:" + purchaseId);
                return new ValueTask<bool>(true);
            }
        }

        private sealed class FakeGalaxyVerifier : IGalaxyReceiptVerifier
        {
            private readonly System.Collections.Generic.List<string> m_Events;

            public FakeGalaxyVerifier(
                System.Collections.Generic.List<string> events)
            {
                m_Events = events;
            }

            public bool IsConfigured => true;
            public string Revision => "fake-v1";
            public bool Verified { get; set; }
            public int VerifyCount { get; private set; }
            public int CacheValidationCount { get; private set; }

            public ValueTask<GalaxyVerifiedAuthority> VerifyAsync(
                string purchaseId,
                CancellationToken cancellationToken)
            {
                VerifyCount++;
                m_Events.Add("verify:" + purchaseId);
                var itemId = purchaseId == "owned-unknown"
                    ? "jss.edition.explorer"
                    : purchaseId == "interrupted-mirra"
                        ? "jss.collection.mirra"
                    : purchaseId == "purchase-late"
                        ? "jss.collection.aster_veil"
                        : "jss.collection.mirra";
                return new ValueTask<GalaxyVerifiedAuthority>(
                    new GalaxyVerifiedAuthority(
                        Verified,
                        purchaseId,
                        itemId,
                        GalaxyPackage,
                        "PRODUCTION",
                        AccountHash("guest:guest-a"),
                        ProfileHash("guest:guest-a"),
                        Verified ? "signed:" + purchaseId : string.Empty,
                        DateTime.UtcNow));
            }

            public ValueTask<bool> ValidateCachedAuthorityAsync(
                GalaxyVerifiedAuthority authority,
                CancellationToken cancellationToken)
            {
                CacheValidationCount++;
                return new ValueTask<bool>(Verified);
            }
        }

        private sealed class FakeGalaxyLedger : IGalaxyEntitlementLedger
        {
            private readonly System.Collections.Generic.List<string> m_Events;
            private readonly System.Collections.Generic.HashSet<string> m_Purchases =
                new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            private readonly System.Collections.Generic.HashSet<string> m_PendingItems =
                new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            private readonly System.Collections.Generic.Dictionary<string,
                System.Collections.Generic.List<GalaxyVerifiedAuthority>> m_Authorities =
                new System.Collections.Generic.Dictionary<string,
                    System.Collections.Generic.List<GalaxyVerifiedAuthority>>(
                        StringComparer.Ordinal);

            public FakeGalaxyLedger(
                System.Collections.Generic.List<string> events)
            {
                m_Events = events;
            }

            public int VerifiedCount => m_Purchases.Count;
            public bool IsKnownPurchase(string identity, string purchaseId) =>
                m_Purchases.Contains(identity + "\n" + purchaseId);
            public bool IsPendingItem(string identity, string itemId) =>
                m_PendingItems.Contains(identity + "\n" + itemId);
            public bool IsReplayedPurchase(string purchaseId, string identity) =>
                m_Purchases.Contains(identity + "\n" + purchaseId);
            public ValueTask PersistPendingAsync(
                string identity,
                string itemId,
                long identityGeneration,
                CancellationToken cancellationToken)
            {
                m_PendingItems.Add(identity + "\n" + itemId);
                return default;
            }
            public ValueTask PersistVerifiedAsync(
                string identity,
                GalaxyVerifiedAuthority authority,
                ContentId entitlementId,
                CancellationToken cancellationToken)
            {
                m_Purchases.Add(identity + "\n" + authority.PurchaseId);
                if (!m_Authorities.TryGetValue(identity, out var authorities))
                {
                    authorities = new List<GalaxyVerifiedAuthority>();
                    m_Authorities.Add(identity, authorities);
                }
                authorities.RemoveAll(value => string.Equals(
                    value.PurchaseId,
                    authority.PurchaseId,
                    StringComparison.Ordinal));
                authorities.Add(authority);
                m_PendingItems.Remove(identity + "\n" + authority.ItemId);
                m_Events.Add("persist:" + authority.PurchaseId);
                return default;
            }
            public ValueTask<IReadOnlyList<GalaxyVerifiedAuthority>>
                LoadAuthoritiesAsync(
                    string identity,
                    CancellationToken cancellationToken)
            {
                return new ValueTask<IReadOnlyList<GalaxyVerifiedAuthority>>(
                    m_Authorities.TryGetValue(identity, out var authorities)
                        ? authorities.ToArray()
                        : Array.Empty<GalaxyVerifiedAuthority>());
            }
            public ValueTask<IReadOnlyList<string>>
                LoadPendingAcknowledgementsAsync(
                    string identity,
                    CancellationToken cancellationToken) =>
                    new ValueTask<IReadOnlyList<string>>(Array.Empty<string>());
            public ValueTask MarkAcknowledgedAsync(
                string identity,
                string purchaseId,
                CancellationToken cancellationToken) => default;
            public ValueTask ClearPendingAsync(
                string identity,
                CancellationToken cancellationToken)
            {
                m_PendingItems.RemoveWhere(value => value.StartsWith(
                    identity + "\n",
                    StringComparison.Ordinal));
                return default;
            }

            public void SeedAuthority(
                string identity,
                GalaxyVerifiedAuthority authority)
            {
                if (!m_Authorities.TryGetValue(identity, out var authorities))
                {
                    authorities = new List<GalaxyVerifiedAuthority>();
                    m_Authorities.Add(identity, authorities);
                }
                authorities.Add(authority);
                m_Purchases.Add(identity + "\n" + authority.PurchaseId);
            }
        }
    }
}
