using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Interaction
{
    public enum InteractionActorKind
    {
        Player = 0,
        Crew = 1,
        Ori = 2,
    }

    public enum InteractionFacing
    {
        Any = 0,
        Left = 1,
        Right = 2,
    }

    public enum InteractionDepthBand
    {
        Gameplay = 0,
        Midground = 1,
        Foreground = 2,
        FarWorld = 3,
    }

    public enum InteractionEventKind
    {
        LandingCompleted = 0,
        PhenomenonObserved = 1,
        PredictionRecorded = 2,
        InstrumentUsed = 3,
        SignalFragmentRecovered = 4,
        ConversationCompleted = 5,
    }

    [Serializable]
    public struct InteractionEventBinding
    {
        [SerializeField] private InteractionEventKind kind;
        [SerializeField] private string contentId;

        public InteractionEventBinding(
            InteractionEventKind eventKind,
            string eventContentId)
        {
            kind = eventKind;
            contentId = eventContentId;
            ValidateOrThrow();
        }

        public InteractionEventKind Kind => kind;

        public ContentId ContentId => new ContentId(contentId);

        public void ValidateOrThrow()
        {
            if (!Enum.IsDefined(typeof(InteractionEventKind), kind))
            {
                throw new InvalidOperationException(
                    $"Unknown interaction event kind '{kind}'.");
            }

            _ = new ContentId(contentId);
        }
    }

    [CreateAssetMenu(
        fileName = "InteractionDefinition",
        menuName = "Just Some Stars/Interaction/Interaction Definition")]
    public sealed class InteractionDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string requiredToolId;
        [SerializeField] private SpriteAnimationClipDefinition playerClip;
        [SerializeField] private SpriteAnimationClipDefinition crewClip;
        [SerializeField] private SpriteAnimationClipDefinition oriClip;
        [SerializeField] private GameMode[] allowedModes = Array.Empty<GameMode>();
        [SerializeField] private InteractionEventBinding[] events =
            Array.Empty<InteractionEventBinding>();
        [SerializeField, Min(0.01f)] private float maxDistance = 2f;
        [SerializeField, Min(0.01f)] private float reservationTimeoutSeconds = 5f;

        public ContentId StableId => new ContentId(stableId);
        public bool RequiresTool => !string.IsNullOrEmpty(requiredToolId);
        public ContentId RequiredToolId => new ContentId(requiredToolId);
        public IReadOnlyList<GameMode> AllowedModes => allowedModes;
        public IReadOnlyList<InteractionEventBinding> Events => events;
        public float MaxDistance => maxDistance;
        public TimeSpan ReservationTimeout =>
            TimeSpan.FromSeconds(reservationTimeoutSeconds);

        public void Configure(
            string id,
            string requiredTool,
            SpriteAnimationClipDefinition playerAction,
            SpriteAnimationClipDefinition crewAction,
            SpriteAnimationClipDefinition oriAction,
            GameMode[] modes,
            InteractionEventBinding[] typedEvents,
            float maxDistance,
            float reservationTimeoutSeconds)
        {
            stableId = id;
            requiredToolId = requiredTool;
            playerClip = playerAction;
            crewClip = crewAction;
            oriClip = oriAction;
            allowedModes = modes != null
                ? (GameMode[])modes.Clone()
                : null;
            events = typedEvents != null
                ? (InteractionEventBinding[])typedEvents.Clone()
                : null;
            this.maxDistance = maxDistance;
            this.reservationTimeoutSeconds = reservationTimeoutSeconds;
            ValidateOrThrow();
        }

        public SpriteAnimationClipDefinition GetClip(
            InteractionActorKind actorKind)
        {
            return actorKind switch
            {
                InteractionActorKind.Player => playerClip,
                InteractionActorKind.Crew => crewClip,
                InteractionActorKind.Ori => oriClip,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(actorKind),
                    actorKind,
                    "Unknown interaction actor kind."),
            };
        }

        public bool AllowsMode(GameMode mode)
        {
            return allowedModes != null && Array.IndexOf(allowedModes, mode) >= 0;
        }

        public bool HasRequiredTool(
            IReadOnlyCollection<ContentId> ownedTools)
        {
            if (!RequiresTool)
            {
                return true;
            }

            if (ownedTools == null)
            {
                return false;
            }

            var required = RequiredToolId;
            foreach (var tool in ownedTools)
            {
                if (tool == required)
                {
                    return true;
                }
            }

            return false;
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            if (!string.IsNullOrEmpty(requiredToolId))
            {
                _ = RequiredToolId;
            }

            playerClip = RequireClip(playerClip, InteractionActorKind.Player);
            crewClip = RequireClip(crewClip, InteractionActorKind.Crew);
            oriClip = RequireClip(oriClip, InteractionActorKind.Ori);

            if (new[]
                {
                    playerClip.StableId,
                    crewClip.StableId,
                    oriClip.StableId,
                }
                .Distinct()
                .Count() != 3)
            {
                throw new InvalidOperationException(
                    $"Interaction '{stableId}' requires a distinct authored clip " +
                    "identity for Player, Crew, and Ori.");
            }

            if (allowedModes == null || allowedModes.Length == 0 ||
                allowedModes.Any(mode => !Enum.IsDefined(typeof(GameMode), mode)) ||
                allowedModes.Distinct().Count() != allowedModes.Length)
            {
                throw new InvalidOperationException(
                    $"Interaction '{stableId}' requires unique valid game modes.");
            }

            if (!IsPositiveFinite(maxDistance))
            {
                throw new InvalidOperationException(
                    $"Interaction '{stableId}' requires a positive max distance.");
            }

            if (!IsPositiveFinite(reservationTimeoutSeconds))
            {
                throw new InvalidOperationException(
                    $"Interaction '{stableId}' requires a positive lease timeout.");
            }

            events ??= Array.Empty<InteractionEventBinding>();
            foreach (var binding in events)
            {
                binding.ValidateOrThrow();
            }

            if (events
                .GroupBy(binding => (binding.Kind, binding.ContentId))
                .Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException(
                    $"Interaction '{stableId}' has duplicate typed events.");
            }
        }

        internal void PublishEvents(GameEventBus eventBus)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            foreach (var binding in events)
            {
                var contentId = binding.ContentId;
                switch (binding.Kind)
                {
                    case InteractionEventKind.LandingCompleted:
                        eventBus.Publish(new LandingCompleted(contentId));
                        break;
                    case InteractionEventKind.PhenomenonObserved:
                        eventBus.Publish(new PhenomenonObserved(contentId));
                        break;
                    case InteractionEventKind.PredictionRecorded:
                        eventBus.Publish(new PredictionRecorded(contentId));
                        break;
                    case InteractionEventKind.InstrumentUsed:
                        eventBus.Publish(new InstrumentUsed(contentId));
                        break;
                    case InteractionEventKind.SignalFragmentRecovered:
                        eventBus.Publish(new SignalFragmentRecovered(contentId));
                        break;
                    case InteractionEventKind.ConversationCompleted:
                        eventBus.Publish(new ConversationCompleted(contentId));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown interaction event kind '{binding.Kind}'.");
                }
            }
        }

        private static SpriteAnimationClipDefinition RequireClip(
            SpriteAnimationClipDefinition clip,
            InteractionActorKind actorKind)
        {
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"Interaction requires an authored {actorKind} clip.");
            }

            clip.ValidateOrThrow();
            return clip;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
