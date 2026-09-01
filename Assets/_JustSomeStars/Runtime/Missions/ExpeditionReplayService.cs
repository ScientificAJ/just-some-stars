using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Missions
{
    public sealed class ExpeditionReplaySession
    {
        public ExpeditionReplaySession(
            string missionId,
            string sceneName,
            GameMode mode,
            GameSave replaySave,
            IReadOnlyList<ExpeditionModifier> modifiers,
            ExpeditionModifierProfile profile)
        {
            MissionId = missionId ?? throw new ArgumentNullException(nameof(missionId));
            SceneName = sceneName ?? throw new ArgumentNullException(nameof(sceneName));
            Mode = mode;
            ReplaySave = replaySave ?? throw new ArgumentNullException(nameof(replaySave));
            Modifiers = modifiers ?? Array.Empty<ExpeditionModifier>();
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public string MissionId { get; }
        public string SceneName { get; }
        public GameMode Mode { get; }
        public GameSave ReplaySave { get; }
        public IReadOnlyList<ExpeditionModifier> Modifiers { get; }
        public ExpeditionModifierProfile Profile { get; }
    }

    public sealed class ExpeditionReplayService
    {
        private readonly EditionFeatureService m_Editions;
        private readonly GameModeController m_Modes;
        private readonly ISceneTransition m_Scenes;

        public ExpeditionReplayService(EditionFeatureService editions)
            : this(editions, null, null)
        {
        }

        public ExpeditionReplayService(
            EditionFeatureService editions,
            GameModeController modes,
            ISceneTransition scenes)
        {
            m_Editions = editions ?? throw new ArgumentNullException(nameof(editions));
            if ((modes == null) != (scenes == null))
            {
                throw new ArgumentException(
                    "Replay mode and scene routing must be supplied together.");
            }
            m_Modes = modes;
            m_Scenes = scenes;
        }

        public ExpeditionReplaySession ActiveSession { get; private set; }

        public ExpeditionReplaySession CreateReplay(
            GameSave save,
            string missionId,
            IReadOnlyList<ExpeditionModifier> modifiers)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }
            if (!m_Editions.IsAvailable(EditionFeature.ExpeditionReplay))
            {
                throw new InvalidOperationException(
                    "Expedition Replay is part of Explorer Edition.");
            }
            if (!TryResolveCompletedMission(save, missionId, out var sceneName))
            {
                throw new InvalidOperationException(
                    "Only a completed expedition can be replayed.");
            }

            var selected = (modifiers ?? Array.Empty<ExpeditionModifier>())
                .Distinct()
                .ToArray();
            foreach (var modifier in selected)
            {
                if (!Enum.IsDefined(typeof(ExpeditionModifier), modifier) ||
                    modifier == ExpeditionModifier.None)
                {
                    throw new ArgumentOutOfRangeException(nameof(modifiers));
                }
            }

            var replaySave = save.Copy();
            var replayStartId = $"{missionId}.replay-start";
            replaySave.Mission = new MissionProgress
            {
                MissionId = missionId,
                CheckpointNodeId = replayStartId,
                CheckpointOrdinal = 0,
                CompletedNodeIds = Array.Empty<string>(),
                ActiveNodeIds = new[] { replayStartId },
            };

            return new ExpeditionReplaySession(
                missionId,
                sceneName,
                GameMode.Flight,
                replaySave,
                selected,
                new ExpeditionModifierProfile(
                    selected.Contains(ExpeditionModifier.ReducedHud),
                    selected.Contains(ExpeditionModifier.CinematicLetterbox),
                    selected.Contains(ExpeditionModifier.SignalEchoes),
                    selected.Contains(ExpeditionModifier.CompanionSpotlight),
                    !selected.Contains(ExpeditionModifier.NoDamagePractice)));
        }

        public async ValueTask<ExpeditionReplaySession> LaunchReplayAsync(
            GameSave save,
            string missionId,
            IReadOnlyList<ExpeditionModifier> modifiers,
            CancellationToken cancellationToken)
        {
            if (m_Modes == null || m_Scenes == null)
            {
                throw new InvalidOperationException(
                    "This replay service was not composed with gameplay routing.");
            }

            var session = CreateReplay(save, missionId, modifiers);
            var previous = ActiveSession;
            ActiveSession = session;
            try
            {
                await EnterFlightAsync(cancellationToken);
                await m_Scenes.RouteAsync(session.SceneName, cancellationToken);
                return session;
            }
            catch
            {
                ActiveSession = previous;
                throw;
            }
        }

        public void CompleteReplay()
        {
            ActiveSession = null;
        }

        private async ValueTask EnterFlightAsync(CancellationToken cancellationToken)
        {
            for (var transitions = 0;
                 m_Modes.CurrentMode != GameMode.Flight && transitions < 4;
                 transitions++)
            {
                var next = m_Modes.CurrentMode switch
                {
                    GameMode.Frontend => GameMode.Customization,
                    GameMode.Customization => GameMode.Clubhouse,
                    GameMode.Clubhouse => GameMode.Flight,
                    GameMode.Surface => GameMode.Flight,
                    GameMode.Lens or GameMode.Dialogue or GameMode.Cinematic =>
                        GameMode.Surface,
                    _ => throw new InvalidOperationException(
                        $"Expedition Replay cannot launch from {m_Modes.CurrentMode}."),
                };
                await m_Modes.EnterAsync(next, cancellationToken);
            }

            if (m_Modes.CurrentMode != GameMode.Flight)
            {
                throw new InvalidOperationException(
                    "Expedition Replay could not enter Flight mode.");
            }
        }

        private static bool TryResolveCompletedMission(
            GameSave save,
            string missionId,
            out string sceneName)
        {
            sceneName = string.Empty;
            var phase = save.ChapterOne?.Phase ?? ChapterOnePhase.NotStarted;
            switch (missionId)
            {
                case "mission.mirra.chapter-one"
                    when phase >= ChapterOnePhase.MirraComplete:
                    sceneName = MirraProgressionService.FlightSceneName;
                    return true;
                case KoroVesperProgressionService.MissionId
                    when phase >= ChapterOnePhase.KoroComplete:
                    sceneName = KoroVesperProgressionService.FlightSceneName;
                    return true;
                case AsterVeilProgressionService.MissionId
                    when phase >= ChapterOnePhase.DinnerComplete:
                    sceneName = AsterVeilProgressionService.FlightSceneName;
                    return true;
                default:
                    return false;
            }
        }
    }
}
