using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;

namespace JustSomeStars.Runtime.UI
{
    public sealed class FrontendDependencies
    {
        public FrontendDependencies(
            SettingsService settings,
            InputRouter input)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public SettingsService Settings { get; }

        public InputRouter Input { get; }
    }
}
