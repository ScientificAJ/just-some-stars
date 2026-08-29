using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Discovery
{
    public enum EvidenceCompletionOutcome
    {
        ObservationRecorded = 0,
    }

    public sealed class EvidenceRecord
    {
        internal EvidenceRecord(
            int sequence,
            Prediction prediction,
            PhenomenonDefinition phenomenon,
            InstrumentDefinition instrument,
            LensMode mode)
        {
            Sequence = sequence;
            PredictionId = prediction.StableId;
            PhenomenonId = phenomenon.StableId;
            InstrumentId = instrument.StableId;
            Mode = mode;
            PredictionWasCorrect = prediction.IsCorrectFor(phenomenon);
        }

        public int Sequence { get; }

        public ContentId PredictionId { get; }

        public ContentId PhenomenonId { get; }

        public ContentId InstrumentId { get; }

        public LensMode Mode { get; }

        public bool PredictionWasCorrect { get; }

        public bool MissionMayContinue => true;

        public EvidenceCompletionOutcome CompletionOutcome =>
            EvidenceCompletionOutcome.ObservationRecorded;
    }

    public sealed class EvidenceRecorder
    {
        private readonly GameEventBus m_Events;
        private readonly List<EvidenceRecord> m_Records =
            new List<EvidenceRecord>();
        private readonly ReadOnlyCollection<EvidenceRecord> m_ReadOnlyRecords;

        public EvidenceRecorder(GameEventBus gameEvents)
        {
            m_Events = gameEvents ?? throw new ArgumentNullException(
                nameof(gameEvents));
            m_ReadOnlyRecords = m_Records.AsReadOnly();
        }

        public ReadOnlyCollection<EvidenceRecord> Records => m_ReadOnlyRecords;

        public EvidenceRecord Record(
            Prediction prediction,
            PhenomenonDefinition phenomenon,
            InstrumentDefinition instrument,
            LensMode mode)
        {
            if (prediction == null)
            {
                throw new ArgumentNullException(nameof(prediction));
            }

            if (phenomenon == null)
            {
                throw new ArgumentNullException(nameof(phenomenon));
            }

            if (instrument == null)
            {
                throw new ArgumentNullException(nameof(instrument));
            }

            phenomenon.ValidateOrThrow();
            instrument.ValidateOrThrow();
            if (prediction.PhenomenonId != phenomenon.StableId)
            {
                throw new InvalidOperationException(
                    "A prediction can only be recorded against its authored phenomenon.");
            }

            if (!instrument.IsCompatibleWith(phenomenon, mode))
            {
                throw new InvalidOperationException(
                    "The selected instrument and Lens mode cannot observe this phenomenon.");
            }

            var record = new EvidenceRecord(
                m_Records.Count + 1,
                prediction,
                phenomenon,
                instrument,
                mode);
            m_Records.Add(record);
            m_Events.Publish(new PredictionRecorded(record.PredictionId));
            m_Events.Publish(new InstrumentUsed(record.InstrumentId));
            m_Events.Publish(new PhenomenonObserved(record.PhenomenonId));
            return record;
        }
    }
}
