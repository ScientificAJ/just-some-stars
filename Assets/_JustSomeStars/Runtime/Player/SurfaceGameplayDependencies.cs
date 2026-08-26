using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;

namespace JustSomeStars.Runtime.Player
{
    public sealed class SurfaceGameplayDependencies
    {
        public SurfaceGameplayDependencies(
            SettingsService settings,
            InputRouter input,
            GameModeController modes)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Modes = modes ?? throw new ArgumentNullException(nameof(modes));
        }

        public SettingsService Settings { get; }

        public InputRouter Input { get; }

        public GameModeController Modes { get; }
    }
}
