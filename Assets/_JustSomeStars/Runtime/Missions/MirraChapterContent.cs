using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Interaction;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    [CreateAssetMenu(
        fileName = "Task19MirraChapter",
        menuName = "Just Some Stars/Missions/Mirra Chapter Content")]
    public sealed class MirraChapterContent : ScriptableObject
    {
        [SerializeField] private MissionDefinition mission;
        [SerializeField] private Task18ProgressionContent progressionContent;
        [SerializeField] private InteractionDefinition probeRepair;
        [SerializeField] private string approachId = "approach.mirra.safe";
        [SerializeField] private string destinationId = "destination.mirra.surface";
        [SerializeField] private string twilightMilestoneId = "route.mirra.twilight";
        [SerializeField] private string hotZoneId = "climate.mirra.hot-side";
        [SerializeField] private string coldZoneId = "climate.mirra.cold-side";
        [SerializeField] private string evidenceId =
            "evidence.mirra.day-night-circulation";
        [SerializeField] private string repairInteractionId =
            "interaction.mirra.probe-repair";
        [SerializeField] private string fragmentId = "fragment.signal.mirra.001";
        [SerializeField] private string departureId =
            "departure.mirra.return-to-flight";
        [SerializeField] private string[] checkpointNodeIds = Array.Empty<string>();
        [SerializeField] private string[] objectiveNodeIds = Array.Empty<string>();
        [SerializeField] private string[] objectiveLocalizationKeys =
            Array.Empty<string>();

        public ContentId StableId => mission.StableId;
        public MissionDefinition Mission => mission;
        public Task18ProgressionContent ProgressionContent => progressionContent;
        public InteractionDefinition ProbeRepair => probeRepair;
        public ContentId ApproachId => new ContentId(approachId);
        public ContentId DestinationId => new ContentId(destinationId);
        public ContentId TwilightMilestoneId => new ContentId(twilightMilestoneId);
        public ContentId HotZoneId => new ContentId(hotZoneId);
        public ContentId ColdZoneId => new ContentId(coldZoneId);
        public ContentId EvidenceId => new ContentId(evidenceId);
        public ContentId RepairInteractionId => new ContentId(repairInteractionId);
        public ContentId FragmentId => new ContentId(fragmentId);
        public ContentId DepartureId => new ContentId(departureId);
        public IReadOnlyList<ContentId> CheckpointNodeIds => checkpointNodeIds
            .Select(id => new ContentId(id))
            .ToArray();

        public void Configure(
            MissionDefinition authoredMission,
            Task18ProgressionContent authoredProgression,
            InteractionDefinition authoredRepair,
            string[] authoredCheckpointNodeIds,
            string[] authoredObjectiveNodeIds,
            string[] authoredObjectiveLocalizationKeys)
        {
            mission = authoredMission;
            progressionContent = authoredProgression;
            probeRepair = authoredRepair;
            checkpointNodeIds = authoredCheckpointNodeIds != null
                ? (string[])authoredCheckpointNodeIds.Clone()
                : null;
            objectiveNodeIds = authoredObjectiveNodeIds != null
                ? (string[])authoredObjectiveNodeIds.Clone()
                : null;
            objectiveLocalizationKeys = authoredObjectiveLocalizationKeys != null
                ? (string[])authoredObjectiveLocalizationKeys.Clone()
                : null;
            ValidateOrThrow();
        }

        public string ResolveObjective(string nodeId)
        {
            ValidateOrThrow();
            var index = Array.IndexOf(objectiveNodeIds, nodeId);
            return index < 0
                ? string.Empty
                : progressionContent.English.Resolve(
                    objectiveLocalizationKeys[index]);
        }

        public void ValidateOrThrow()
        {
            if (mission == null || progressionContent == null || probeRepair == null ||
                checkpointNodeIds == null || checkpointNodeIds.Length != 7 ||
                objectiveNodeIds == null || objectiveLocalizationKeys == null ||
                objectiveNodeIds.Length == 0 ||
                objectiveNodeIds.Length != objectiveLocalizationKeys.Length)
            {
                throw new InvalidOperationException(
                    "Mirra chapter content requires its mission, progression, " +
                    "repair interaction, and seven durable checkpoints.");
            }

            mission.ValidateOrThrow();
            progressionContent.ValidateOrThrow();
            probeRepair.ValidateOrThrow();
            _ = ApproachId;
            _ = DestinationId;
            _ = TwilightMilestoneId;
            _ = HotZoneId;
            _ = ColdZoneId;
            _ = EvidenceId;
            _ = RepairInteractionId;
            _ = FragmentId;
            _ = DepartureId;
            if (probeRepair.StableId != RepairInteractionId ||
                checkpointNodeIds.Select(id => new ContentId(id)).Distinct().Count() != 7 ||
                objectiveNodeIds.Select(id => new ContentId(id)).Distinct().Count() !=
                    objectiveNodeIds.Length)
            {
                throw new InvalidOperationException(
                    "Mirra chapter IDs must be unique and agree with authored content.");
            }

            var missionCheckpoints = mission.Nodes
                .Where(node => node.IsSafeCheckpoint)
                .OrderBy(node => node.CheckpointOrdinal)
                .Select(node => node.StableId.Value)
                .ToArray();
            if (!missionCheckpoints.SequenceEqual(checkpointNodeIds))
            {
                throw new InvalidOperationException(
                    "Mirra checkpoint order must exactly match the mission graph.");
            }

            foreach (var key in objectiveLocalizationKeys)
            {
                _ = progressionContent.English.Resolve(key);
            }
        }
    }
}
