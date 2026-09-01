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
        fileName = "Task26AsterVeilChapter",
        menuName = "Just Some Stars/Missions/Aster Veil Chapter")]
    public sealed class AsterVeilChapterContent : ScriptableObject
    {
        [SerializeField] private MissionDefinition mission;
        [SerializeField] private PhenomenonDefinition relativeMotion;
        [SerializeField] private InstrumentDefinition motionTracker;
        [SerializeField] private ScienceSourceDefinition scienceSource;
        [SerializeField] private DialogueEntry[] dialogue = Array.Empty<DialogueEntry>();
        [SerializeField] private string[] checkpointNodeIds = Array.Empty<string>();
        [SerializeField] private string authoredSeedId =
            "seed.aster.debris.260826.v1";

        public MissionDefinition Mission => mission;
        public ContentId StableId => mission.StableId;
        public PhenomenonDefinition RelativeMotion => relativeMotion;
        public InstrumentDefinition MotionTracker => motionTracker;
        public ScienceSourceDefinition ScienceSource => scienceSource;
        public IReadOnlyList<DialogueEntry> Dialogue => dialogue;
        public IReadOnlyList<ContentId> CheckpointNodeIds => checkpointNodeIds
            .Select(value => new ContentId(value)).ToArray();
        public ContentId AuthoredSeedId => new(authoredSeedId);

        public void Configure(
            MissionDefinition authoredMission,
            PhenomenonDefinition authoredRelativeMotion,
            InstrumentDefinition authoredMotionTracker,
            ScienceSourceDefinition authoredScienceSource,
            DialogueEntry[] authoredDialogue,
            string[] authoredCheckpoints,
            string seedId)
        {
            mission = authoredMission;
            relativeMotion = authoredRelativeMotion;
            motionTracker = authoredMotionTracker;
            scienceSource = authoredScienceSource;
            dialogue = authoredDialogue?.ToArray();
            checkpointNodeIds = authoredCheckpoints?.ToArray();
            authoredSeedId = seedId;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (mission == null || relativeMotion == null || motionTracker == null ||
                scienceSource == null || dialogue == null || dialogue.Length < 5 ||
                dialogue.Any(item => item == null) || checkpointNodeIds == null ||
                checkpointNodeIds.Length != 9)
            {
                throw new InvalidOperationException(
                    "Aster Veil requires its complete route, science and finale content.");
            }

            mission.ValidateOrThrow();
            relativeMotion.ValidateOrThrow();
            motionTracker.ValidateOrThrow();
            scienceSource.ValidateOrThrow();
            foreach (var entry in dialogue)
            {
                entry.ValidateOrThrow();
            }
            _ = AuthoredSeedId;
            if (checkpointNodeIds.Select(value => new ContentId(value)).Distinct().Count() !=
                checkpointNodeIds.Length)
            {
                throw new InvalidOperationException(
                    "Aster Veil checkpoints must remain uniquely identified.");
            }
        }
    }
}
