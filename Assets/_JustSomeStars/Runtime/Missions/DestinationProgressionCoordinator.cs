using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Missions
{
    public sealed class DestinationProgressionCoordinator :
        IChapterProgressionCoordinator
    {
        private readonly GameEventBus m_Events;
        private readonly ISaveService m_Saves;
        private readonly SettingsService m_Settings;
        private IChapterProgression m_Active;
        private bool m_Shutdown;

        public DestinationProgressionCoordinator(
            GameEventBus gameEvents,
            ISaveService saves,
            SettingsService settings)
        {
            m_Events = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
            m_Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IChapterProgression ActiveProgression => m_Active;
        public string ActiveChapterId => m_Active?.ChapterId.Value ?? string.Empty;
        public ContentId ChapterId => RequireActive().ChapterId;
        public ContentId ApproachId => RequireActive().ApproachId;
        public string ResumeSceneName => RequireActive().ResumeSceneName;
        public GameMode ResumeMode => RequireActive().ResumeMode;
        public bool HasPendingDeparture => RequireActive().HasPendingDeparture;

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_Active != null)
            {
                return StartupResult.Available();
            }

            if (m_Shutdown)
            {
                throw new InvalidOperationException(
                    "Destination progression cannot initialize after shutdown.");
            }

            var loaded = await m_Saves.LoadAsync(cancellationToken);
            var useKoro = loaded.HasSave && ShouldEnterKoro(loaded.Save);
            m_Active = useKoro
                ? new KoroVesperProgressionService(m_Events, m_Saves, m_Settings)
                : new MirraProgressionService(m_Events, m_Saves, m_Settings);
            var result = await m_Active.InitializeAsync(cancellationToken);
            if (result.State != StartupResultState.Available)
            {
                await m_Active.ShutdownAsync();
                m_Active = null;
            }

            return result;
        }

        public async ValueTask ShutdownAsync()
        {
            if (m_Shutdown)
            {
                return;
            }

            m_Shutdown = true;
            if (m_Active != null)
            {
                await m_Active.ShutdownAsync();
                m_Active = null;
            }
        }

        public bool IsActiveNode(string nodeId) =>
            RequireActive().IsActiveNode(nodeId);

        public Task FlushPendingAsync(CancellationToken cancellationToken) =>
            RequireActive().FlushPendingAsync(cancellationToken);

        public Task ConfirmDepartureAsync(CancellationToken cancellationToken) =>
            RequireActive().ConfirmDepartureAsync(cancellationToken);

        public T RequireActive<T>() where T : class, IChapterProgression
        {
            if (RequireActive() is T typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                $"Active chapter '{ActiveChapterId}' is not {typeof(T).Name}.");
        }

        private IChapterProgression RequireActive() => m_Active ??
            throw new InvalidOperationException(
                "Destination progression must initialize before it is queried.");

        private static bool ShouldEnterKoro(GameSave save)
        {
            var missionId = save.Mission?.MissionId ?? string.Empty;
            if (string.Equals(
                    missionId,
                    KoroVesperProgressionService.MissionId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(
                    missionId,
                    "mission.mirra.chapter-one",
                    StringComparison.Ordinal))
            {
                return save.DiscoveryIds.Contains(
                    KoroVesperProgressionService.FragmentIdValue,
                    StringComparer.Ordinal);
            }

            return save.Mission.CompletedNodeIds.Contains(
                    "mission.mirra.complete",
                    StringComparer.Ordinal) ||
                save.Story.CheckpointOrdinal >= 7;
        }
    }
}
