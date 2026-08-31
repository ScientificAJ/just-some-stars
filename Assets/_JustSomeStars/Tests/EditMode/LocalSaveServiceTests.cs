using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class LocalSaveServiceTests
    {
        private const string RecoveryMessage =
            "The latest save could not be read, so the last complete checkpoint was restored.";

        private string m_TestRoot;
        private string m_SavePath;

        [SetUp]
        public void SetUp()
        {
            m_TestRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask7SaveTests",
                Guid.NewGuid().ToString("N"));
            m_SavePath = Path.Combine(m_TestRoot, "jss-save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_TestRoot))
            {
                Directory.Delete(m_TestRoot, recursive: true);
            }
        }

        [Test]
        public void SchemaV3_RoundTripsEveryOwnedDomainWithoutDeviceSettings()
        {
            var serializer = new JsonSaveSerializer(SaveMigrator.CreateCurrent());
            var original = CreateSave(checkpointOrdinal: 4, revision: 7, updatedUtcTicks: 800);
            original.Story.CheckpointId = "mirra.signal-array";
            original.Mission = new MissionProgress
            {
                MissionId = "mission.mirra",
                CheckpointNodeId = "mission.mirra.signal-array",
                CheckpointOrdinal = 4,
                CompletedNodeIds = new[] { "mission.mirra.arrival" },
                ActiveNodeIds = new[] { "mission.mirra.signal-array" },
            };
            original.Captain.BodyFamilyId = "captain.family.c";
            original.Captain.AppearancePresetId = "captain.face.03";
            original.Captain.SuitCosmeticId = "suit.founder";
            original.DiscoveryIds = new[] { "mirra.wind", "koro.geyser" };
            original.EarnedCosmeticIds = new[] { "patch.signal", "visor.amber" };
            original.AtlasEntryIds = new[] { "atlas.mirra", "atlas.koro" };
            original.Photographs = new[]
            {
                new PhotoMetadata
                {
                    PhotoId = "photo.local.1",
                    RelativePath = "Photos/mirra-1.jpg",
                    CapturedUtcTicks = 700,
                },
            };
            original.Birthday = new BirthdayState
            {
                HasValue = true,
                Day = 29,
                Month = 2,
                Year = 2012,
                LastBirthdayGiftYear = 2026,
            };

            var document = serializer.Serialize(original);
            var parsed = serializer.TryDeserialize(document, out var reopened);

            Assert.That(parsed, Is.True);
            Assert.That(reopened, Is.EqualTo(original));
            Assert.That(document, Does.Contain("\"schemaVersion\": 3"));
            Assert.That(document, Does.Contain("\"story\""));
            Assert.That(document, Does.Contain("\"mission\""));
            Assert.That(document, Does.Contain("\"captain\""));
            Assert.That(document, Does.Contain("\"discoveryIds\""));
            Assert.That(document, Does.Contain("\"earnedCosmeticIds\""));
            Assert.That(document, Does.Contain("\"atlasEntryIds\""));
            Assert.That(document, Does.Contain("\"photographs\""));
            Assert.That(document, Does.Contain("\"birthday\""));
            Assert.That(document, Does.Contain("\"metadata\""));
            Assert.That(document, Does.Not.Contain("pilotingAssist"));
            Assert.That(document, Does.Not.Contain("presentationQuality"));
        }

        [Test]
        public async Task MissingSave_IsExplicitAndDoesNotCreateFiles()
        {
            var service = new LocalSaveService(m_SavePath);

            var startup = await service.InitializeAsync(CancellationToken.None);
            var loaded = await service.LoadAsync(CancellationToken.None);

            Assert.That(startup.IsAvailable, Is.True);
            Assert.That(service.IsInitialized, Is.True);
            Assert.That(loaded.Status, Is.EqualTo(LoadSaveStatus.Missing));
            Assert.That(loaded.HasSave, Is.False);
            Assert.That(loaded.Save, Is.Null);
            Assert.That(loaded.UserMessage, Is.EqualTo("No local save exists yet."));
            Assert.That(File.Exists(m_SavePath), Is.False);
            Assert.That(File.Exists(m_SavePath + ".backup"), Is.False);
            Assert.That(File.Exists(m_SavePath + ".tmp"), Is.False);

            await service.ShutdownAsync();
            Assert.That(service.IsInitialized, Is.False);
        }

        [Test]
        public async Task CheckpointWrite_ReopensPrimaryAndKeepsPreviousCompleteBackup()
        {
            var first = CreateSave(checkpointOrdinal: 2, revision: 1, updatedUtcTicks: 200);
            var second = CreateSave(checkpointOrdinal: 5, revision: 2, updatedUtcTicks: 500);
            var service = new LocalSaveService(m_SavePath);
            await service.InitializeAsync(CancellationToken.None);

            await service.SaveCheckpointAsync(first, CancellationToken.None);
            await service.SaveCheckpointAsync(second, CancellationToken.None);

            Assert.That(File.Exists(m_SavePath), Is.True);
            Assert.That(File.Exists(m_SavePath + ".backup"), Is.True);
            Assert.That(File.Exists(m_SavePath + ".tmp"), Is.False);
            var reopened = new LocalSaveService(m_SavePath);
            var primary = await reopened.LoadAsync(CancellationToken.None);
            Assert.That(primary.Status, Is.EqualTo(LoadSaveStatus.LoadedPrimary));
            Assert.That(primary.Save, Is.EqualTo(second));
            var serializer = new JsonSaveSerializer(SaveMigrator.CreateCurrent());
            Assert.That(
                serializer.TryDeserialize(
                    File.ReadAllText(m_SavePath + ".backup"),
                    out var backup),
                Is.True);
            Assert.That(backup, Is.EqualTo(first));

            await service.ShutdownAsync();
        }

        [Test]
        public async Task MalformedPrimary_LoadsLastKnownGoodBackupWithoutMutatingEitherCopy()
        {
            var first = CreateSave(checkpointOrdinal: 2, revision: 1, updatedUtcTicks: 200);
            var second = CreateSave(checkpointOrdinal: 5, revision: 2, updatedUtcTicks: 500);
            var service = new LocalSaveService(m_SavePath);
            await service.InitializeAsync(CancellationToken.None);
            await service.SaveCheckpointAsync(first, CancellationToken.None);
            await service.SaveCheckpointAsync(second, CancellationToken.None);
            const string malformed = "{ definitely-not-a-save";
            File.WriteAllText(m_SavePath, malformed);
            var backupBefore = File.ReadAllBytes(m_SavePath + ".backup");

            var reopened = new LocalSaveService(m_SavePath);
            var startup = await reopened.InitializeAsync(CancellationToken.None);
            var result = reopened.LastLoadResult;

            Assert.That(startup.IsAvailable, Is.True);
            Assert.That(result.Status, Is.EqualTo(LoadSaveStatus.RecoveredBackup));
            Assert.That(result.HasSave, Is.True);
            Assert.That(result.Save, Is.EqualTo(first));
            Assert.That(result.UserMessage, Is.EqualTo(RecoveryMessage));
            Assert.That(File.ReadAllText(m_SavePath), Is.EqualTo(malformed));
            Assert.That(File.ReadAllBytes(m_SavePath + ".backup"), Is.EqualTo(backupBefore));
            Assert.That(File.Exists(m_SavePath + ".tmp"), Is.False);

            var explicitRecovery = await reopened.RecoverAsync(CancellationToken.None);
            Assert.That(
                explicitRecovery.Status,
                Is.EqualTo(LoadSaveStatus.RecoveredBackup));
            Assert.That(explicitRecovery.Save, Is.EqualTo(first));
            Assert.That(explicitRecovery.UserMessage, Is.EqualTo(RecoveryMessage));
        }

        [Test]
        public async Task BothCopiesMalformed_RemainsAvailableAndPreservesRecoveryEvidence()
        {
            Directory.CreateDirectory(m_TestRoot);
            const string primaryBytes = "broken-primary";
            const string backupBytes = "broken-backup";
            File.WriteAllText(m_SavePath, primaryBytes);
            File.WriteAllText(m_SavePath + ".backup", backupBytes);
            var service = new LocalSaveService(m_SavePath);

            var startup = await service.InitializeAsync(CancellationToken.None);
            var result = service.LastLoadResult;

            Assert.That(startup.IsAvailable, Is.True);
            Assert.That(result.Status, Is.EqualTo(LoadSaveStatus.Unreadable));
            Assert.That(result.HasSave, Is.False);
            Assert.That(result.Save, Is.Null);
            Assert.That(
                result.UserMessage,
                Is.EqualTo(
                    "Local progress could not be read. The damaged files were kept so recovery remains possible."));
            Assert.That(File.ReadAllText(m_SavePath), Is.EqualTo(primaryBytes));
            Assert.That(File.ReadAllText(m_SavePath + ".backup"), Is.EqualTo(backupBytes));
        }

        [Test]
        public async Task InterruptedReplacement_PreservesLastCompletePrimaryAndBackup()
        {
            var first = CreateSave(checkpointOrdinal: 1, revision: 1, updatedUtcTicks: 100);
            var second = CreateSave(checkpointOrdinal: 2, revision: 2, updatedUtcTicks: 200);
            var interrupted = CreateSave(checkpointOrdinal: 3, revision: 3, updatedUtcTicks: 300);
            var service = new LocalSaveService(m_SavePath);
            await service.InitializeAsync(CancellationToken.None);
            await service.SaveCheckpointAsync(first, CancellationToken.None);
            await service.SaveCheckpointAsync(second, CancellationToken.None);
            var primaryBefore = File.ReadAllBytes(m_SavePath);
            var backupBefore = File.ReadAllBytes(m_SavePath + ".backup");
            var failing = new LocalSaveService(
                m_SavePath,
                new JsonSaveSerializer(SaveMigrator.CreateCurrent()),
                new InterruptingReplaceStorage(new FileSaveStorage()));
            await failing.InitializeAsync(CancellationToken.None);

            Assert.ThrowsAsync<IOException>(async () =>
                await failing.SaveCheckpointAsync(
                    interrupted,
                    CancellationToken.None));

            Assert.That(File.ReadAllBytes(m_SavePath), Is.EqualTo(primaryBefore));
            Assert.That(File.ReadAllBytes(m_SavePath + ".backup"), Is.EqualTo(backupBefore));
            Assert.That(File.Exists(m_SavePath + ".tmp"), Is.False);
            var reopened = new LocalSaveService(m_SavePath);
            var result = await reopened.LoadAsync(CancellationToken.None);
            Assert.That(result.Save, Is.EqualTo(second));
        }

        [Test]
        public async Task NewCheckpointOverUnreadablePrimary_DoesNotOverwriteGoodBackup()
        {
            var first = CreateSave(checkpointOrdinal: 1, revision: 1, updatedUtcTicks: 100);
            var second = CreateSave(checkpointOrdinal: 2, revision: 2, updatedUtcTicks: 200);
            var replacement = CreateSave(checkpointOrdinal: 3, revision: 3, updatedUtcTicks: 300);
            var service = new LocalSaveService(m_SavePath);
            await service.InitializeAsync(CancellationToken.None);
            await service.SaveCheckpointAsync(first, CancellationToken.None);
            await service.SaveCheckpointAsync(second, CancellationToken.None);
            File.WriteAllText(m_SavePath, "unreadable-primary");
            var backupBefore = File.ReadAllBytes(m_SavePath + ".backup");
            var reopened = new LocalSaveService(m_SavePath);
            await reopened.InitializeAsync(CancellationToken.None);

            await reopened.SaveCheckpointAsync(replacement, CancellationToken.None);

            Assert.That(File.ReadAllBytes(m_SavePath + ".backup"), Is.EqualTo(backupBefore));
            var loaded = await reopened.LoadAsync(CancellationToken.None);
            Assert.That(loaded.Status, Is.EqualTo(LoadSaveStatus.LoadedPrimary));
            Assert.That(loaded.Save, Is.EqualTo(replacement));
        }

        [Test]
        public async Task ReadFailure_IsReportedWithoutBlockingRequiredStartup()
        {
            var service = new LocalSaveService(
                m_SavePath,
                new JsonSaveSerializer(SaveMigrator.CreateCurrent()),
                new ThrowingReadStorage());

            var startup = await service.InitializeAsync(CancellationToken.None);

            Assert.That(startup.IsAvailable, Is.True);
            Assert.That(service.LastLoadResult.Status, Is.EqualTo(LoadSaveStatus.StorageUnavailable));
            Assert.That(
                service.LastLoadResult.UserMessage,
                Is.EqualTo(
                    "Local progress is temporarily unavailable. You can keep playing offline and try again later."));
        }

        [Test]
        public async Task ReplaceableSerializer_IsUsedForCheckpointAndLoad()
        {
            var serializer = new RecordingSaveSerializer(
                new JsonSaveSerializer(SaveMigrator.CreateCurrent()));
            var service = new LocalSaveService(
                m_SavePath,
                serializer,
                new FileSaveStorage());
            await service.InitializeAsync(CancellationToken.None);
            var save = CreateSave(checkpointOrdinal: 1, revision: 1, updatedUtcTicks: 100);

            await service.SaveCheckpointAsync(save, CancellationToken.None);
            var loaded = await service.LoadAsync(CancellationToken.None);

            Assert.That(serializer.SerializeCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(serializer.DeserializeCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(loaded.Save, Is.EqualTo(save));
        }

        [Test]
        public void Merge_UnionsProgressUsesNewestCaptainAndKeepsPhotosLocal()
        {
            var local = CreateSave(checkpointOrdinal: 4, revision: 7, updatedUtcTicks: 700);
            local.Story.CheckpointId = "mirra.arrival";
            local.DiscoveryIds = new[] { "mirra.wind", "shared" };
            local.EarnedCosmeticIds = new[] { "suit.local" };
            local.AtlasEntryIds = new[] { "atlas.mirra" };
            local.Photographs = new[]
            {
                new PhotoMetadata
                {
                    PhotoId = "local.photo",
                    RelativePath = "Photos/local.jpg",
                    CapturedUtcTicks = 650,
                },
            };
            local.Captain.AppearancePresetId = "captain.local";
            local.Captain.LastCustomizedUtcTicks = 600;
            local.Birthday = new BirthdayState
            {
                HasValue = true,
                Day = 2,
                Month = 5,
                Year = 2012,
                LastBirthdayGiftYear = 2025,
            };
            var cloud = CreateSave(checkpointOrdinal: 8, revision: 9, updatedUtcTicks: 900);
            cloud.Story.CheckpointId = "koro.geyser";
            cloud.DiscoveryIds = new[] { "koro.geyser", "shared" };
            cloud.EarnedCosmeticIds = new[] { "suit.cloud" };
            cloud.AtlasEntryIds = new[] { "atlas.koro" };
            cloud.Photographs = new[]
            {
                new PhotoMetadata
                {
                    PhotoId = "cloud.photo",
                    RelativePath = "Photos/cloud.jpg",
                    CapturedUtcTicks = 850,
                },
            };
            cloud.Captain.AppearancePresetId = "captain.cloud";
            cloud.Captain.LastCustomizedUtcTicks = 800;
            cloud.Birthday = local.Birthday.Copy();
            cloud.Birthday.LastBirthdayGiftYear = 2026;

            var merged = SaveMerge.Combine(local, cloud);

            Assert.That(merged.Story.CheckpointOrdinal, Is.EqualTo(8));
            Assert.That(merged.Story.CheckpointId, Is.EqualTo("koro.geyser"));
            Assert.That(
                merged.DiscoveryIds,
                Is.EqualTo(new[] { "koro.geyser", "mirra.wind", "shared" }));
            Assert.That(
                merged.EarnedCosmeticIds,
                Is.EqualTo(new[] { "suit.cloud", "suit.local" }));
            Assert.That(
                merged.AtlasEntryIds,
                Is.EqualTo(new[] { "atlas.koro", "atlas.mirra" }));
            Assert.That(merged.Captain.AppearancePresetId, Is.EqualTo("captain.cloud"));
            Assert.That(merged.Photographs, Is.EqualTo(local.Photographs));
            Assert.That(merged.Photographs, Is.Not.SameAs(local.Photographs));
            Assert.That(merged.Birthday.LastBirthdayGiftYear, Is.EqualTo(2026));
            Assert.That(merged.Metadata.SaveId, Is.EqualTo(local.Metadata.SaveId));
            Assert.That(merged.Metadata.Revision, Is.EqualTo(10));
            Assert.That(merged.Metadata.CreatedUtcTicks, Is.EqualTo(100));
            Assert.That(merged.Metadata.UpdatedUtcTicks, Is.EqualTo(901));
        }

        [TestCase(SaveMergeConflictKind.StoryCheckpoint)]
        [TestCase(SaveMergeConflictKind.CaptainCustomization)]
        [TestCase(SaveMergeConflictKind.Birthday)]
        public void Merge_IncompatibleEqualPriorityStateThrowsTypedConflict(
            SaveMergeConflictKind conflictKind)
        {
            var local = CreateSave(checkpointOrdinal: 4, revision: 4, updatedUtcTicks: 400);
            var cloud = local.Copy();
            cloud.Metadata.SaveId = "save.cloud";

            switch (conflictKind)
            {
                case SaveMergeConflictKind.StoryCheckpoint:
                    cloud.Story.CheckpointId = "different.same-ordinal";
                    break;
                case SaveMergeConflictKind.CaptainCustomization:
                    cloud.Captain.AppearancePresetId = "different.same-time";
                    break;
                case SaveMergeConflictKind.Birthday:
                    local.Birthday = new BirthdayState
                    {
                        HasValue = true,
                        Day = 1,
                        Month = 1,
                        Year = 2012,
                    };
                    cloud.Birthday = new BirthdayState
                    {
                        HasValue = true,
                        Day = 2,
                        Month = 1,
                        Year = 2012,
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(conflictKind));
            }

            var failure = Assert.Throws<SaveMergeConflictException>(() =>
                SaveMerge.Combine(local, cloud));

            Assert.That(failure.Kind, Is.EqualTo(conflictKind));
            Assert.That(failure.Message, Does.Contain("player choice"));
        }

        [Test]
        public async Task PreCancelledOperations_DoNotReadOrWrite()
        {
            var storage = new CountingSaveStorage();
            var service = new LocalSaveService(
                m_SavePath,
                new JsonSaveSerializer(SaveMigrator.CreateCurrent()),
                storage);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await AssertCancelledAsync(() =>
                service.InitializeAsync(cancellation.Token).AsTask());
            await AssertCancelledAsync(() =>
                service.LoadAsync(cancellation.Token).AsTask());
            await AssertCancelledAsync(() =>
                service.RecoverAsync(cancellation.Token).AsTask());
            await AssertCancelledAsync(() =>
                service.SaveCheckpointAsync(
                    CreateSave(1, 1, 100),
                    cancellation.Token).AsTask());
            Assert.That(storage.OperationCount, Is.EqualTo(0));
        }

        private static async Task AssertCancelledAsync(Func<Task> action)
        {
            try
            {
                await action();
                Assert.Fail("Expected the operation to observe cancellation.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static GameSave CreateSave(
            int checkpointOrdinal,
            long revision,
            long updatedUtcTicks)
        {
            var save = GameSave.CreateNew("save.local", createdUtcTicks: 100);
            save.Story.CheckpointId = $"checkpoint.{checkpointOrdinal}";
            save.Story.CheckpointOrdinal = checkpointOrdinal;
            save.Captain.BodyFamilyId = "captain.family.a";
            save.Captain.AppearancePresetId = "captain.face.01";
            save.Captain.SuitCosmeticId = "suit.clubhouse";
            save.Captain.LastCustomizedUtcTicks = 100;
            save.Metadata.Revision = revision;
            save.Metadata.UpdatedUtcTicks = updatedUtcTicks;
            return save;
        }

        private sealed class InterruptingReplaceStorage : ISaveStorage
        {
            private readonly ISaveStorage m_Inner;

            public InterruptingReplaceStorage(ISaveStorage inner)
            {
                m_Inner = inner;
            }

            public bool Exists(string path) => m_Inner.Exists(path);

            public string ReadAllText(string path) => m_Inner.ReadAllText(path);

            public void WriteDurably(string path, string document) =>
                m_Inner.WriteDurably(path, document);

            public void Move(string sourcePath, string destinationPath) =>
                m_Inner.Move(sourcePath, destinationPath);

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath)
            {
                throw new IOException("Injected interruption before atomic replacement.");
            }

            public void Delete(string path) => m_Inner.Delete(path);
        }

        private sealed class ThrowingReadStorage : ISaveStorage
        {
            public bool Exists(string path) => true;

            public string ReadAllText(string path) =>
                throw new IOException("Injected storage read failure.");

            public void WriteDurably(string path, string document) =>
                throw new InvalidOperationException();

            public void Move(string sourcePath, string destinationPath) =>
                throw new InvalidOperationException();

            public void Replace(string sourcePath, string destinationPath, string backupPath) =>
                throw new InvalidOperationException();

            public void Delete(string path)
            {
            }
        }

        private sealed class CountingSaveStorage : ISaveStorage
        {
            public int OperationCount { get; private set; }

            public bool Exists(string path)
            {
                OperationCount++;
                return false;
            }

            public string ReadAllText(string path)
            {
                OperationCount++;
                return null;
            }

            public void WriteDurably(string path, string document)
            {
                OperationCount++;
            }

            public void Move(string sourcePath, string destinationPath)
            {
                OperationCount++;
            }

            public void Replace(string sourcePath, string destinationPath, string backupPath)
            {
                OperationCount++;
            }

            public void Delete(string path)
            {
                OperationCount++;
            }
        }

        private sealed class RecordingSaveSerializer : ISaveSerializer
        {
            private readonly ISaveSerializer m_Inner;

            public RecordingSaveSerializer(ISaveSerializer inner)
            {
                m_Inner = inner;
            }

            public int SerializeCount { get; private set; }

            public int DeserializeCount { get; private set; }

            public string Serialize(GameSave save)
            {
                SerializeCount++;
                return m_Inner.Serialize(save);
            }

            public bool TryDeserialize(string document, out GameSave save)
            {
                DeserializeCount++;
                return m_Inner.TryDeserialize(document, out save);
            }
        }
    }
}
