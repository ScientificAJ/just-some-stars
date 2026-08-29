using System;
using JustSomeStars.Runtime.Interaction;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    public sealed class CrewPerception
    {
        public CrewPerception(
            string id,
            CrewAttention attention,
            Vector2 position,
            InteractionDepthBand depthBand,
            CrewActionState suggestedState,
            CrewActionPriority priority,
            float utility,
            bool cameraVisible)
        {
            Id = id;
            Attention = attention;
            Position = position;
            DepthBand = depthBand;
            SuggestedState = suggestedState;
            Priority = priority;
            Utility = utility;
            CameraVisible = cameraVisible;
            _ = ToCandidate();
        }

        public string Id { get; }
        public CrewAttention Attention { get; }
        public Vector2 Position { get; }
        public InteractionDepthBand DepthBand { get; }
        public CrewActionState SuggestedState { get; }
        public CrewActionPriority Priority { get; }
        public float Utility { get; }
        public bool CameraVisible { get; }

        public CrewActionCandidate ToCandidate()
        {
            return new CrewActionCandidate(
                Id,
                SuggestedState,
                Priority,
                Attention,
                Utility,
                Position,
                DepthBand,
                SuggestedState == CrewActionState.Speak ||
                    SuggestedState == CrewActionState.Conversation);
        }
    }
}
