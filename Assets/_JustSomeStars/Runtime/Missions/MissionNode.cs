using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    public enum MissionNodeKind
    {
        Entry = 0,
        Objective = 1,
        Optional = 2,
        Checkpoint = 3,
        Recovery = 4,
        Terminal = 5,
    }

    public enum MissionEventKind
    {
        LandingCompleted = 0,
        PhenomenonObserved = 1,
        PredictionRecorded = 2,
        InstrumentUsed = 3,
        SignalFragmentRecovered = 4,
        ConversationCompleted = 5,
    }

    [Serializable]
    public struct MissionRequirement : IEquatable<MissionRequirement>
    {
        [SerializeField] private MissionEventKind eventKind;
        [SerializeField] private string payloadId;

        public MissionRequirement(MissionEventKind eventKind, string payloadId)
        {
            if (!Enum.IsDefined(typeof(MissionEventKind), eventKind))
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind));
            }

            _ = new ContentId(payloadId);
            this.eventKind = eventKind;
            this.payloadId = payloadId;
        }

        public MissionEventKind EventKind => eventKind;
        public ContentId PayloadId => new ContentId(payloadId);

        public bool Equals(MissionRequirement other) =>
            eventKind == other.eventKind &&
            string.Equals(payloadId, other.payloadId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is MissionRequirement other && Equals(other);

        public override int GetHashCode() =>
            ((int)eventKind * 397) ^ StringComparer.Ordinal.GetHashCode(payloadId ?? string.Empty);
    }

    [Serializable]
    public sealed class MissionNode
    {
        [SerializeField] private string stableId;
        [SerializeField] private MissionNodeKind kind;
        [SerializeField] private MissionRequirement[] requirements =
            Array.Empty<MissionRequirement>();
        [SerializeField] private string[] nextNodeIds = Array.Empty<string>();
        [SerializeField] private string[] dialogueIds = Array.Empty<string>();
        [SerializeField] private string recoveryNodeId;
        [SerializeField, Min(0)] private int checkpointOrdinal;

        public MissionNode(
            string stableId,
            MissionNodeKind kind,
            MissionRequirement[] requirements,
            string[] nextNodeIds,
            string[] dialogueIds,
            string recoveryNodeId,
            int checkpointOrdinal)
        {
            this.stableId = stableId;
            this.kind = kind;
            this.requirements = requirements != null
                ? (MissionRequirement[])requirements.Clone()
                : null;
            this.nextNodeIds = nextNodeIds != null
                ? (string[])nextNodeIds.Clone()
                : null;
            this.dialogueIds = dialogueIds != null
                ? (string[])dialogueIds.Clone()
                : null;
            this.recoveryNodeId = recoveryNodeId;
            this.checkpointOrdinal = checkpointOrdinal;
        }

        public ContentId StableId => new ContentId(stableId);
        public MissionNodeKind Kind => kind;
        public IReadOnlyList<MissionRequirement> Requirements => requirements;
        public IReadOnlyList<string> NextNodeIds => nextNodeIds;
        public IReadOnlyList<string> DialogueIds => dialogueIds;
        public bool HasRecoveryNode => !string.IsNullOrEmpty(recoveryNodeId);
        public ContentId RecoveryNodeId => new ContentId(recoveryNodeId);
        public int CheckpointOrdinal => checkpointOrdinal;
        public bool IsSafeCheckpoint => checkpointOrdinal > 0;

        internal void ValidateOrThrow(string missionId)
        {
            _ = StableId;
            if (!Enum.IsDefined(typeof(MissionNodeKind), kind))
            {
                throw new InvalidOperationException(
                    $"Mission '{missionId}' has an invalid node kind.");
            }

            if (requirements == null || nextNodeIds == null || dialogueIds == null)
            {
                throw new InvalidOperationException(
                    $"Mission node '{stableId}' has missing authored arrays.");
            }

            var requirementSet = new HashSet<MissionRequirement>();
            foreach (var requirement in requirements)
            {
                _ = requirement.PayloadId;
                if (!requirementSet.Add(requirement))
                {
                    throw new InvalidOperationException(
                        $"Mission node '{stableId}' repeats a completion requirement.");
                }
            }

            RequireUniqueCanonicalIds(nextNodeIds, "outgoing node");
            RequireUniqueCanonicalIds(dialogueIds, "dialogue");
            if (!string.IsNullOrEmpty(recoveryNodeId))
            {
                _ = new ContentId(recoveryNodeId);
            }

            if (checkpointOrdinal < 0 ||
                (checkpointOrdinal > 0 && kind == MissionNodeKind.Entry))
            {
                throw new InvalidOperationException(
                    $"Mission node '{stableId}' has an invalid checkpoint ordinal.");
            }

            if ((kind == MissionNodeKind.Entry || kind == MissionNodeKind.Terminal) &&
                requirements.Length != 0)
            {
                throw new InvalidOperationException(
                    $"Mission node '{stableId}' cannot carry event requirements.");
            }

            if (kind == MissionNodeKind.Terminal && nextNodeIds.Length != 0)
            {
                throw new InvalidOperationException(
                    $"Terminal node '{stableId}' cannot link onward.");
            }
        }

        private static void RequireUniqueCanonicalIds(string[] ids, string role)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                _ = new ContentId(id);
                if (!unique.Add(id))
                {
                    throw new InvalidOperationException(
                        $"A mission node repeats {role} '{id}'.");
                }
            }
        }
    }
}
