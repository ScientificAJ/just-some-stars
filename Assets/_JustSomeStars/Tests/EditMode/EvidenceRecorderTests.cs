using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Rendering2D;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class EvidenceRecorderTests
    {
        private const string MirraPath =
            "Assets/_JustSomeStars/Content/Phenomena/MirraTemperature.asset";
        private const string KoroPath =
            "Assets/_JustSomeStars/Content/Phenomena/KoroSpectrum.asset";
        private const string AsterPath =
            "Assets/_JustSomeStars/Content/Phenomena/AsterMotion.asset";

        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var ownedObject in m_OwnedObjects)
            {
                if (ownedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObject);
                }
            }

            m_OwnedObjects.Clear();
        }

        [Test]
        public void LensMode_DeclaresExactlyTheSixApprovedModes()
        {
            Assert.That(
                Enum.GetNames(typeof(LensMode)),
                Is.EqualTo(new[]
                {
                    "Imaging",
                    "Spectrum",
                    "Temperature",
                    "Atmosphere",
                    "Motion",
                    "Signal",
                }));
        }

        [Test]
        public void IncorrectPrediction_RecordsTruthAndPublishesExactEventsOnce()
        {
            var events = new GameEventBus();
            var recorder = new EvidenceRecorder(events);
            var phenomenon = CreatePhenomenon(
                "phenomenon.mirra.temperature-gradient",
                LensMode.Temperature,
                "hypothesis.mirra.day-night-circulation");
            var instrument = CreateInstrument(
                "instrument.thermal-imager",
                LensMode.Temperature);
            var prediction = new Prediction(
                "prediction.mirra.uniform-temperature",
                phenomenon.StableId.Value,
                "hypothesis.mirra.uniform-temperature");
            var publications = new List<string>();
            using var predictionSubscription = events.Subscribe<PredictionRecorded>(
                item => publications.Add("prediction:" + item.PredictionId.Value));
            using var instrumentSubscription = events.Subscribe<InstrumentUsed>(
                item => publications.Add("instrument:" + item.InstrumentId.Value));
            using var phenomenonSubscription = events.Subscribe<PhenomenonObserved>(
                item => publications.Add("phenomenon:" + item.PhenomenonId.Value));

            var record = recorder.Record(
                prediction,
                phenomenon,
                instrument,
                LensMode.Temperature);

            Assert.That(record.PredictionWasCorrect, Is.False);
            Assert.That(record.MissionMayContinue, Is.True);
            Assert.That(record.PredictionId, Is.EqualTo(prediction.StableId));
            Assert.That(record.InstrumentId, Is.EqualTo(instrument.StableId));
            Assert.That(record.PhenomenonId, Is.EqualTo(phenomenon.StableId));
            Assert.That(recorder.Records, Has.Count.EqualTo(1));
            Assert.That(publications, Is.EqualTo(new[]
            {
                "prediction:" + prediction.StableId.Value,
                "instrument:" + instrument.StableId.Value,
                "phenomenon:" + phenomenon.StableId.Value,
            }));
        }

        [Test]
        public void CorrectAndIncorrectPredictions_HaveIdenticalContinuationContract()
        {
            var phenomenon = CreatePhenomenon(
                "phenomenon.koro.spectrum",
                LensMode.Spectrum,
                "hypothesis.koro.water-vapor");
            var instrument = CreateInstrument(
                "instrument.spectrometer",
                LensMode.Spectrum);
            var recorder = new EvidenceRecorder(new GameEventBus());

            var correct = recorder.Record(
                new Prediction(
                    "prediction.koro.water-vapor",
                    phenomenon.StableId.Value,
                    "hypothesis.koro.water-vapor"),
                phenomenon,
                instrument,
                LensMode.Spectrum);
            var incorrect = recorder.Record(
                new Prediction(
                    "prediction.koro.carbon-dioxide",
                    phenomenon.StableId.Value,
                    "hypothesis.koro.carbon-dioxide"),
                phenomenon,
                instrument,
                LensMode.Spectrum);

            Assert.That(correct.PredictionWasCorrect, Is.True);
            Assert.That(incorrect.PredictionWasCorrect, Is.False);
            Assert.That(correct.MissionMayContinue, Is.True);
            Assert.That(incorrect.MissionMayContinue, Is.True);
            Assert.That(correct.CompletionOutcome, Is.EqualTo(
                incorrect.CompletionOutcome));
        }

        [Test]
        public void RejectedCompatibilityOrMismatchedPrediction_CannotFabricateEvidence()
        {
            var events = new GameEventBus();
            var recorder = new EvidenceRecorder(events);
            var phenomenon = CreatePhenomenon(
                "phenomenon.aster.motion",
                LensMode.Motion,
                "hypothesis.aster.relative-motion");
            var wrongInstrument = CreateInstrument(
                "instrument.camera",
                LensMode.Imaging);
            var rightInstrument = CreateInstrument(
                "instrument.motion-tracker",
                LensMode.Motion);
            var validPrediction = new Prediction(
                "prediction.aster.relative-motion",
                phenomenon.StableId.Value,
                "hypothesis.aster.relative-motion");
            var otherPrediction = new Prediction(
                "prediction.other",
                "phenomenon.other",
                "hypothesis.other");
            var publications = 0;
            using var observed = events.Subscribe<PhenomenonObserved>(
                _ => publications++);

            Assert.Throws<InvalidOperationException>(() => recorder.Record(
                validPrediction,
                phenomenon,
                wrongInstrument,
                LensMode.Motion));
            Assert.Throws<InvalidOperationException>(() => recorder.Record(
                otherPrediction,
                phenomenon,
                rightInstrument,
                LensMode.Motion));

            Assert.That(recorder.Records, Is.Empty);
            Assert.That(publications, Is.Zero);
        }

        [Test]
        public void EvidenceCollection_IsReadOnlyAndSequenceIsStable()
        {
            var recorder = new EvidenceRecorder(new GameEventBus());
            var phenomenon = CreatePhenomenon(
                "phenomenon.mirra.temperature",
                LensMode.Temperature,
                "hypothesis.mirra.gradient");
            var instrument = CreateInstrument(
                "instrument.thermal",
                LensMode.Temperature);

            recorder.Record(
                new Prediction(
                    "prediction.first",
                    phenomenon.StableId.Value,
                    "hypothesis.mirra.gradient"),
                phenomenon,
                instrument,
                LensMode.Temperature);
            recorder.Record(
                new Prediction(
                    "prediction.second",
                    phenomenon.StableId.Value,
                    "hypothesis.mirra.other"),
                phenomenon,
                instrument,
                LensMode.Temperature);

            Assert.That(recorder.Records.Select(item => item.Sequence),
                Is.EqualTo(new[] { 1, 2 }));
            Assert.That(recorder.Records, Is.InstanceOf<
                System.Collections.ObjectModel.ReadOnlyCollection<EvidenceRecord>>());
        }

        [Test]
        public void GuidedBalancedAndDeep_ChangeCopyOnlyNotScientificOutcome()
        {
            var phenomenon = CreatePhenomenon(
                "phenomenon.mirra.temperature",
                LensMode.Temperature,
                "hypothesis.mirra.gradient");
            var instrument = CreateInstrument(
                "instrument.thermal",
                LensMode.Temperature);
            var prediction = new Prediction(
                "prediction.mirra.gradient",
                phenomenon.StableId.Value,
                "hypothesis.mirra.gradient");
            var outcomes = new List<EvidenceCompletionOutcome>();

            foreach (ScienceDepth depth in Enum.GetValues(typeof(ScienceDepth)))
            {
                var recorder = new EvidenceRecorder(new GameEventBus());
                outcomes.Add(recorder.Record(
                    prediction,
                    phenomenon,
                    instrument,
                    LensMode.Temperature).CompletionOutcome);
                Assert.That(
                    phenomenon.GetPresentationKey(depth),
                    Is.Not.Empty);
            }

            Assert.That(outcomes.Distinct().Count(), Is.EqualTo(1));
            Assert.That(
                phenomenon.GetPresentationKey(ScienceDepth.Guided),
                Is.Not.EqualTo(phenomenon.GetPresentationKey(ScienceDepth.Deep)));
        }

        [Test]
        public void Definitions_RejectInvalidOrDuplicateAuthoredContracts()
        {
            var instrument = ScriptableObject.CreateInstance<InstrumentDefinition>();
            var phenomenon = ScriptableObject.CreateInstance<PhenomenonDefinition>();
            m_OwnedObjects.Add(instrument);
            m_OwnedObjects.Add(phenomenon);

            Assert.Throws<InvalidOperationException>(() => instrument.Configure(
                "instrument.invalid",
                new[] { LensMode.Motion, LensMode.Motion },
                0f));
            Assert.Throws<InvalidOperationException>(() => phenomenon.Configure(
                "phenomenon.invalid",
                "science-source.invalid",
                LayerBand.Hud,
                LensFocusBehavior.Point,
                new[] { LensMode.Motion, LensMode.Motion },
                "hypothesis.invalid",
                "hint.invalid",
                "detail.invalid",
                0f));
        }

        [Test]
        public void CanonicalPhenomenonFixtures_AreLoadableUniqueAndCompatible()
        {
            var expected = new[]
            {
                (MirraPath, "phenomenon.mirra.temperature-gradient",
                    LensMode.Temperature),
                (KoroPath, "phenomenon.koro.geyser-spectrum",
                    LensMode.Spectrum),
                (AsterPath, "phenomenon.aster.relative-motion",
                    LensMode.Motion),
            };
            var loaded = expected.Select(item =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<PhenomenonDefinition>(
                    item.Item1);
                Assert.That(asset, Is.Not.Null, item.Item1);
                asset.ValidateOrThrow();
                Assert.That(asset.StableId.Value, Is.EqualTo(item.Item2));
                Assert.That(asset.ObservableModes, Does.Contain(item.Item3));
                return asset;
            }).ToArray();

            Assert.That(
                loaded.Select(item => item.StableId).Distinct().Count(),
                Is.EqualTo(3));
        }

        private InstrumentDefinition CreateInstrument(
            string id,
            params LensMode[] modes)
        {
            var definition = ScriptableObject.CreateInstance<InstrumentDefinition>();
            m_OwnedObjects.Add(definition);
            definition.Configure(id, modes, 0.5f);
            return definition;
        }

        private PhenomenonDefinition CreatePhenomenon(
            string id,
            LensMode mode,
            string correctHypothesisId)
        {
            var definition = ScriptableObject.CreateInstance<PhenomenonDefinition>();
            m_OwnedObjects.Add(definition);
            definition.Configure(
                id,
                "science-source." + id,
                LayerBand.Gameplay,
                LensFocusBehavior.Point,
                new[] { mode },
                correctHypothesisId,
                "hint." + id,
                "detail." + id,
                0.75f);
            return definition;
        }
    }
}
