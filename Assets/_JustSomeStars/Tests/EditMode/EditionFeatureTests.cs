using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class EditionFeatureTests
    {
        [Test]
        public async Task VerifiedExplorer_RemainsAvailableOfflineAndRevokesOnlyOnVerifiedState()
        {
            var store = new FakeStore(Verified("explorer_edition"));
            var editions = new EditionFeatureService(store);

            Assert.That((await editions.InitializeAsync(CancellationToken.None)).IsAvailable,
                Is.True);
            Assert.That(editions.ExplorerEditionOwned, Is.True);
            foreach (EditionFeature feature in Enum.GetValues(typeof(EditionFeature)))
            {
                Assert.That(editions.IsAvailable(feature), Is.True, feature.ToString());
            }

            editions.Observe(EntitlementSnapshot.Empty);
            Assert.That(editions.ExplorerEditionOwned, Is.True,
                "Network loss/unverified state cannot remove a verified edition.");

            editions.Observe(Verified());
            Assert.That(editions.ExplorerEditionOwned, Is.False,
                "A fresh verified entitlement state may revoke the edition.");
        }

        [Test]
        public void FreeGame_KeepsStoryAtlasAndStandardPhotoMode()
        {
            var editions = new EditionFeatureService(new FakeStore(EntitlementSnapshot.Empty));

            Assert.That(editions.BaseStoryAvailable, Is.True);
            Assert.That(editions.AtlasScienceAvailable, Is.True);
            Assert.That(editions.StandardPhotoModeAvailable, Is.True);
            Assert.That(editions.IsAvailable(EditionFeature.AdvancedPhotoMode), Is.False);
        }

        [Test]
        public async Task ExplorerServices_RequireEditionAndCompletedExpedition()
        {
            var editions = new EditionFeatureService(
                new FakeStore(Verified("explorer_edition")));
            await editions.InitializeAsync(CancellationToken.None);
            var save = GameSave.CreateNew("save.explorer", 1);
            save.ChapterOne.Phase = ChapterOnePhase.MirraComplete;
            save.Mission = new MissionProgress
            {
                MissionId = "mission.mirra.chapter-one",
                CheckpointNodeId = "node.complete",
                CheckpointOrdinal = 1,
                CompletedNodeIds = new[] { "mission.mirra.chapter-one" },
                ActiveNodeIds = Array.Empty<string>(),
            };

            var modes = GameModeController.CreateForTests(GameMode.Clubhouse);
            await modes.InitializeAsync(CancellationToken.None);
            var scenes = new RecordingSceneTransition();
            var replayService = new ExpeditionReplayService(editions, modes, scenes);
            var replay = await replayService.LaunchReplayAsync(
                save,
                "mission.mirra.chapter-one",
                new[] { ExpeditionModifier.SignalEchoes },
                CancellationToken.None);
            Assert.That(replay.Modifiers,
                Is.EqualTo(new[] { ExpeditionModifier.SignalEchoes }));
            Assert.That(replay.SceneName, Is.EqualTo(MirraProgressionService.FlightSceneName));
            Assert.That(scenes.LastDestination,
                Is.EqualTo(MirraProgressionService.FlightSceneName));
            Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.Flight));
            Assert.That(replayService.ActiveSession, Is.SameAs(replay));
            Assert.That(replay.ReplaySave, Is.Not.SameAs(save));
            Assert.That(replay.ReplaySave.Mission.CheckpointOrdinal, Is.Zero);
            Assert.That(replay.Profile.SignalEchoes, Is.True);
            Assert.That(new DevelopmentArchiveService(editions).AvailableEntries,
                Is.Not.Empty);
            Assert.That(new DevelopmentArchiveService(editions).AvailableEntries[0]
                .ScienceNote, Is.Not.Empty);
            var player = new FakeSoundtrackPlayer();
            var jukebox = new SoundtrackJukeboxController(editions, player);
            Assert.That(jukebox.Select("music.dinner-homecoming"), Is.True);
            Assert.That(jukebox.SelectedTrackId, Is.EqualTo("music.dinner-homecoming"));
            Assert.That(player.LastCueId, Is.EqualTo("cue.dinner.homecoming"));
            jukebox.Stop();
            Assert.That(player.StopCount, Is.EqualTo(1));
        }

        private static EntitlementSnapshot Verified(params string[] entitlements) =>
            new EntitlementSnapshot(
                "player.explorer",
                "fingerprint.explorer",
                StoreEnvironment.GooglePlay,
                "com.scientificaj.justsomestars",
                EntitlementVerification.Verified,
                EntitlementSource.CustomerInfo,
                new DateTime(638923680000000000L, DateTimeKind.Utc),
                Array.ConvertAll(entitlements, value => new ContentId(value)));

        private sealed class FakeStore : IStoreService
        {
            public FakeStore(EntitlementSnapshot snapshot)
            {
                CurrentEntitlements = snapshot;
            }

            public StoreAvailability Availability => StoreAvailability.Available;
            public IReadOnlyList<StoreProduct> Products => Array.Empty<StoreProduct>();
            public EntitlementSnapshot CurrentEntitlements { get; }
            public string StatusMessage => string.Empty;
            public event Action StateChanged { add { } remove { } }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(StartupResult.Available());
            }

            public ValueTask ShutdownAsync() => default;

            public ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<IReadOnlyList<StoreProduct>>(Products);

            public ValueTask<PurchaseResult> PurchaseAsync(
                ContentId productId,
                CancellationToken cancellationToken) =>
                new ValueTask<PurchaseResult>(new PurchaseResult(
                    PurchaseStatus.Unavailable,
                    CurrentEntitlements,
                    "Not used by this test."));

            public ValueTask<EntitlementSnapshot> RestoreAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<EntitlementSnapshot>(CurrentEntitlements);

            public ValueTask<EntitlementSnapshot> RefreshEntitlementsAsync(
                CancellationToken cancellationToken) =>
                new ValueTask<EntitlementSnapshot>(CurrentEntitlements);

            public ValueTask ResumeAsync(CancellationToken cancellationToken) => default;
        }

        private sealed class FakeSoundtrackPlayer : ISoundtrackPlayer
        {
            public string LastCueId { get; private set; } = string.Empty;
            public int StopCount { get; private set; }

            public bool Play(SoundtrackTrack track)
            {
                LastCueId = track.CueId;
                return true;
            }

            public void Stop()
            {
                StopCount++;
            }
        }

        private sealed class RecordingSceneTransition : ISceneTransition
        {
            public string LastDestination { get; private set; } = string.Empty;

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastDestination = destination;
                return default;
            }
        }
    }
}
