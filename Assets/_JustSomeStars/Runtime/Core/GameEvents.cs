using System;

namespace JustSomeStars.Runtime.Core
{
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
