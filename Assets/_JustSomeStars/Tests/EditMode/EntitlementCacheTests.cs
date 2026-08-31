using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class EntitlementCacheTests
    {
        private const string User = "$RCAnonymousID:test-user";
        private const string Fingerprint = "sha256:test-app";
        private const string Package = "com.scientificaj.justsomestars";
        private string m_Root;
        private string m_Path;
        private OfflineEntitlementCache m_Cache;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "JssTask23Entitlements",
                Guid.NewGuid().ToString("N"));
            m_Path = Path.Combine(m_Root, "verified.json");
            m_Cache = new OfflineEntitlementCache(m_Path);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }

        [Test]
        public void Cache_VerifiedSnapshotReplacesNotUnions()
        {
            m_Cache.ReplaceVerified(Snapshot(
                EntitlementVerification.Verified,
                "explorer_edition",
                "mirra_collection"));
            Assert.That(LoadIds(), Is.EqualTo(new[]
            {
                "explorer_edition",
                "mirra_collection",
            }));

            m_Cache.ReplaceVerified(Snapshot(
                EntitlementVerification.VerifiedOnDevice,
                "mirra_collection"));
            Assert.That(LoadIds(), Is.EqualTo(new[] { "mirra_collection" }),
                "A verified refresh must revoke an entitlement missing from the " +
                "latest exact CustomerInfo projection.");

            m_Cache.ReplaceVerified(Snapshot(EntitlementVerification.Verified));
            Assert.That(LoadIds(), Is.Empty,
                "A verified empty snapshot must revoke every cached entitlement.");
        }

        [Test]
        public void Cache_TransientFailureAndIdentityEnvironmentMismatchFailClosed()
        {
            m_Cache.ReplaceVerified(Snapshot(
                EntitlementVerification.Verified,
                "explorer_edition"));

            Assert.That(LoadIds(), Is.EqualTo(new[] { "explorer_edition" }),
                "Not writing on transient failure must preserve the prior exact set.");
            Assert.That(m_Cache.Load(
                "different-user",
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package), Is.Null);
            Assert.That(m_Cache.Load(
                User,
                "different-app",
                StoreEnvironment.RevenueCatTestStore,
                Package), Is.Null);
            Assert.That(m_Cache.Load(
                User,
                Fingerprint,
                StoreEnvironment.GooglePlay,
                Package), Is.Null);
            Assert.That(m_Cache.Load(
                User,
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package + ".other"), Is.Null);
        }

        [Test]
        public void Cache_CorruptPrimaryRecoversExactBackupAndFutureSchemaFailsClosed()
        {
            m_Cache.ReplaceVerified(Snapshot(
                EntitlementVerification.Verified,
                "explorer_edition"));
            m_Cache.ReplaceVerified(Snapshot(
                EntitlementVerification.Verified,
                "mirra_collection"));
            File.WriteAllText(m_Path, "{corrupt");

            Assert.That(LoadIds(), Is.EqualTo(new[] { "explorer_edition" }),
                "A corrupt newest file may recover only the prior complete backup.");

            m_Cache.Clear();
            Directory.CreateDirectory(m_Root);
            File.WriteAllText(m_Path, "{\"schemaVersion\":99}");
            Assert.That(m_Cache.Load(
                User,
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package), Is.Null,
                "A future cache schema must fail closed.");
        }

        [Test]
        public void Cache_UnverifiedInputRejectedAndRefreshMarkerIsIdentityBound()
        {
            Assert.Throws<InvalidOperationException>(() =>
                m_Cache.ReplaceVerified(Snapshot(
                    EntitlementVerification.NotRequested,
                    "explorer_edition")));
            Assert.Throws<InvalidOperationException>(() =>
                m_Cache.ReplaceVerified(Snapshot(
                    EntitlementVerification.Failed,
                    "explorer_edition")));

            m_Cache.MarkRefreshRequired(
                User,
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package);
            Assert.That(m_Cache.IsRefreshRequired(
                User,
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package), Is.True);
            Assert.That(m_Cache.IsRefreshRequired(
                "different-user",
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package), Is.False);
            m_Cache.ClearRefreshRequired();
            Assert.That(m_Cache.IsRefreshRequired(
                User,
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package), Is.False);
        }

        [Test]
        public void CommerceContracts_AreProviderNeutralAndNeverReferenceGameSave()
        {
            var runtime = typeof(IStoreService).Assembly;
            Assert.That(typeof(IStoreService).GetInterfaces(),
                Does.Contain(typeof(IGameService)));
            Assert.That(typeof(IStoreService).GetMethod("GetProductsAsync"), Is.Not.Null);
            Assert.That(typeof(IStoreService).GetMethod("PurchaseAsync"), Is.Not.Null);
            Assert.That(typeof(IStoreService).GetMethod("RestoreAsync"), Is.Not.Null);
            Assert.That(
                typeof(IStoreService).GetMethod("RefreshEntitlementsAsync"),
                Is.Not.Null);
            Assert.That(
                typeof(RevenueCatStoreService)
                    .GetFields(BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .Select(field => field.FieldType.FullName),
                Has.None.EqualTo(
                    "JustSomeStars.Runtime.Saving.GameSave"),
                "Paid ownership must never enter the cloud-unioned earned-save field.");
            Assert.That(
                runtime.GetReferencedAssemblies()
                    .Select(reference => reference.Name),
                Has.None.EqualTo("revenuecat.purchases-unity"),
                "The provider-neutral runtime assembly must not depend directly " +
                "on RevenueCat SDK types.");
        }

        [Test]
        public void RevenueCatPackage_IsPinnedOfficialAndManifestSupportsResume()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            var packageManifest = File.ReadAllText(Path.Combine(
                projectRoot,
                "Packages",
                "manifest.json"));
            var packageLock = File.ReadAllText(Path.Combine(
                projectRoot,
                "Packages",
                "packages-lock.json"));
            var androidManifest = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Plugins",
                "Android",
                "AndroidManifest.xml"));

            StringAssert.Contains(
                "com.revenuecat.purchases-unity-9.9.1.tgz",
                packageManifest);
            StringAssert.Contains(
                "com.revenuecat.purchases-unity",
                packageLock);
            StringAssert.Contains("9.9.1", packageLock);
            StringAssert.Contains(
                "android:launchMode=\"singleTop\"",
                androidManifest);
            StringAssert.DoesNotContain(
                "android:launchMode=\"singleTask\"",
                androidManifest);

            var packageArchive = Path.Combine(
                projectRoot,
                "Packages",
                "RevenueCatPackages",
                "com.revenuecat.purchases-unity-9.9.1.tgz");
            Assert.That(File.Exists(packageArchive), Is.True);
            using var stream = File.OpenRead(packageArchive);
            using var sha256 = SHA256.Create();
            var archiveHash = string.Concat(
                sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
            Assert.That(
                archiveHash,
                Is.EqualTo(
                    "6014a539443b8c2f2c1baf834476957ee891e06730781a1574efbffd2333d313"));

            var bridgeAssembly = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_JustSomeStars",
                "Runtime",
                "Commerce",
                "RevenueCatGoogle",
                "JustSomeStars.RevenueCatGoogle.asmdef"));
            StringAssert.Contains("revenuecat.purchases-unity", bridgeAssembly);
            StringAssert.Contains("!JSS_GALAXY", bridgeAssembly);

            var bridgeSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_JustSomeStars",
                "Runtime",
                "Commerce",
                "RevenueCatGoogle",
                "RevenueCatUnityGateway.cs"));
            StringAssert.Contains(
                "RefreshCustomerInfoFromSdk",
                bridgeSource,
                "Unsolicited SDK callbacks must trigger a fresh identity-bound " +
                "CustomerInfo fetch instead of relabelling the raw callback.");
            StringAssert.DoesNotContain(
                "CustomerInfoUpdated?.Invoke(Project(info))",
                bridgeSource);
            StringAssert.Contains(
                "Project(info, expectedAppUserId)",
                bridgeSource);
            Assert.That(
                Directory.Exists(Path.Combine(
                    Application.dataPath,
                    "_JustSomeStars",
                    "GeneratedCommerce")),
                Is.False,
                "A public runtime key may exist only under the transactional " +
                "build lease and must never remain in the worktree.");
        }

        private EntitlementSnapshot Snapshot(
            EntitlementVerification verification,
            params string[] entitlements) => new EntitlementSnapshot(
                User,
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package,
                verification,
                EntitlementSource.CustomerInfo,
                new DateTime(638922816000000000L, DateTimeKind.Utc),
                entitlements.Select(value => new ContentId(value)));

        private string[] LoadIds() => m_Cache.Load(
                User,
                Fingerprint,
                StoreEnvironment.RevenueCatTestStore,
                Package)
            .ActiveEntitlements
            .Select(value => value.Value)
            .ToArray();
    }
}
