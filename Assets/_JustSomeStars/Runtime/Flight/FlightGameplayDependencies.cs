using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;

namespace JustSomeStars.Runtime.Flight
{
    public sealed class FlightGameplayDependencies
    {
        public FlightGameplayDependencies(
            SettingsService settings,
            InputRouter input,
            GameModeController modes,
            GameEventBus events,
            ISceneTransition scenes)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Modes = modes ?? throw new ArgumentNullException(nameof(modes));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
        }

        public FlightGameplayDependencies(
            SettingsService settings,
            InputRouter input,
            GameModeController modes,
            GameEventBus events,
            ISceneTransition scenes,
            MirraProgressionService progression)
            : this(settings, input, modes, events, scenes)
        {
            Progression = progression ?? throw new ArgumentNullException(
                nameof(progression));
        }

        public SettingsService Settings { get; }
        public InputRouter Input { get; }
        public GameModeController Modes { get; }
        public GameEventBus Events { get; }
        public ISceneTransition Scenes { get; }
        public MirraProgressionService Progression { get; }
    }
}
