using System;

namespace JustSomeStars.Runtime.Core
{
    public enum PlayerBehaviorOutcome
    {
        IncorrectPrediction = 0,
        IncompatibleInstrument = 1,
        RecoveryRequested = 2,
    }

    public readonly struct PlayerBehaviorObserved
    {
        public PlayerBehaviorObserved(
            ContentId subjectId,
            PlayerBehaviorOutcome outcome)
        {
            SubjectId = GameEventContentId.Require(subjectId, nameof(subjectId));
            if (!Enum.IsDefined(typeof(PlayerBehaviorOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            Outcome = outcome;
        }

        public ContentId SubjectId { get; }
        public ContentId ObjectiveId => SubjectId;
        public PlayerBehaviorOutcome Outcome { get; }
    }

    public readonly struct LandingCompleted
    {
        public LandingCompleted(ContentId destinationId)
        {
            DestinationId = GameEventContentId.Require(
                destinationId,
                nameof(destinationId));
        }

        public ContentId DestinationId { get; }
    }

    public readonly struct ApproachCompleted
    {
        public ApproachCompleted(ContentId approachId)
        {
            ApproachId = GameEventContentId.Require(approachId, nameof(approachId));
        }

        public ContentId ApproachId { get; }
    }

    public readonly struct TraversalMilestoneReached
    {
        public TraversalMilestoneReached(ContentId milestoneId)
        {
            MilestoneId = GameEventContentId.Require(
                milestoneId,
                nameof(milestoneId));
        }

        public ContentId MilestoneId { get; }
    }

    public readonly struct ClimateSampleObserved
    {
        public ClimateSampleObserved(
            ContentId zoneId,
            float temperatureCelsius,
            UnityEngine.Vector2 windAcceleration)
        {
            ZoneId = GameEventContentId.Require(zoneId, nameof(zoneId));
            if (!IsFinite(temperatureCelsius) ||
                !IsFinite(windAcceleration.x) ||
                !IsFinite(windAcceleration.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(temperatureCelsius),
                    "Climate observations must contain finite measurements.");
            }

            TemperatureCelsius = temperatureCelsius;
            WindAcceleration = windAcceleration;
        }

        public ContentId ZoneId { get; }
        public float TemperatureCelsius { get; }
        public UnityEngine.Vector2 WindAcceleration { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct EvidenceAccepted
    {
        public EvidenceAccepted(ContentId evidenceId, ContentId predictionId)
        {
            EvidenceId = GameEventContentId.Require(evidenceId, nameof(evidenceId));
            PredictionId = GameEventContentId.Require(
                predictionId,
                nameof(predictionId));
        }

        public ContentId EvidenceId { get; }
        public ContentId PredictionId { get; }
    }

    public readonly struct InteractionCompleted
    {
        public InteractionCompleted(ContentId interactionId)
        {
            InteractionId = GameEventContentId.Require(
                interactionId,
                nameof(interactionId));
        }

        public ContentId InteractionId { get; }
    }

    public readonly struct DepartureRequested
    {
        public DepartureRequested(ContentId departureId)
        {
            DepartureId = GameEventContentId.Require(
                departureId,
                nameof(departureId));
        }

        public ContentId DepartureId { get; }
    }

    public readonly struct DepartureCompleted
    {
        public DepartureCompleted(ContentId departureId)
        {
            DepartureId = GameEventContentId.Require(
                departureId,
                nameof(departureId));
        }

        public ContentId DepartureId { get; }
    }

    public readonly struct PhenomenonObserved
    {
        public PhenomenonObserved(ContentId phenomenonId)
        {
            PhenomenonId = GameEventContentId.Require(
                phenomenonId,
                nameof(phenomenonId));
        }

        public ContentId PhenomenonId { get; }
    }

    public readonly struct PredictionRecorded
    {
        public PredictionRecorded(ContentId predictionId)
        {
            PredictionId = GameEventContentId.Require(
                predictionId,
                nameof(predictionId));
        }

        public ContentId PredictionId { get; }
    }

    public readonly struct InstrumentUsed
    {
        public InstrumentUsed(ContentId instrumentId)
        {
            InstrumentId = GameEventContentId.Require(
                instrumentId,
                nameof(instrumentId));
        }

        public ContentId InstrumentId { get; }
    }

    public readonly struct SignalFragmentRecovered
    {
        public SignalFragmentRecovered(ContentId fragmentId)
        {
            FragmentId = GameEventContentId.Require(fragmentId, nameof(fragmentId));
        }

        public ContentId FragmentId { get; }
    }

    public readonly struct ConversationCompleted
    {
        public ConversationCompleted(ContentId conversationId)
        {
            ConversationId = GameEventContentId.Require(
                conversationId,
                nameof(conversationId));
        }

        public ContentId ConversationId { get; }
    }

    internal static class GameEventContentId
    {
        public static ContentId Require(ContentId contentId, string parameterName)
        {
            if (!contentId.IsValid)
            {
                throw new ArgumentException(
                    "A typed game event requires a valid content ID.",
                    parameterName);
            }

            return contentId;
        }
    }
}
