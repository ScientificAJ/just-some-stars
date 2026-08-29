using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Interaction;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    [Serializable]
    public sealed class TraversalNode2D
    {
        [SerializeField] private string stableId;
        [SerializeField] private Vector2 position;
        [SerializeField] private InteractionDepthBand depthBand;
        [SerializeField] private string[] neighborIds = Array.Empty<string>();

        public TraversalNode2D(
            string id,
            Vector2 position,
            InteractionDepthBand depthBand,
            params string[] neighborIds)
        {
            stableId = id;
            this.position = position;
            this.depthBand = depthBand;
            this.neighborIds = (neighborIds ?? Array.Empty<string>()).ToArray();
            if (!Enum.IsDefined(typeof(InteractionDepthBand), depthBand) ||
                float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                NeighborIds.Distinct().Count() != NeighborIds.Count)
            {
                throw new InvalidOperationException(
                    $"Traversal node '{Id}' has invalid 2D data.");
            }
        }

        public ContentId Id => new ContentId(stableId);
        public Vector2 Position => position;
        public InteractionDepthBand DepthBand => depthBand;
        public IReadOnlyList<ContentId> NeighborIds => neighborIds
            .Select(id => new ContentId(id)).ToArray();
    }

    [Serializable]
    public struct TraversalDepthTransition :
        IEquatable<TraversalDepthTransition>
    {
        [SerializeField] private InteractionDepthBand from;
        [SerializeField] private InteractionDepthBand to;

        public TraversalDepthTransition(
            InteractionDepthBand from,
            InteractionDepthBand to)
        {
            if (!Enum.IsDefined(typeof(InteractionDepthBand), from) ||
                !Enum.IsDefined(typeof(InteractionDepthBand), to) || from == to)
            {
                throw new ArgumentException(
                    "A traversal depth transition requires two distinct bands.");
            }

            this.from = from;
            this.to = to;
        }

        public InteractionDepthBand From => from;
        public InteractionDepthBand To => to;

        public bool Equals(TraversalDepthTransition other)
        {
            return From == other.From && To == other.To;
        }

        public override bool Equals(object obj)
        {
            return obj is TraversalDepthTransition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)From * 397) ^ (int)To;
        }
    }

    [CreateAssetMenu(
        fileName = "TraversalGraph2D",
        menuName = "Just Some Stars/Crew/Traversal Graph 2D")]
    public sealed class TraversalGraph2D : ScriptableObject
    {
        [SerializeField] private TraversalNode2D[] nodes =
            Array.Empty<TraversalNode2D>();
        [SerializeField] private TraversalDepthTransition[] transitions =
            Array.Empty<TraversalDepthTransition>();
        private Dictionary<ContentId, TraversalNode2D> m_Nodes;
        private HashSet<TraversalDepthTransition> m_Transitions;

        public void Configure(
            IEnumerable<TraversalNode2D> nodes,
            IEnumerable<TraversalDepthTransition> transitions)
        {
            if (nodes == null || transitions == null)
            {
                throw new ArgumentNullException(
                    nodes == null ? nameof(nodes) : nameof(transitions));
            }

            this.nodes = nodes.ToArray();
            this.transitions = transitions.ToArray();
            BuildRuntimeGraph();
        }

        private void BuildRuntimeGraph()
        {
            m_Nodes = new Dictionary<ContentId, TraversalNode2D>();
            foreach (var node in nodes ?? Array.Empty<TraversalNode2D>())
            {
                if (node == null || !m_Nodes.TryAdd(node.Id, node))
                {
                    throw new InvalidOperationException(
                        "Traversal graph contains a null or duplicate node.");
                }
            }

            if (m_Nodes.Count == 0)
            {
                throw new InvalidOperationException(
                    "Traversal graph requires at least one node.");
            }

            m_Transitions = new HashSet<TraversalDepthTransition>(
                transitions ?? Array.Empty<TraversalDepthTransition>());
            ValidateEdges();
        }

        public IReadOnlyList<TraversalNode2D> FindPath(
            ContentId startId,
            ContentId goalId)
        {
            EnsureBuilt();
            if (!m_Nodes.ContainsKey(startId) || !m_Nodes.ContainsKey(goalId))
            {
                throw new InvalidOperationException(
                    "Traversal path endpoints must be authored graph nodes.");
            }

            var open = new Queue<ContentId>();
            var previous = new Dictionary<ContentId, ContentId>();
            var visited = new HashSet<ContentId> { startId };
            open.Enqueue(startId);
            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (current == goalId)
                {
                    return Reconstruct(startId, goalId, previous);
                }

                foreach (var neighbor in m_Nodes[current].NeighborIds
                    .OrderBy(id => id.Value, StringComparer.Ordinal))
                {
                    if (visited.Add(neighbor))
                    {
                        previous.Add(neighbor, current);
                        open.Enqueue(neighbor);
                    }
                }
            }

            return Array.Empty<TraversalNode2D>();
        }

        private void OnEnable()
        {
            if (nodes != null && nodes.Length > 0)
            {
                BuildRuntimeGraph();
            }
        }

        private void EnsureBuilt()
        {
            if (m_Nodes == null)
            {
                BuildRuntimeGraph();
            }
        }

        private void ValidateEdges()
        {
            foreach (var node in m_Nodes.Values)
            {
                foreach (var neighborId in node.NeighborIds)
                {
                    if (!m_Nodes.TryGetValue(neighborId, out var neighbor))
                    {
                        throw new InvalidOperationException(
                            $"Traversal node '{node.Id}' references missing node " +
                            $"'{neighborId}'.");
                    }

                    if (node.DepthBand != neighbor.DepthBand &&
                        !m_Transitions.Contains(new TraversalDepthTransition(
                            node.DepthBand,
                            neighbor.DepthBand)))
                    {
                        throw new InvalidOperationException(
                            $"Traversal edge '{node.Id}' to '{neighbor.Id}' " +
                            "requires an authored depth-band transition.");
                    }
                }
            }
        }

        private IReadOnlyList<TraversalNode2D> Reconstruct(
            ContentId start,
            ContentId goal,
            IReadOnlyDictionary<ContentId, ContentId> previous)
        {
            var path = new List<TraversalNode2D> { m_Nodes[goal] };
            var current = goal;
            while (current != start)
            {
                current = previous[current];
                path.Add(m_Nodes[current]);
            }

            path.Reverse();
            return path;
        }
    }
}
