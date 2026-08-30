using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    [CreateAssetMenu(
        fileName = "MissionDefinition",
        menuName = "Just Some Stars/Missions/Mission Definition")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string entryNodeId;
        [SerializeField] private MissionNode[] nodes = Array.Empty<MissionNode>();

        public ContentId StableId => new ContentId(stableId);
        public ContentId EntryNodeId => new ContentId(entryNodeId);
        public IReadOnlyList<MissionNode> Nodes => nodes;

        public void Configure(string id, string entry, MissionNode[] authoredNodes)
        {
            stableId = id;
            entryNodeId = entry;
            nodes = authoredNodes != null ? (MissionNode[])authoredNodes.Clone() : null;
        }

        public MissionNode RequireNode(ContentId id)
        {
            ValidateOrThrow();
            var node = nodes.SingleOrDefault(candidate => candidate.StableId == id);
            return node ?? throw new KeyNotFoundException(
                $"Mission '{stableId}' has no node '{id}'.");
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            var entry = EntryNodeId;
            if (nodes == null || nodes.Length < 2 || nodes.Any(node => node == null))
            {
                throw new InvalidOperationException(
                    $"Mission '{stableId}' requires a non-null authored graph.");
            }

            foreach (var node in nodes)
            {
                node.ValidateOrThrow(stableId);
            }

            var byId = nodes.GroupBy(node => node.StableId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            if (byId.Any(pair => pair.Value.Length != 1) || !byId.ContainsKey(entry))
            {
                throw new InvalidOperationException(
                    $"Mission '{stableId}' has duplicate nodes or a missing entry node.");
            }

            if (byId[entry][0].Kind != MissionNodeKind.Entry)
            {
                throw new InvalidOperationException(
                    $"Mission '{stableId}' entry must be an Entry node.");
            }

            var terminalCount = nodes.Count(node => node.Kind == MissionNodeKind.Terminal);
            if (terminalCount == 0)
            {
                throw new InvalidOperationException(
                    $"Mission '{stableId}' requires a terminal node.");
            }

            var checkpointOrdinals = new HashSet<int>();
            foreach (var node in nodes)
            {
                foreach (var next in node.NextNodeIds)
                {
                    var target = new ContentId(next);
                    if (!byId.ContainsKey(target))
                    {
                        throw new InvalidOperationException(
                            $"Mission node '{node.StableId}' links to missing '{next}'.");
                    }

                    if (target == node.StableId)
                    {
                        throw new InvalidOperationException(
                            $"Mission node '{node.StableId}' cannot link to itself.");
                    }
                }

                if (node.HasRecoveryNode)
                {
                    if (!byId.TryGetValue(node.RecoveryNodeId, out var recovery) ||
                        recovery[0].Kind == MissionNodeKind.Terminal ||
                        recovery[0].HasRecoveryNode)
                    {
                        throw new InvalidOperationException(
                            $"Mission node '{node.StableId}' has an invalid recovery target.");
                    }
                }

                if (node.CheckpointOrdinal > 0 &&
                    !checkpointOrdinals.Add(node.CheckpointOrdinal))
                {
                    throw new InvalidOperationException(
                        $"Mission '{stableId}' has an ambiguous checkpoint ordinal.");
                }
            }

            var visited = new HashSet<ContentId>();
            var queue = new Queue<ContentId>();
            queue.Enqueue(entry);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                foreach (var next in byId[current][0].NextNodeIds)
                {
                    queue.Enqueue(new ContentId(next));
                }
            }

            if (visited.Count != nodes.Length)
            {
                throw new InvalidOperationException(
                    $"Mission '{stableId}' contains unreachable nodes.");
            }

            foreach (var node in nodes.Where(node =>
                         node.Kind != MissionNodeKind.Terminal &&
                         node.Kind != MissionNodeKind.Optional))
            {
                if (node.NextNodeIds.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Mission node '{node.StableId}' is a required dead end.");
                }
            }
        }
    }
}
