using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Discovery;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    [CreateAssetMenu(
        fileName = "Task18ProgressionContent",
        menuName = "Just Some Stars/Missions/Task 18 Progression Content")]
    public sealed class Task18ProgressionContent : ScriptableObject
    {
        [SerializeField] private MissionDefinition mission;
        [SerializeField] private DialogueEntry[] dialogue = Array.Empty<DialogueEntry>();
        [SerializeField] private AtlasEntry[] atlasEntries = Array.Empty<AtlasEntry>();
        [SerializeField] private ScienceSourceDefinition[] scienceSources =
            Array.Empty<ScienceSourceDefinition>();
        [SerializeField] private LocalizedEnglishCatalog english;
        [SerializeField] private PhenomenonDefinition mirraPhenomenon;
        [SerializeField] private InstrumentDefinition mirraInstrument;
        [SerializeField] private string[] predictionIds = Array.Empty<string>();

        public MissionDefinition Mission => mission;
        public IReadOnlyList<DialogueEntry> Dialogue => dialogue;
        public IReadOnlyList<AtlasEntry> AtlasEntries => atlasEntries;
        public IReadOnlyList<ScienceSourceDefinition> ScienceSources => scienceSources;
        public LocalizedEnglishCatalog English => english;
        public PhenomenonDefinition MirraPhenomenon => mirraPhenomenon;
        public InstrumentDefinition MirraInstrument => mirraInstrument;
        public IReadOnlyList<ContentId> PredictionIds => predictionIds
            .Select(id => new ContentId(id))
            .ToArray();

        public void Configure(
            MissionDefinition authoredMission,
            DialogueEntry[] authoredDialogue,
            AtlasEntry[] authoredAtlas,
            ScienceSourceDefinition[] authoredSources,
            LocalizedEnglishCatalog authoredEnglish,
            PhenomenonDefinition phenomenon,
            InstrumentDefinition instrument,
            string[] authoredPredictionIds)
        {
            mission = authoredMission;
            dialogue = authoredDialogue != null
                ? (DialogueEntry[])authoredDialogue.Clone()
                : null;
            atlasEntries = authoredAtlas != null
                ? (AtlasEntry[])authoredAtlas.Clone()
                : null;
            scienceSources = authoredSources != null
                ? (ScienceSourceDefinition[])authoredSources.Clone()
                : null;
            english = authoredEnglish;
            mirraPhenomenon = phenomenon;
            mirraInstrument = instrument;
            predictionIds = authoredPredictionIds != null
                ? (string[])authoredPredictionIds.Clone()
                : null;
            ValidateOrThrow();
        }

        public DialogueEntry RequireDialogue(string id)
        {
            ValidateOrThrow();
            return dialogue.Single(entry => entry.StableId.Value == id);
        }

        public void ValidateOrThrow()
        {
            if (mission == null || english == null || mirraPhenomenon == null ||
                mirraInstrument == null || dialogue == null || atlasEntries == null ||
                scienceSources == null || dialogue.Length == 0 ||
                atlasEntries.Length == 0 || scienceSources.Length == 0 ||
                predictionIds == null || predictionIds.Length == 0 ||
                dialogue.Any(entry => entry == null) ||
                atlasEntries.Any(entry => entry == null) ||
                scienceSources.Any(source => source == null))
            {
                throw new InvalidOperationException(
                    "Task 18 progression catalog requires every authored dependency.");
            }

            mission.ValidateOrThrow();
            english.ValidateOrThrow();
            mirraPhenomenon.ValidateOrThrow();
            mirraInstrument.ValidateOrThrow();
            var uniquePredictions = new HashSet<ContentId>();
            foreach (var id in predictionIds)
            {
                if (!uniquePredictions.Add(new ContentId(id)))
                {
                    throw new InvalidOperationException(
                        $"Task 18 prediction ID '{id}' is duplicated.");
                }
            }
            foreach (var entry in dialogue)
            {
                entry.ValidateOrThrow();
            }

            foreach (var entry in atlasEntries)
            {
                entry.ValidateOrThrow();
            }

            foreach (var source in scienceSources)
            {
                source.ValidateOrThrow();
            }
        }
    }
}
