using System;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;

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
            IChapterProgression progression,
            IStoreService store = null,
            IAccountService account = null,
            ISaveService saves = null)
            : this(settings, input, modes, events, scenes)
        {
            Progression = progression ?? throw new ArgumentNullException(
                nameof(progression));
            Store = store;
            Account = account;
            Saves = saves;
        }

        public SettingsService Settings { get; }
        public InputRouter Input { get; }
        public GameModeController Modes { get; }
        public GameEventBus Events { get; }
        public ISceneTransition Scenes { get; }
        public IChapterProgression Progression { get; }
        public IStoreService Store { get; }
        public IAccountService Account { get; }
        public ISaveService Saves { get; }
    }
}
