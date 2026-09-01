using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CloudSaveMergeTests
    {
        [Test]
        public void Merge_PreservesFurthestProgressAndUnionsPlayerCollections()
        {
            var local = CreateSave("save.local", checkpoint: 7, customizedAt: 900);
            local.DiscoveryIds = new[] { "mirra.wind", "shared" };
            local.EarnedCosmeticIds = new[] { "cosmetic.local", "shared" };
            local.AtlasEntryIds = new[] { "atlas.local", "shared" };
            local.Photographs = new[]
            {
                new PhotoMetadata
                {
                    PhotoId = "photo.local",
                    CapturedUtcTicks = 50,
                    RelativePath = "Photos/mirra-local.jpg",
                },
            };

            var cloud = CreateSave("save.cloud", checkpoint: 9, customizedAt: 800);
            cloud.DiscoveryIds = new[] { "koro.geyser", "shared" };
            cloud.EarnedCosmeticIds = new[] { "cosmetic.cloud", "shared" };
            cloud.AtlasEntryIds = new[] { "atlas.cloud", "shared" };
            cloud.Photographs = new[]
            {
                new PhotoMetadata
                {
                    PhotoId = "photo.cloud",
                    CapturedUtcTicks = 60,
                    RelativePath = "Photos/koro-cloud.jpg",
                },
            };

            var merged = SaveMerge.Combine(local, cloud);

            Assert.That(merged.Story.CheckpointOrdinal, Is.EqualTo(9));
            Assert.That(
                merged.DiscoveryIds,
                Is.EqualTo(new[] { "koro.geyser", "mirra.wind", "shared" }));
            Assert.That(
                merged.EarnedCosmeticIds,
                Is.EqualTo(new[] { "cosmetic.cloud", "cosmetic.local", "shared" }));
            Assert.That(
                merged.AtlasEntryIds,
                Is.EqualTo(new[] { "atlas.cloud", "atlas.local", "shared" }));
            Assert.That(
                merged.Captain.AppearancePresetId,
                Is.EqualTo(local.Captain.AppearancePresetId),
                "The newest explicit local appearance must win.");
            Assert.That(
                merged.Photographs,
                Has.Length.EqualTo(1),
                "Chapter One photos remain device-local rather than cloud-merged.");
            Assert.That(merged.Photographs[0].PhotoId, Is.EqualTo("photo.local"));
        }

        [Test]
        public async Task FirestoreService_RoundTripsVersionedSaveUnderExactUidDocument()
        {
            var gateway = new MemoryFirestoreGateway();
            var service = new FirestoreCloudSaveService(gateway);
            var initialized = await service.InitializeAsync(CancellationToken.None);
            var original = CreateSave("save.guest", checkpoint: 4, customizedAt: 400);

            await service.UploadAsync("firebase-user-123", original, CancellationToken.None);
            var downloaded = await service.DownloadAsync(
                "firebase-user-123",
                CancellationToken.None);

            Assert.That(initialized.IsAvailable, Is.True);
            Assert.That(gateway.LastPath, Is.EqualTo("users/firebase-user-123"));
            Assert.That(downloaded.HasValue, Is.True);
            Assert.That(downloaded.Value.SchemaVersion, Is.EqualTo(GameSave.CurrentSchemaVersion));
            Assert.That(downloaded.Value.Save, Is.EqualTo(original));
            Assert.That(downloaded.Value.Save, Is.Not.SameAs(original));
            Assert.That(gateway.Documents[gateway.LastPath], Does.Contain("schemaVersion"));
        }

        [Test]
        public async Task FirestoreService_MigratesSchemaV2CloudProjectionBeforeValidation()
        {
            var gateway = new MemoryFirestoreGateway();
            var service = new FirestoreCloudSaveService(gateway);
            var original = CreateSave("save.cloud-v2", checkpoint: 2, customizedAt: 200);
            original.Birthday = new BirthdayState
            {
                HasValue = true,
                Day = 4,
                Month = 7,
                Year = 2013,
                LastBirthdayGiftYear = 2025,
            };
            await service.UploadAsync("firebase-user-v2", original, CancellationToken.None);
            gateway.Documents[gateway.LastPath] = gateway.Documents[gateway.LastPath]
                .Replace("\"schemaVersion\": 4", "\"schemaVersion\": 2")
                .Replace("    \"correctionCount\": 0,\n", string.Empty);

            var downloaded = await service.DownloadAsync(
                "firebase-user-v2",
                CancellationToken.None);

            Assert.That(downloaded.HasValue, Is.True);
            Assert.That(downloaded.Value.Save.SchemaVersion,
                Is.EqualTo(GameSave.CurrentSchemaVersion));
            Assert.That(downloaded.Value.Save.Birthday.CorrectionCount, Is.Zero);
            Assert.That(downloaded.Value.Save.Birthday.LastBirthdayGiftYear,
                Is.EqualTo(2025));
        }

        [Test]
        public void MergeBirthday_PreservesConsumedCorrectionAllowance()
        {
            var local = CreateSave("save.local-birthday", checkpoint: 1, customizedAt: 100);
            local.Birthday = new BirthdayState
            {
                HasValue = true,
                Day = 4,
                Month = 7,
                Year = 2013,
                CorrectionCount = 0,
                LastBirthdayGiftYear = 2025,
            };
            var cloud = local.Copy();
            cloud.Metadata.SaveId = "save.cloud-birthday";
            cloud.Birthday.CorrectionCount = 1;
            cloud.Birthday.LastBirthdayGiftYear = 2026;

            var merged = SaveMerge.Combine(local, cloud);

            Assert.That(merged.Birthday.CorrectionCount, Is.EqualTo(1));
            Assert.That(merged.Birthday.LastBirthdayGiftYear, Is.EqualTo(2026));
        }

        [Test]
        public void ResolveBirthdayConflict_PreservesMonotonicCorrectionAndGiftHistory()
        {
            var local = CreateSave("save.local-conflict", checkpoint: 1, customizedAt: 100);
            local.Birthday = new BirthdayState
            {
                HasValue = true,
                Day = 4,
                Month = 7,
                Year = 2013,
                CorrectionCount = 0,
                LastBirthdayGiftYear = 2025,
            };
            var cloud = local.Copy();
            cloud.Metadata.SaveId = "save.cloud-conflict";
            cloud.Birthday.Day = 5;
            cloud.Birthday.CorrectionCount = 2;
            cloud.Birthday.LastBirthdayGiftYear = 2026;

            var resolved = SaveMerge.ResolveConflict(
                local,
                cloud,
                preferLocal: true);

            Assert.That(resolved.Birthday.Day, Is.EqualTo(4));
            Assert.That(resolved.Birthday.CorrectionCount, Is.EqualTo(2));
            Assert.That(resolved.Birthday.LastBirthdayGiftYear, Is.EqualTo(2026));
        }

        [Test]
        public void CloudSnapshot_PreservesObservedRevisionZeroUpdateToken()
        {
            var save = CreateSave("save.revision-zero", checkpoint: 0, customizedAt: 100);
            save.Metadata.Revision = 0;
            var observed = new CloudSaveVersion(0, "observed-revision-zero-token");

            var snapshot = new CloudSaveSnapshot(save, version: observed);

            Assert.That(snapshot.Version.Revision, Is.EqualTo(0));
            Assert.That(
                snapshot.Version.UpdateToken,
                Is.EqualTo("observed-revision-zero-token"));
        }

        [Test]
        public async Task FirestoreService_WritesRulesCompatibleProjectionAndTimestampPolicy()
        {
            var gateway = new MemoryFirestoreGateway();
            var service = new FirestoreCloudSaveService(gateway);
            await service.InitializeAsync(CancellationToken.None);
            var save = CreateSave("save.rules-shape", checkpoint: 0, customizedAt: 100);

            await service.UploadAsync("uid-rules-shape", save, CancellationToken.None);

            var document = gateway.Documents[gateway.LastPath];
            Assert.That(document, Does.Not.Contain("photographs"));
            Assert.That(gateway.LastWrite.SetCreatedAtOnCreate, Is.True);
            Assert.That(
                gateway.LastWrite.RequiresServerAuthoritativeCreate,
                Is.True);
            Assert.That(gateway.LastWrite.PreserveCreatedAtOnUpdate, Is.True);
            Assert.That(
                gateway.LastWrite.PreserveServerOwnedBirthdayGiftYearsOnUpdate,
                Is.True);
            Assert.That(gateway.LastWrite.SetUpdatedAtToServerTime, Is.True);
            Assert.That(document, Does.Contain("schemaVersion"));
        }

        [Test]
        public async Task FirestoreService_RejectsInvalidUidAndForeignOrMalformedDocuments()
        {
            var gateway = new MemoryFirestoreGateway();
            var service = new FirestoreCloudSaveService(gateway);
            await service.InitializeAsync(CancellationToken.None);

            Assert.That(
                Assert.ThrowsAsync<ArgumentException>(async () =>
                    await service.DownloadAsync("../other-user", CancellationToken.None)),
                Is.Not.Null);
            Assert.That(gateway.ReadCount, Is.EqualTo(0));

            gateway.Documents["users/firebase-user-123"] =
                "{\"documentSchemaVersion\":999,\"save\":{}}";
            Assert.That(
                Assert.ThrowsAsync<System.IO.InvalidDataException>(async () =>
                    await service.DownloadAsync(
                        "firebase-user-123",
                        CancellationToken.None)),
                Is.Not.Null);
        }

        [Test]
        public async Task FirestoreService_ExportsButDirectClientDeleteFailsClosed()
        {
            var gateway = new MemoryFirestoreGateway();
            var service = new FirestoreCloudSaveService(gateway);
            await service.InitializeAsync(CancellationToken.None);
            var save = CreateSave("save.export", checkpoint: 3, customizedAt: 300);
            await service.UploadAsync("uid-export", save, CancellationToken.None);

            var exported = await service.ExportAsync(
                "uid-export",
                CancellationToken.None);
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.DeleteAsync("uid-export", CancellationToken.None));

            Assert.That(exported, Does.Contain("save.export"));
            Assert.That(gateway.LastPath, Is.EqualTo("users/uid-export"));
            Assert.That(gateway.Documents, Does.ContainKey("users/uid-export"));
            Assert.That(gateway.DeleteCount, Is.Zero);
        }

        [Test]
        public void FirestoreService_RejectsIdentifiersOutsideTheRulesContract()
        {
            var gateway = new MemoryFirestoreGateway();
            var service = new FirestoreCloudSaveService(gateway);
            var save = CreateSave("save.bounds", checkpoint: 1, customizedAt: 100);
            save.DiscoveryIds = new[]
            {
                "discovery.valid",
                new string('x', 129),
            };

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.UploadAsync(
                    "uid-bounds",
                    save,
                    CancellationToken.None));
            Assert.That(gateway.Documents, Is.Empty);
        }

        private static GameSave CreateSave(
            string saveId,
            int checkpoint,
            long customizedAt)
        {
            var save = GameSave.CreateNew(saveId, createdUtcTicks: 100);
            save.Story.CheckpointId = $"checkpoint.{checkpoint}";
            save.Story.CheckpointOrdinal = checkpoint;
            save.Captain.AppearancePresetId = $"appearance.{customizedAt}";
            save.Captain.LastCustomizedUtcTicks = customizedAt;
            save.Metadata.Revision = checkpoint;
            save.Metadata.UpdatedUtcTicks = 100 + checkpoint;
            return save;
        }

        private sealed class MemoryFirestoreGateway : IFirestoreDocumentGateway
        {
            public Dictionary<string, string> Documents { get; } =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public string LastPath { get; private set; }

            public int ReadCount { get; private set; }

            public int DeleteCount { get; private set; }

            public FirestoreDocumentWrite LastWrite { get; private set; }

            public bool IsConfigured => true;

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(
                    StartupResult.Available());
            }

            public ValueTask<string> ReadAsync(
                string documentPath,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastPath = documentPath;
                ReadCount++;
                Documents.TryGetValue(documentPath, out var value);
                return new ValueTask<string>(value);
            }

            public ValueTask WriteAsync(
                string documentPath,
                FirestoreDocumentWrite document,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastPath = documentPath;
                LastWrite = document;
                Documents[documentPath] = document.PayloadJson;
                return default;
            }

            public ValueTask<CloudCommitResult> WriteIfVersionAsync(
                string documentPath,
                FirestoreDocumentWrite document,
                CloudSaveVersion expectedRemote,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastPath = documentPath;
                LastWrite = document;
                Documents[documentPath] = document.PayloadJson;
                return new ValueTask<CloudCommitResult>(new CloudCommitResult(
                    committed: true,
                    versionMismatch: false,
                    new CloudSaveVersion(expectedRemote.Revision + 1, "committed")));
            }

            public ValueTask DeleteAsync(
                string documentPath,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastPath = documentPath;
                DeleteCount++;
                Documents.Remove(documentPath);
                return default;
            }

            public ValueTask ShutdownAsync()
            {
                return default;
            }
        }
    }
}
