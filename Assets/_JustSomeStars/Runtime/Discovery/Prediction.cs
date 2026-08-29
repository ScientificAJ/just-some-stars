using System;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Discovery
{
    [Serializable]
    public sealed class Prediction
    {
        [SerializeField] private string stableId;
        [SerializeField] private string phenomenonId;
        [SerializeField] private string hypothesisId;

        public Prediction()
        {
        }

        public Prediction(
            string predictionStableId,
            string predictedPhenomenonId,
            string predictedHypothesisId)
        {
            stableId = predictionStableId;
            phenomenonId = predictedPhenomenonId;
            hypothesisId = predictedHypothesisId;
            ValidateOrThrow();
        }

        public ContentId StableId => new ContentId(stableId);

        public ContentId PhenomenonId => new ContentId(phenomenonId);

        public ContentId HypothesisId => new ContentId(hypothesisId);

        public void ValidateOrThrow()
        {
            _ = StableId;
            _ = PhenomenonId;
            _ = HypothesisId;
        }

        public bool IsCorrectFor(PhenomenonDefinition phenomenon)
        {
            return phenomenon != null &&
                PhenomenonId == phenomenon.StableId &&
                HypothesisId == phenomenon.CorrectHypothesisId;
        }
    }
}
