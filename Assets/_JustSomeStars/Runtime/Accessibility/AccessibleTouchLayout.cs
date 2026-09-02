using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibleTouchLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform movementControls;
        [SerializeField] private RectTransform actionControls;
        [SerializeField] private RectTransform[] movementGroup =
            Array.Empty<RectTransform>();
        [SerializeField] private RectTransform[] actionGroup =
            Array.Empty<RectTransform>();

        private AnchorRange[] m_MovementRanges = Array.Empty<AnchorRange>();
        private AnchorRange[] m_ActionRanges = Array.Empty<AnchorRange>();
        private bool m_Captured;

        public void Apply(bool leftHanded)
        {
            var movement = ResolveGroup(movementGroup, movementControls);
            var actions = ResolveGroup(actionGroup, actionControls);
            if (movement.Length == 0 || actions.Length == 0)
            {
                return;
            }
            if (!m_Captured)
            {
                m_MovementRanges = Capture(movement);
                m_ActionRanges = Capture(actions);
                m_Captured = true;
            }
            ApplyGroup(movement, m_MovementRanges, leftHanded);
            ApplyGroup(actions, m_ActionRanges, leftHanded);
        }

        private static RectTransform[] ResolveGroup(
            RectTransform[] group,
            RectTransform legacy)
        {
            if (group != null && group.Length > 0)
            {
                return Array.FindAll(group, item => item != null);
            }
            return legacy != null
                ? new[] { legacy }
                : Array.Empty<RectTransform>();
        }

        private static AnchorRange[] Capture(RectTransform[] targets)
        {
            var result = new AnchorRange[targets.Length];
            for (var index = 0; index < targets.Length; index++)
            {
                result[index] = new AnchorRange(
                    targets[index].anchorMin,
                    targets[index].anchorMax);
            }
            return result;
        }

        private static void ApplyGroup(
            RectTransform[] targets,
            AnchorRange[] ranges,
            bool mirrored)
        {
            for (var index = 0;
                 index < targets.Length && index < ranges.Length;
                 index++)
            {
                targets[index].anchorMin = mirrored
                    ? new Vector2(
                        1f - ranges[index].Maximum.x,
                        ranges[index].Minimum.y)
                    : ranges[index].Minimum;
                targets[index].anchorMax = mirrored
                    ? new Vector2(
                        1f - ranges[index].Minimum.x,
                        ranges[index].Maximum.y)
                    : ranges[index].Maximum;
            }
        }

        private readonly struct AnchorRange
        {
            public AnchorRange(Vector2 minimum, Vector2 maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            public Vector2 Minimum { get; }
            public Vector2 Maximum { get; }
        }
    }
}
