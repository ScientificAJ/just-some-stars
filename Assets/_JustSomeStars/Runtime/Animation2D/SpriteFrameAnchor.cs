using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Animation2D
{
    [Serializable]
    public sealed class SpriteFrameAnchor
    {
        [SerializeField] private string id;
        [SerializeField] private Vector2 runtimePixels;
        [SerializeField] private bool isAuthoredVisible;

        public string Id => id;
        public Vector2 RuntimePixels => runtimePixels;
        public bool IsAuthoredVisible => isAuthoredVisible;

        public SpriteFrameAnchor(
            string stableId,
            Vector2 position,
            bool authoredVisible = true)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Anchor id is required.", nameof(stableId));
            }
            id = stableId;
            runtimePixels = position;
            isAuthoredVisible = authoredVisible;
        }
    }

    [Serializable]
    public sealed class SpriteFrameAnchorRow
    {
        [SerializeField] private SpriteFrameAnchor[] anchors =
            Array.Empty<SpriteFrameAnchor>();

        public IReadOnlyList<SpriteFrameAnchor> Anchors => anchors;

        public SpriteFrameAnchorRow(SpriteFrameAnchor[] frameAnchors)
        {
            if (frameAnchors == null || frameAnchors.Length == 0 ||
                frameAnchors.Any(anchor => anchor == null) ||
                frameAnchors.Select(anchor => anchor.Id)
                    .Distinct(StringComparer.Ordinal).Count() != frameAnchors.Length)
            {
                throw new ArgumentException(
                    "Frame anchors must be non-empty and uniquely named.",
                    nameof(frameAnchors));
            }
            anchors = (SpriteFrameAnchor[])frameAnchors.Clone();
        }
    }

    [Serializable]
    public sealed class SpriteClipAnchorTrack
    {
        [SerializeField] private string clipId;
        [SerializeField] private SpriteFrameAnchorRow[] frames =
            Array.Empty<SpriteFrameAnchorRow>();

        public string ClipId => clipId;
        public IReadOnlyList<SpriteFrameAnchorRow> Frames => frames;

        public SpriteClipAnchorTrack(
            string stableClipId,
            SpriteFrameAnchorRow[] frameRows)
        {
            if (string.IsNullOrWhiteSpace(stableClipId) || frameRows == null ||
                frameRows.Length == 0 || frameRows.Any(frame => frame == null))
            {
                throw new ArgumentException(
                    "Anchor track requires an id and complete frame rows.");
            }
            clipId = stableClipId;
            frames = (SpriteFrameAnchorRow[])frameRows.Clone();
        }
    }
}
