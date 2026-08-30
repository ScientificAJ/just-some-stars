using System;
using JustSomeStars.Runtime.Accessibility;

namespace JustSomeStars.Runtime.Flight
{
    public readonly struct FlightAssistProfile
    {
        public FlightAssistProfile(
            float steeringCorrection,
            float routeCorrection,
            float safeMargin,
            int storyAccessMask)
        {
            SteeringCorrection = steeringCorrection;
            RouteCorrection = routeCorrection;
            SafeMargin = safeMargin;
            StoryAccessMask = storyAccessMask;
        }

        public float SteeringCorrection { get; }

        public float RouteCorrection { get; }

        public float SafeMargin { get; }

        public int StoryAccessMask { get; }
    }

    public static class FlightAssist
    {
        public static FlightAssistProfile For(AssistLevel level)
        {
            if (!Enum.IsDefined(typeof(AssistLevel), level))
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            const int identicalStoryAccess = -1;
            return level switch
            {
                AssistLevel.Guided => new FlightAssistProfile(
                    5.4f,
                    8.0f,
                    2.2f,
                    identicalStoryAccess),
                AssistLevel.Balanced => new FlightAssistProfile(
                    3.2f,
                    4.8f,
                    1.45f,
                    identicalStoryAccess),
                AssistLevel.Ace => new FlightAssistProfile(
                    1.2f,
                    1.8f,
                    0.75f,
                    identicalStoryAccess),
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };
        }
    }
}
