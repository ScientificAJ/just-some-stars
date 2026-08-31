using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class AccountLinkTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "JssTask21Accounts",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
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
        public async Task GuestIdentity_IsStableAcrossRestartWithoutNetworkOrAccount()
        {
            var path = Path.Combine(m_Root, "guest-account.json");
            var first = new GuestAccountService(path);
            var firstStart = await first.InitializeAsync(CancellationToken.None);
            var guestId = first.Current.GuestId;
            await first.ShutdownAsync();

            var reopened = new GuestAccountService(path);
            var secondStart = await reopened.InitializeAsync(CancellationToken.None);

            Assert.That(firstStart.IsAvailable, Is.True);
            Assert.That(secondStart.IsAvailable, Is.True);
            Assert.That(guestId, Does.StartWith("guest."));
            Assert.That(reopened.Current.GuestId, Is.EqualTo(guestId));
            Assert.That(reopened.Current.Connection, Is.EqualTo(AccountConnection.OfflineGuest));
            Assert.That(reopened.Current.FirebaseUserId, Is.Empty);
            Assert.That(reopened.Current.StatusMessage,
                Is.EqualTo("Playing offline. Progress stays on this device."));
        }

        [Test]
        public async Task LinkGoogle_MergesGuestAndCloudThenPersistsBothCopies()
        {
            var local = await CreateLocalSaveAsync("save.local", 5, "local.discovery");
            var cloud = new FakeCloudSaveService
            {
                Downloaded = new CloudSaveSnapshot(
                    CreateSave("save.cloud", 8, "cloud.discovery")),
            };
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.123",
            };
            var service = CreateFirebaseService(local, cloud, auth);
            await service.InitializeAsync(CancellationToken.None);
            var guestId = service.Current.GuestId;

            var result = await service.LinkGoogleAsync(CancellationToken.None);
            var persisted = await local.LoadAsync(CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(AccountLinkStatus.Linked));
            Assert.That(result.MergedSave.Story.CheckpointOrdinal, Is.EqualTo(8));
            Assert.That(
                result.MergedSave.DiscoveryIds,
                Is.EqualTo(new[] { "cloud.discovery", "local.discovery" }));
            Assert.That(persisted.Save, Is.EqualTo(result.MergedSave));
            Assert.That(cloud.UploadedUserId, Is.EqualTo("firebase.uid.123"));
            Assert.That(cloud.UploadedSave, Is.EqualTo(result.MergedSave));
            Assert.That(service.Current.Connection, Is.EqualTo(AccountConnection.Linked));
            Assert.That(service.Current.FirebaseUserId, Is.EqualTo("firebase.uid.123"));
            Assert.That(service.Current.GuestId, Is.EqualTo(guestId));
        }

        [Test]
        public async Task Initialize_WithPersistedAuthSession_ReconcilesWithoutInteractiveLink()
        {
            var local = await CreateLocalSaveAsync("save.local", 5, "local.discovery");
            var cloud = new FakeCloudSaveService
            {
                Downloaded = new CloudSaveSnapshot(
                    CreateSave("save.cloud", 8, "cloud.discovery"),
                    version: new CloudSaveVersion(8, "remote-v8")),
            };
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                InitialUserId = "firebase.uid.persisted",
            };
            var service = CreateFirebaseService(local, cloud, auth);

            var startup = await service.InitializeAsync(CancellationToken.None);

            Assert.That(startup.IsAvailable, Is.True);
            Assert.That(auth.LinkCount, Is.EqualTo(0));
            Assert.That(service.Current.Connection, Is.EqualTo(AccountConnection.Linked));
            Assert.That(service.Current.FirebaseUserId,
                Is.EqualTo("firebase.uid.persisted"));
            Assert.That(cloud.UploadedSave.Story.CheckpointOrdinal, Is.EqualTo(8));
            Assert.That(
                cloud.UploadedSave.DiscoveryIds,
                Is.EqualTo(new[] { "cloud.discovery", "local.discovery" }));
        }

        [Test]
        public async Task SyncLinkedSession_DoesNotReenterInteractiveGoogleLink()
        {
            var local = await CreateLocalSaveAsync("save.local", 5, "local.discovery");
            var cloud = new FakeCloudSaveService();
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.sync",
            };
            var service = CreateFirebaseService(local, cloud, auth);
            await service.InitializeAsync(CancellationToken.None);
            await service.LinkGoogleAsync(CancellationToken.None);
            var linkCount = auth.LinkCount;

            var result = await service.SyncAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(auth.LinkCount, Is.EqualTo(linkCount));
            Assert.That(cloud.UploadCount, Is.EqualTo(2));
            Assert.That(service.Current.Connection, Is.EqualTo(AccountConnection.Linked));
        }

        [Test]
        public async Task LinkedCheckpoint_SavesLocallyThenSynchronizesWithoutRelinking()
        {
            var local = await CreateLocalSaveAsync("save.local", 5, "local.discovery");
            var cloud = new FakeCloudSaveService();
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.checkpoint",
            };
            var account = CreateFirebaseService(local, cloud, auth);
            await account.InitializeAsync(CancellationToken.None);
            await account.LinkGoogleAsync(CancellationToken.None);
            var saves = new CloudCheckpointSaveService(local, account);
            var next = CreateSave("save.local", 6, "checkpoint.discovery");
            var linkCount = auth.LinkCount;
            var uploadCount = cloud.UploadCount;

            await saves.SaveCheckpointAsync(next, CancellationToken.None);

            Assert.That(
                (await local.LoadAsync(CancellationToken.None)).Save,
                Is.EqualTo(next));
            Assert.That(auth.LinkCount, Is.EqualTo(linkCount));
            Assert.That(cloud.UploadCount, Is.EqualTo(uploadCount + 1));
            Assert.That(cloud.UploadedSave, Is.EqualTo(next));

            var offlineNext = CreateSave(
                "save.local",
                7,
                "offline-checkpoint.discovery");
            var throwingAccount = new ThrowingSyncAccountService(
                account.Current,
                new ArgumentException("cloud projection rejected an identifier"));
            var resilientSaves = new CloudCheckpointSaveService(
                local,
                throwingAccount);

            Assert.DoesNotThrowAsync(async () =>
                await resilientSaves.SaveCheckpointAsync(
                    offlineNext,
                    CancellationToken.None));
            Assert.That(
                (await local.LoadAsync(CancellationToken.None)).Save,
                Is.EqualTo(offlineNext),
                "An optional cloud failure must never undo or escape after the local checkpoint is durable.");
            Assert.That(throwingAccount.SyncCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ConditionalWriteMismatch_PreservesBothCopiesAndReportsRetry()
        {
            var local = await CreateLocalSaveAsync("save.local", 5, "local.discovery");
            var localBefore = (await local.LoadAsync(CancellationToken.None)).Save;
            var remoteBefore = CreateSave("save.cloud", 8, "cloud.discovery");
            var cloud = new FakeCloudSaveService
            {
                Downloaded = new CloudSaveSnapshot(
                    remoteBefore,
                    version: new CloudSaveVersion(8, "remote-v8")),
                NextCommitResult = new CloudCommitResult(
                    committed: false,
                    versionMismatch: true,
                    new CloudSaveVersion(9, "competing-v9")),
            };
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.race",
            };
            var service = CreateFirebaseService(local, cloud, auth);
            await service.InitializeAsync(CancellationToken.None);

            var result = await service.LinkGoogleAsync(CancellationToken.None);
            var localAfter = (await local.LoadAsync(CancellationToken.None)).Save;

            Assert.That(result.Status, Is.EqualTo(AccountLinkStatus.Failed));
            Assert.That(cloud.UploadCount, Is.EqualTo(0));
            Assert.That(cloud.Downloaded.Value.Save, Is.EqualTo(remoteBefore));
            Assert.That(localAfter, Is.Not.EqualTo(localBefore));
            Assert.That(localAfter.Story.CheckpointOrdinal, Is.EqualTo(8));
            Assert.That(
                localAfter.DiscoveryIds,
                Is.EqualTo(new[] { "cloud.discovery", "local.discovery" }));
            Assert.That(service.Current.Connection, Is.EqualTo(AccountConnection.Pending));
            Assert.That(service.Current.Sync,
                Is.EqualTo(AccountSyncState.NeedsRemoteRead));
            Assert.That(service.Current.StatusMessage, Does.Contain("waiting to retry"));
        }

        [Test]
        public async Task MissingFirebaseConfiguration_RemainsPlayableAndMutatesNothing()
        {
            var local = await CreateLocalSaveAsync("save.local", 3, "local.discovery");
            var before = (await local.LoadAsync(CancellationToken.None)).Save;
            var cloud = new FakeCloudSaveService
            {
                InitializeResult = StartupResult.Unavailable(
                    "Google backup is not configured for this build."),
            };
            var auth = new FakeFirebaseAuthGateway { IsConfigured = false };
            var service = CreateFirebaseService(local, cloud, auth);

            var startup = await service.InitializeAsync(CancellationToken.None);
            var link = await service.LinkGoogleAsync(CancellationToken.None);
            var after = (await local.LoadAsync(CancellationToken.None)).Save;

            Assert.That(startup.IsAvailable, Is.True,
                "The guest path remains an available service without Firebase.");
            Assert.That(service.Current.Connection,
                Is.EqualTo(AccountConnection.CloudUnavailable));
            Assert.That(service.Current.StatusMessage,
                Is.EqualTo("Google backup is unavailable in this build. " +
                           "Offline progress still works."));
            Assert.That(link.Status, Is.EqualTo(AccountLinkStatus.Unavailable));
            Assert.That(after, Is.EqualTo(before));
            Assert.That(auth.LinkCount, Is.EqualTo(0));
            Assert.That(cloud.UploadCount, Is.EqualTo(0));
        }

        [Test]
        public async Task SignOut_ReturnsToSameGuestAndNeverDeletesLocalProgress()
        {
            var local = await CreateLocalSaveAsync("save.local", 6, "local.discovery");
            var cloud = new FakeCloudSaveService();
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.signout",
            };
            var service = CreateFirebaseService(local, cloud, auth);
            await service.InitializeAsync(CancellationToken.None);
            var guestId = service.Current.GuestId;
            await service.LinkGoogleAsync(CancellationToken.None);

            await service.SignOutAsync(CancellationToken.None);

            Assert.That(auth.SignOutCount, Is.EqualTo(1));
            Assert.That(service.Current.Connection,
                Is.EqualTo(AccountConnection.CloudAvailable));
            Assert.That(service.Current.GuestId, Is.EqualTo(guestId));
            Assert.That((await local.LoadAsync(CancellationToken.None)).HasSave, Is.True);
            Assert.That(cloud.DeleteCount, Is.EqualTo(0));
        }

        [Test]
        public async Task AccountOperationFailures_AlwaysRestoreAnIdleAuthenticatedState()
        {
            var local = await CreateLocalSaveAsync("save.local", 6, "local.discovery");
            var cloud = new FakeCloudSaveService();
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.failures",
            };
            var deletion = new FakeAccountDeletionGateway();
            var service = CreateFirebaseService(local, cloud, auth, deletion);
            await service.InitializeAsync(CancellationToken.None);
            await service.LinkGoogleAsync(CancellationToken.None);

            cloud.ThrowOnExport = true;
            Assert.That(
                (await service.ExportDataAsync(CancellationToken.None)).Succeeded,
                Is.False);
            AssertIdleAuthenticated(service, "export failure");

            auth.ThrowOnSignOut = true;
            await service.SignOutAsync(CancellationToken.None);
            AssertIdleAuthenticated(service, "sign-out failure");

            auth.ThrowOnUnlink = true;
            Assert.That(
                (await service.UnlinkGoogleAsync(CancellationToken.None)).Status,
                Is.EqualTo(AccountUnlinkStatus.Failed));
            AssertIdleAuthenticated(service, "unlink failure");

            deletion.ThrowOnDelete = true;
            await service.DeleteAccountAsync(CancellationToken.None);
            AssertIdleAuthenticated(service, "partial delete failure");
            Assert.That(
                service.Current.StatusMessage,
                Does.Contain("did not fully complete"));

            await service.SignOutAsync(CancellationToken.None);
            cloud.DownloadException = new ArgumentException(
                "cloud projection rejected an identifier");
            Assert.That(
                (await service.LinkGoogleAsync(CancellationToken.None)).Status,
                Is.EqualTo(AccountLinkStatus.Failed));
            AssertPendingAuthenticated(service, "post-auth projection failure");

            await service.SignOutAsync(CancellationToken.None);
            cloud.DownloadException = new OperationCanceledException(
                "link cancelled after authentication");
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await service.LinkGoogleAsync(CancellationToken.None));
            AssertPendingAuthenticated(service, "post-auth cancellation");
        }

        [Test]
        public async Task ExportUnlinkAndDelete_AreExplicitUidScopedOperations()
        {
            var local = await CreateLocalSaveAsync("save.local", 4, "local.discovery");
            var cloud = new FakeCloudSaveService { ExportDocument = "{cloud-export}" };
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.erase",
            };
            var deletion = new FakeAccountDeletionGateway();
            var service = CreateFirebaseService(local, cloud, auth, deletion);
            await service.InitializeAsync(CancellationToken.None);
            await service.LinkGoogleAsync(CancellationToken.None);

            var export = await service.ExportCloudDataAsync(CancellationToken.None);
            var unlink = await service.UnlinkGoogleAsync(CancellationToken.None);
            Assert.That(export, Is.EqualTo("{cloud-export}"));
            Assert.That(unlink.Status, Is.EqualTo(AccountUnlinkStatus.Unlinked));
            Assert.That(auth.UnlinkCount, Is.EqualTo(1));
            Assert.That(cloud.DeleteCount, Is.EqualTo(0));
            Assert.That(service.Current.Connection,
                Is.EqualTo(AccountConnection.CloudAvailable));

            await service.LinkGoogleAsync(CancellationToken.None);

            await service.DeleteAccountAsync(CancellationToken.None);

            Assert.That(deletion.DeletedUserId, Is.EqualTo("firebase.uid.erase"));
            Assert.That(deletion.DeleteCount, Is.EqualTo(1));
            Assert.That(cloud.DeleteCount, Is.Zero);
            Assert.That(auth.SignOutCount, Is.EqualTo(1));
            Assert.That(service.Current.Connection,
                Is.EqualTo(AccountConnection.CloudAvailable));
            Assert.That((await local.LoadAsync(CancellationToken.None)).HasSave, Is.True,
                "Complete cloud deletion must not erase the guest's device save.");
        }

        [TestCase(
            AccountConflictChoice.UseThisDevice,
            "checkpoint.5",
            "mission.local")]
        [TestCase(
            AccountConflictChoice.UseCloudBackup,
            "checkpoint.incompatible",
            "mission.cloud")]
        public async Task ConflictChoice_PersistsSelectedStoryAndPreservesCollections(
            AccountConflictChoice choice,
            string expectedCheckpoint,
            string expectedMission)
        {
            var local = await CreateLocalSaveAsync("save.local", 5, "local.discovery");
            var localSave = (await local.LoadAsync(CancellationToken.None)).Save;
            localSave.EarnedCosmeticIds = new[] { "cosmetic.local" };
            localSave.AtlasEntryIds = new[] { "atlas.local" };
            localSave.Captain.SuitCosmeticId = "suit.local";
            localSave.Captain.LastCustomizedUtcTicks = 200;
            localSave.Mission.MissionId = "mission.local";
            localSave.Mission.CheckpointNodeId = "mission.local.node";
            localSave.Mission.CheckpointOrdinal = 1;
            localSave.Birthday.HasValue = true;
            localSave.Birthday.Day = 4;
            localSave.Birthday.Month = 7;
            localSave.Birthday.Year = 2013;
            localSave.Birthday.LastBirthdayGiftYear = 2025;
            localSave.Photographs = new[]
            {
                new PhotoMetadata
                {
                    PhotoId = "photo.local",
                    RelativePath = "photos/local.png",
                    CapturedUtcTicks = 190,
                },
            };
            await local.SaveCheckpointAsync(localSave, CancellationToken.None);

            var cloudSave = CreateSave("save.cloud", 5, "cloud.discovery");
            cloudSave.Story.CheckpointId = "checkpoint.incompatible";
            cloudSave.EarnedCosmeticIds = new[] { "cosmetic.cloud" };
            cloudSave.AtlasEntryIds = new[] { "atlas.cloud" };
            cloudSave.Captain.SuitCosmeticId = "suit.cloud.newer";
            cloudSave.Captain.LastCustomizedUtcTicks = 300;
            cloudSave.Mission.MissionId = "mission.cloud";
            cloudSave.Mission.CheckpointNodeId = "mission.cloud.node";
            cloudSave.Mission.CheckpointOrdinal = 2;
            cloudSave.Birthday.HasValue = true;
            cloudSave.Birthday.Day = 4;
            cloudSave.Birthday.Month = 7;
            cloudSave.Birthday.Year = 2013;
            cloudSave.Birthday.LastBirthdayGiftYear = 2026;
            cloudSave.Photographs = new[]
            {
                new PhotoMetadata
                {
                    PhotoId = "photo.cloud",
                    RelativePath = "photos/cloud.png",
                    CapturedUtcTicks = 195,
                },
            };
            var cloud = new FakeCloudSaveService
            {
                Downloaded = new CloudSaveSnapshot(
                    cloudSave,
                    version: new CloudSaveVersion(5, "remote-v5")),
            };
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.choice",
            };
            var service = CreateFirebaseService(local, cloud, auth);
            await service.InitializeAsync(CancellationToken.None);
            Assert.That(
                (await service.LinkGoogleAsync(CancellationToken.None)).Status,
                Is.EqualTo(AccountLinkStatus.NeedsPlayerChoice));

            var result = await service.ResolveConflictAsync(
                choice,
                CancellationToken.None);
            var persisted = (await local.LoadAsync(CancellationToken.None)).Save;

            Assert.That(result.Status, Is.EqualTo(AccountLinkStatus.Linked));
            Assert.That(persisted.Story.CheckpointId, Is.EqualTo(expectedCheckpoint));
            Assert.That(persisted.Mission.MissionId, Is.EqualTo(expectedMission));
            Assert.That(
                persisted.DiscoveryIds,
                Is.EqualTo(new[] { "cloud.discovery", "local.discovery" }));
            Assert.That(
                persisted.EarnedCosmeticIds,
                Is.EqualTo(new[] { "cosmetic.cloud", "cosmetic.local" }));
            Assert.That(
                persisted.AtlasEntryIds,
                Is.EqualTo(new[] { "atlas.cloud", "atlas.local" }));
            Assert.That(persisted.Captain.SuitCosmeticId,
                Is.EqualTo("suit.cloud.newer"));
            Assert.That(persisted.Birthday.LastBirthdayGiftYear, Is.EqualTo(2026));
            Assert.That(persisted.Photographs.Length, Is.EqualTo(1));
            Assert.That(persisted.Photographs[0].PhotoId, Is.EqualTo("photo.local"));
            Assert.That(cloud.UploadedSave, Is.EqualTo(persisted));
            Assert.That(service.Current.Sync, Is.EqualTo(AccountSyncState.Synced));
        }

        [Test]
        public async Task IncompatibleCloudSave_ReturnsPlayerChoiceWithoutOverwritingEitherCopy()
        {
            var local = await CreateLocalSaveAsync("save.local", 5, "local.discovery");
            var localBefore = (await local.LoadAsync(CancellationToken.None)).Save;
            var cloudSave = CreateSave("save.cloud", 5, "cloud.discovery");
            cloudSave.Story.CheckpointId = "checkpoint.incompatible";
            var cloud = new FakeCloudSaveService
            {
                Downloaded = new CloudSaveSnapshot(cloudSave),
            };
            var auth = new FakeFirebaseAuthGateway
            {
                IsConfigured = true,
                LinkUserId = "firebase.uid.conflict",
            };
            var service = CreateFirebaseService(local, cloud, auth);
            await service.InitializeAsync(CancellationToken.None);

            var result = await service.LinkGoogleAsync(CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(AccountLinkStatus.NeedsPlayerChoice));
            Assert.That(result.ConflictKind,
                Is.EqualTo(SaveMergeConflictKind.StoryCheckpoint));
            Assert.That((await local.LoadAsync(CancellationToken.None)).Save,
                Is.EqualTo(localBefore));
            Assert.That(cloud.UploadCount, Is.EqualTo(0));
            Assert.That(service.Current.Connection,
                Is.EqualTo(AccountConnection.Conflict));
            Assert.That(service.Current.Sync, Is.EqualTo(AccountSyncState.Conflict));
        }

        private FirebaseAccountService CreateFirebaseService(
            LocalSaveService local,
            FakeCloudSaveService cloud,
            FakeFirebaseAuthGateway auth,
            FakeAccountDeletionGateway deletion = null)
        {
            var guest = new GuestAccountService(Path.Combine(
                m_Root,
                "guest-account.json"));
            return new FirebaseAccountService(
                guest,
                local,
                cloud,
                auth,
                deletion ?? new FakeAccountDeletionGateway());
        }

        private static void AssertIdleAuthenticated(
            FirebaseAccountService service,
            string operation)
        {
            Assert.That(service.Current.Operation, Is.EqualTo(AccountOperation.None),
                operation);
            Assert.That(service.Current.Connection, Is.EqualTo(AccountConnection.Linked),
                operation);
            Assert.That(service.Current.FirebaseUserId, Is.Not.Empty, operation);
        }

        private static void AssertPendingAuthenticated(
            FirebaseAccountService service,
            string operation)
        {
            Assert.That(service.Current.Operation, Is.EqualTo(AccountOperation.None),
                operation);
            Assert.That(service.Current.Connection, Is.EqualTo(AccountConnection.Pending),
                operation);
            Assert.That(service.Current.FirebaseUserId, Is.Not.Empty, operation);
        }

        private async Task<LocalSaveService> CreateLocalSaveAsync(
            string saveId,
            int checkpoint,
            string discovery)
        {
            var local = new LocalSaveService(Path.Combine(m_Root, "save.json"));
            await local.InitializeAsync(CancellationToken.None);
            await local.SaveCheckpointAsync(
                CreateSave(saveId, checkpoint, discovery),
                CancellationToken.None);
            return local;
        }

        private static GameSave CreateSave(
            string saveId,
            int checkpoint,
            string discovery)
        {
            var save = GameSave.CreateNew(saveId, createdUtcTicks: 100);
            save.Story.CheckpointId = $"checkpoint.{checkpoint}";
            save.Story.CheckpointOrdinal = checkpoint;
            save.DiscoveryIds = new[] { discovery };
            save.Metadata.Revision = checkpoint;
            save.Metadata.UpdatedUtcTicks = 100 + checkpoint;
            return save;
        }

        private sealed class FakeCloudSaveService : ICloudSaveService
        {
            public StartupResult InitializeResult { get; set; } =
                StartupResult.Available();

            public CloudSaveSnapshot? Downloaded { get; set; }

            public string ExportDocument { get; set; } = "{export}";

            public CloudCommitResult? NextCommitResult { get; set; }

            public bool ThrowOnExport { get; set; }

            public Exception DownloadException { get; set; }

            public int UploadCount { get; private set; }

            public int DeleteCount { get; private set; }

            public string UploadedUserId { get; private set; }

            public GameSave UploadedSave { get; private set; }

            public string DeletedUserId { get; private set; }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(InitializeResult);
            }

            public ValueTask ShutdownAsync()
            {
                return default;
            }

            public ValueTask<CloudSaveSnapshot?> DownloadAsync(
                string userId,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DownloadException != null)
                {
                    var exception = DownloadException;
                    DownloadException = null;
                    throw exception;
                }

                return new ValueTask<CloudSaveSnapshot?>(Downloaded);
            }

            public ValueTask UploadAsync(
                string userId,
                GameSave save,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UploadCount++;
                UploadedUserId = userId;
                UploadedSave = save.Copy();
                return default;
            }

            public ValueTask<CloudCommitResult> UploadIfUnchangedAsync(
                string userId,
                GameSave save,
                CloudSaveVersion expectedRemote,
                CancellationToken cancellationToken)
            {
                if (NextCommitResult.HasValue)
                {
                    var result = NextCommitResult.Value;
                    NextCommitResult = null;
                    return new ValueTask<CloudCommitResult>(result);
                }

                UploadAsync(userId, save, cancellationToken);
                return new ValueTask<CloudCommitResult>(new CloudCommitResult(
                    committed: true,
                    versionMismatch: false,
                    new CloudSaveVersion(save.Metadata.Revision, "committed")));
            }

            public ValueTask DeleteAsync(
                string userId,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeleteCount++;
                DeletedUserId = userId;
                return default;
            }

            public ValueTask<string> ExportAsync(
                string userId,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ThrowOnExport)
                {
                    ThrowOnExport = false;
                    throw new InvalidOperationException("export failure");
                }

                return new ValueTask<string>(ExportDocument);
            }
        }

        private sealed class FakeFirebaseAuthGateway : IFirebaseAuthGateway
        {
            public bool IsConfigured { get; set; }

            public string LinkUserId { get; set; }

            public string InitialUserId { get; set; }

            public string CurrentUserId { get; private set; }

            public int LinkCount { get; private set; }

            public int SignOutCount { get; private set; }

            public int UnlinkCount { get; private set; }

            public bool ThrowOnSignOut { get; set; }

            public bool ThrowOnUnlink { get; set; }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CurrentUserId = InitialUserId;
                return new ValueTask<StartupResult>(IsConfigured
                    ? StartupResult.Available()
                    : StartupResult.Unavailable(
                        "Google backup is not configured for this build."));
            }

            public ValueTask<string> LinkGoogleAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LinkCount++;
                CurrentUserId = LinkUserId;
                return new ValueTask<string>(CurrentUserId);
            }

            public ValueTask SignOutAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ThrowOnSignOut)
                {
                    ThrowOnSignOut = false;
                    throw new InvalidOperationException("sign-out failure");
                }

                SignOutCount++;
                CurrentUserId = null;
                return default;
            }

            public ValueTask UnlinkGoogleAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ThrowOnUnlink)
                {
                    ThrowOnUnlink = false;
                    throw new InvalidOperationException("unlink failure");
                }

                UnlinkCount++;
                CurrentUserId = null;
                return default;
            }

            public ValueTask ShutdownAsync()
            {
                CurrentUserId = null;
                return default;
            }
        }

        private sealed class FakeAccountDeletionGateway : IAccountDeletionGateway
        {
            public bool IsConfigured { get; set; } = true;

            public bool ThrowOnDelete { get; set; }

            public int DeleteCount { get; private set; }

            public string DeletedUserId { get; private set; }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(IsConfigured
                    ? StartupResult.Available()
                    : StartupResult.Unavailable("Server deletion is unavailable."));
            }

            public ValueTask DeleteAccountAsync(
                string userId,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ThrowOnDelete)
                {
                    ThrowOnDelete = false;
                    throw new InvalidOperationException("server deletion failure");
                }

                DeleteCount++;
                DeletedUserId = userId;
                return default;
            }

            public ValueTask ShutdownAsync() => default;
        }

        private sealed class ThrowingSyncAccountService : IAccountService
        {
            private readonly Exception m_Exception;

            public ThrowingSyncAccountService(AccountState current, Exception exception)
            {
                Current = current;
                m_Exception = exception;
            }

            public AccountState Current { get; }

            public int SyncCount { get; private set; }

#pragma warning disable 67
            public event Action<AccountState> StateChanged;
#pragma warning restore 67

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<AccountLinkResult> LinkGoogleAsync(
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ValueTask<AccountLinkResult> ResolveConflictAsync(
                AccountConflictChoice choice,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ValueTask<CloudSyncResult> SyncAsync(
                CancellationToken cancellationToken)
            {
                SyncCount++;
                throw m_Exception;
            }

            public ValueTask<AccountExportResult> ExportDataAsync(
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ValueTask<AccountUnlinkResult> UnlinkGoogleAsync(
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ValueTask SignOutAsync(CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ValueTask DeleteAccountAsync(CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }
    }
}
