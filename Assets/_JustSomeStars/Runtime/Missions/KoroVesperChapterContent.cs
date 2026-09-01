using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Discovery;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    [CreateAssetMenu(
        fileName = "Task25KoroVesperChapter",
        menuName = "Just Some Stars/Missions/Koro Vesper Chapter")]
    public sealed class KoroVesperChapterContent : ScriptableObject
    {
        [SerializeField] private MissionDefinition mission;
        [SerializeField] private DialogueEntry[] dialogue = Array.Empty<DialogueEntry>();
        [SerializeField] private AtlasEntry geyserAtlas;
        [SerializeField] private ScienceSourceDefinition[] scienceSources =
            Array.Empty<ScienceSourceDefinition>();
        [SerializeField] private LocalizedEnglishCatalog english;
        [SerializeField] private PhenomenonDefinition naturalGeyser;
        [SerializeField] private PhenomenonDefinition signalGeyser;
        [SerializeField] private InstrumentDefinition spectrometer;
        [SerializeField] private string[] predictionIds = Array.Empty<string>();
        [SerializeField] private string[] checkpointNodeIds = Array.Empty<string>();

        public ContentId StableId => mission.StableId;
        public MissionDefinition Mission => mission;
        public IReadOnlyList<DialogueEntry> Dialogue => dialogue;
        public AtlasEntry GeyserAtlas => geyserAtlas;
        public IReadOnlyList<ScienceSourceDefinition> ScienceSources => scienceSources;
        public LocalizedEnglishCatalog English => english;
        public PhenomenonDefinition NaturalGeyser => naturalGeyser;
        public PhenomenonDefinition SignalGeyser => signalGeyser;
        public InstrumentDefinition Spectrometer => spectrometer;
        public IReadOnlyList<ContentId> PredictionIds => predictionIds
            .Select(value => new ContentId(value)).ToArray();
        public IReadOnlyList<ContentId> CheckpointNodeIds => checkpointNodeIds
            .Select(value => new ContentId(value)).ToArray();

        public void Configure(
            MissionDefinition authoredMission,
            DialogueEntry[] authoredDialogue,
            AtlasEntry authoredAtlas,
            ScienceSourceDefinition[] authoredSources,
            LocalizedEnglishCatalog authoredEnglish,
            PhenomenonDefinition authoredNatural,
            PhenomenonDefinition authoredSignal,
            InstrumentDefinition authoredInstrument,
            string[] authoredPredictions,
            string[] authoredCheckpoints)
        {
            mission = authoredMission;
            dialogue = authoredDialogue?.ToArray();
            geyserAtlas = authoredAtlas;
            scienceSources = authoredSources?.ToArray();
            english = authoredEnglish;
            naturalGeyser = authoredNatural;
            signalGeyser = authoredSignal;
            spectrometer = authoredInstrument;
            predictionIds = authoredPredictions?.ToArray();
            checkpointNodeIds = authoredCheckpoints?.ToArray();
            ValidateOrThrow();
        }

        public string ResolveAtlasEnglish(ContentId atlasId, ScienceDepth depth)
        {
            ValidateOrThrow();
            if (geyserAtlas.StableId != atlasId)
            {
                throw new KeyNotFoundException($"Unknown Koro Atlas entry '{atlasId}'.");
            }

            var key = depth switch
            {
                ScienceDepth.Guided => geyserAtlas.ShortTextKey,
                ScienceDepth.Balanced => geyserAtlas.BalancedTextKey,
                ScienceDepth.Deep => geyserAtlas.DeepTextKey,
                _ => throw new ArgumentOutOfRangeException(nameof(depth)),
            };
            return english.Resolve(key);
        }

        public void ValidateOrThrow()
        {
            if (mission == null || geyserAtlas == null || english == null ||
                naturalGeyser == null || signalGeyser == null ||
                spectrometer == null || dialogue == null || dialogue.Length < 3 ||
                dialogue.Any(item => item == null) || scienceSources == null ||
                scienceSources.Length < 3 || scienceSources.Any(item => item == null) ||
                predictionIds == null || predictionIds.Length < 2 ||
                checkpointNodeIds == null || checkpointNodeIds.Length != 6)
            {
                throw new InvalidOperationException(
                    "Koro/Vesper content requires its complete authored science route.");
            }

            mission.ValidateOrThrow();
            geyserAtlas.ValidateOrThrow();
            english.ValidateOrThrow();
            naturalGeyser.ValidateOrThrow();
            signalGeyser.ValidateOrThrow();
            spectrometer.ValidateOrThrow();
            foreach (var item in dialogue)
            {
                item.ValidateOrThrow();
            }
            foreach (var source in scienceSources)
            {
                source.ValidateOrThrow();
            }

            if (naturalGeyser.StableId == signalGeyser.StableId ||
                predictionIds.Select(value => new ContentId(value)).Distinct().Count() !=
                predictionIds.Length ||
                checkpointNodeIds.Select(value => new ContentId(value)).Distinct().Count() !=
                checkpointNodeIds.Length)
            {
                throw new InvalidOperationException(
                    "Koro/Vesper spectra and checkpoints must remain uniquely identified.");
            }
        }
    }
}
