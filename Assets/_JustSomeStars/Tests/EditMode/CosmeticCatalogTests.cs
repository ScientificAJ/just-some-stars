using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CosmeticCatalogTests
    {
        private const string CatalogPath =
            "Assets/_JustSomeStars/Content/Cosmetics/CosmeticCatalog.asset";

        [Test]
        public void LaunchCatalogue_IsCompleteDistinctAndVisuallyBound()
        {
            var catalog = LoadCatalog();

            Assert.DoesNotThrow(catalog.ValidateOrThrow);
            Assert.That(catalog.Items.Count, Is.EqualTo(128));
            Assert.That(
                catalog.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(128));
            Assert.That(
                catalog.Items.Select(item => item.Category).Distinct().Count(),
                Is.EqualTo(7));
            Assert.That(catalog.Items.Count(item => item.CanBeEarned), Is.GreaterThanOrEqualTo(64));
            Assert.That(catalog.Items.All(item => item.Icon != null), Is.True);
            Assert.That(catalog.Items.All(item => item.PresentationSprite != null), Is.True);
            Assert.That(catalog.Items.Select(item => item.PresentationSprite.GetInstanceID())
                .Distinct().Count(), Is.EqualTo(128));
            Assert.That(catalog.Items.Select(item => item.PresentationEffectId)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(128));
            Assert.That(catalog.Items.All(item =>
                !string.IsNullOrWhiteSpace(item.PresentationAssetPath) &&
                item.PresentationAssetPath.Contains(
                    "/PresentationAtlases/", StringComparison.Ordinal) &&
                item.CompatibleClipIds.Count > 0 &&
                item.CompatibleFrameEvents.Count > 0), Is.True);
        }

        [Test]
        public void LaunchPacks_UseFrozenIdentifiersAndRequiredCounts()
        {
            var catalog = LoadCatalog();

            Assert.That(catalog.Items.Count(item =>
                item.OwnershipSource == CosmeticOwnershipSource.Edition &&
                item.PackId == "explorer_edition"), Is.InRange(25, 35));
            Assert.That(catalog.Items.Count(item =>
                item.OwnershipSource == CosmeticOwnershipSource.Edition &&
                item.PackId == "founders_constellation"), Is.InRange(40, 50));
            Assert.That(catalog.Find("birthday.ori-starlight.2026"), Is.Not.Null);
            Assert.That(catalog.Items.Where(item =>
                item.OwnershipSource == CosmeticOwnershipSource.IndividualPurchase)
                .All(item => item.ProductId.StartsWith(
                    "jss.cosmetic.", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void CaptainSilhouettes_FitEveryFamilyAndResolveToRealModuleAtlases()
        {
            var catalog = LoadCatalog();
            var families = new[]
            {
                ("Compact", "compact"),
                ("Average", "average"),
                ("TallBroad", "tallbroad"),
            };

            foreach (var item in catalog.Items.Where(item =>
                         item.Category == CosmeticCategory.Captain &&
                         item.SilhouetteChanging))
            {
                Assert.That(item.BodyFits.Distinct().Count(), Is.EqualTo(3), item.Id);
                foreach (var family in families)
                {
                    foreach (var facing in new[] { "left", "right" })
                    {
                        var path = item.AttachmentAssetPath
                            .Replace("{body-family-title}", family.Item1)
                            .Replace("{body-family-slug}", family.Item2)
                            .Replace("{facing}", facing);
                        Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                            Is.Not.Null, $"{item.Id}: {path}");
                        var maskPath = item.PaletteMaskPath
                            .Replace("{body-family-title}", family.Item1)
                            .Replace("{body-family-slug}", family.Item2)
                            .Replace("{facing}", facing);
                        Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath),
                            Is.Not.Null, $"{item.Id}: {maskPath}");
                    }
                }
            }
        }

        [Test]
        public async Task EveryOwnershipRoute_EquipsAcrossCategoriesAndCloudRoundTrips()
        {
            var catalog = LoadCatalog();
            var presentation = new CosmeticPresentationService(catalog);
            var targets = Enum.GetValues(typeof(CosmeticCategory))
                .Cast<CosmeticCategory>()
                .ToDictionary(
                    category => category,
                    category => new RecordingPresentationTarget(category));
            foreach (var target in targets.Values)
            {
                presentation.Register(target);
            }
            var resolver = new OwnershipResolver(catalog, presentation);
            var save = GameSave.CreateNew("save.cosmetics-cloud", 100);
            long ticks = 200;

            foreach (var category in Enum.GetValues(typeof(CosmeticCategory))
                         .Cast<CosmeticCategory>())
            {
                var earned = catalog.Items.First(item =>
                    item.Category == category &&
                    item.OwnershipSource == CosmeticOwnershipSource.Earned);
                save.EarnedCosmeticIds = save.EarnedCosmeticIds
                    .Append(earned.Id)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                Assert.That(resolver.Resolve(
                    earned.Id,
                    save,
                    EntitlementSnapshot.Empty).Owned, Is.True, earned.Id);
                resolver.Equip(
                    category,
                    earned.Id,
                    save,
                    EntitlementSnapshot.Empty,
                    ticks++);

                var edition = catalog.Items.First(item =>
                    item.Category == category &&
                    item.OwnershipSource == CosmeticOwnershipSource.Edition);
                Assert.That(resolver.Resolve(
                    edition.Id,
                    save,
                    Verified(edition.PackId)).Owned, Is.True, edition.Id);
                resolver.Equip(
                    category,
                    edition.Id,
                    save,
                    Verified(edition.PackId),
                    ticks++);

                var paid = catalog.Items.First(item =>
                    item.Category == category &&
                    item.OwnershipSource == CosmeticOwnershipSource.IndividualPurchase);
                var restored = Verified(paid.ProductId);
                Assert.That(resolver.Resolve(
                    paid.Id,
                    save,
                    restored).Owned, Is.True, paid.Id);
                resolver.Equip(
                    category,
                    paid.Id,
                    save,
                    restored,
                    ticks++);
                Assert.That(targets[category].LastBinding.Definition.Id,
                    Is.EqualTo(paid.Id));
                Assert.That(targets[category].LastBinding.Sprite,
                    Is.SameAs(paid.PresentationSprite));
                Assert.That(CommerceProductMap.IsAllowedEntitlement(paid.ProductId),
                    Is.True, paid.ProductId);
                Assert.That(GalaxyProductMap.TryEntitlement(
                    paid.ProductId,
                    out var galaxyEntitlement), Is.True, paid.ProductId);
                Assert.That(galaxyEntitlement.Value, Is.EqualTo(paid.ProductId));
            }

            var birthday = catalog.Find("birthday.ori-starlight.2026");
            save.EarnedCosmeticIds = save.EarnedCosmeticIds
                .Append(birthday.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.That(resolver.Resolve(
                birthday.Id,
                save,
                EntitlementSnapshot.Empty).Source,
                Is.EqualTo(CosmeticOwnershipSource.Birthday));
            resolver.Equip(
                CosmeticCategory.Ori,
                birthday.Id,
                save,
                EntitlementSnapshot.Empty,
                ticks);

            var gateway = new MemoryFirestoreGateway();
            var cloud = new FirestoreCloudSaveService(gateway);
            await cloud.InitializeAsync(CancellationToken.None);
            await cloud.UploadAsync("player-cosmetics", save, CancellationToken.None);
            var downloaded = await cloud.DownloadAsync(
                "player-cosmetics",
                CancellationToken.None);

            Assert.That(downloaded.HasValue, Is.True);
            Assert.That(downloaded.Value.Save.CosmeticLoadout,
                Is.EqualTo(save.CosmeticLoadout));
            Assert.That(downloaded.Value.Save.EarnedCosmeticIds,
                Is.EquivalentTo(save.EarnedCosmeticIds));
        }

        [Test]
        public void OwnershipPrecedence_AndEquipPersistenceCoverEveryCategory()
        {
            var catalog = LoadCatalog();
            var resolver = new OwnershipResolver(catalog);
            var save = GameSave.CreateNew("save.cosmetics", 10);
            var earnedEdition = catalog.Items.First(item =>
                item.OwnershipSource == CosmeticOwnershipSource.Edition &&
                item.CanBeEarned);
            save.EarnedCosmeticIds = new[]
            {
                earnedEdition.Id,
                "birthday.ori-starlight.2026",
            };
            var verified = Verified(
                earnedEdition.PackId,
                "complete_launch_collection");

            Assert.That(resolver.Resolve(earnedEdition.Id, save, verified).Source,
                Is.EqualTo(CosmeticOwnershipSource.Earned));
            Assert.That(resolver.Resolve("birthday.ori-starlight.2026", save, verified).Source,
                Is.EqualTo(CosmeticOwnershipSource.Birthday));

            long ticks = 20;
            foreach (var category in Enum.GetValues(typeof(CosmeticCategory))
                         .Cast<CosmeticCategory>())
            {
                var free = catalog.Items.First(item =>
                    item.Category == category &&
                    item.OwnershipSource == CosmeticOwnershipSource.Free);
                resolver.Equip(category, free.Id, save, EntitlementSnapshot.Empty, ticks++);
                Assert.That(save.CosmeticLoadout.Selected(category), Is.EqualTo(free.Id));
            }

            var document = new JsonSaveSerializer(SaveMigrator.CreateCurrent()).Serialize(save);
            Assert.That(new JsonSaveSerializer(SaveMigrator.CreateCurrent())
                .TryDeserialize(document, out var restored), Is.True);
            Assert.That(restored.CosmeticLoadout, Is.EqualTo(save.CosmeticLoadout));
        }

        [Test]
        public void SchemaV4_MigratesToCurrentLoadoutAndCloudMergeKeepsLatestEquip()
        {
            var serializer = new JsonSaveSerializer(SaveMigrator.CreateCurrent());
            var current = GameSave.CreateNew("save.cosmetic-migration", 10);
            var v5 = serializer.Serialize(current);
            var v4 = Regex.Replace(
                v5.Replace("\"schemaVersion\": 5", "\"schemaVersion\": 4"),
                ",\\s*\"cosmeticLoadout\"\\s*:\\s*\\{[^}]*\\}",
                string.Empty);

            Assert.That(serializer.TryDeserialize(v4, out var migrated), Is.True);
            Assert.That(migrated.SchemaVersion, Is.EqualTo(GameSave.CurrentSchemaVersion));
            Assert.That(migrated.CosmeticLoadout.Captain,
                Is.EqualTo("cosmetic.captain.clubhouse-canvas"));

            var cloud = migrated.Copy();
            cloud.CosmeticLoadout.Set(
                CosmeticCategory.Lens,
                "cosmetic.lens.clubhouse-constellation",
                99);
            var merged = SaveMerge.Combine(migrated, cloud);
            Assert.That(merged.CosmeticLoadout.LastEquippedUtcTicks, Is.EqualTo(99));
        }

        private static CosmeticCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        private static EntitlementSnapshot Verified(params string[] entitlements) =>
            new EntitlementSnapshot(
                "player.cosmetics",
                "fingerprint.cosmetics",
                StoreEnvironment.GooglePlay,
                "com.scientificaj.justsomestars",
                EntitlementVerification.Verified,
                EntitlementSource.CustomerInfo,
                new DateTime(638923680000000000L, DateTimeKind.Utc),
                entitlements.Select(value => new ContentId(value)));

        private sealed class MemoryFirestoreGateway : IFirestoreDocumentGateway
        {
            private readonly Dictionary<string, string> m_Documents =
                new(StringComparer.Ordinal);

            public bool IsConfigured => true;

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<string> ReadAsync(
                string documentPath,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                m_Documents.TryGetValue(documentPath, out var document);
                return new ValueTask<string>(document);
            }

            public ValueTask WriteAsync(
                string documentPath,
                FirestoreDocumentWrite document,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                m_Documents[documentPath] = document.PayloadJson;
                return default;
            }

            public ValueTask<CloudCommitResult> WriteIfVersionAsync(
                string documentPath,
                FirestoreDocumentWrite document,
                CloudSaveVersion expectedRemote,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                m_Documents[documentPath] = document.PayloadJson;
                return new ValueTask<CloudCommitResult>(new CloudCommitResult(
                    true,
                    false,
                    new CloudSaveVersion(expectedRemote.Revision + 1, "cosmetics")));
            }

            public ValueTask DeleteAsync(
                string documentPath,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                m_Documents.Remove(documentPath);
                return default;
            }
        }

        private sealed class RecordingPresentationTarget :
            ICosmeticPresentationTarget
        {
            public RecordingPresentationTarget(CosmeticCategory category)
            {
                Category = category;
            }

            public CosmeticCategory Category { get; }
            public CosmeticPresentationBinding LastBinding { get; private set; }

            public void Apply(CosmeticPresentationBinding binding)
            {
                LastBinding = binding;
            }
        }
    }
}
