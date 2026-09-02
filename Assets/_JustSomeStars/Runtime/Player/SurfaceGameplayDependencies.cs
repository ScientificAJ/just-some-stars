using System;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Player
{
    public sealed class SurfaceGameplayDependencies
    {
        public SurfaceGameplayDependencies(
            SettingsService settings,
            InputRouter input,
            GameModeController modes,
            GameEventBus gameEvents)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Modes = modes ?? throw new ArgumentNullException(nameof(modes));
            Events = gameEvents ?? throw new ArgumentNullException(
                nameof(gameEvents));
        }

        public SurfaceGameplayDependencies(
            SettingsService settings,
            InputRouter input,
            GameModeController modes,
            GameEventBus gameEvents,
            ISaveService saves,
            IChapterProgression progression,
            IStoreService store = null,
            IAccountService account = null)
            : this(settings, input, modes, gameEvents)
        {
            Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            ChapterProgression = progression ?? throw new ArgumentNullException(
                nameof(progression));
            Store = store;
            Account = account;
        }

        public SettingsService Settings { get; }

        public InputRouter Input { get; }

        public GameModeController Modes { get; }

        public GameEventBus Events { get; }

        public ISaveService Saves { get; }

        public IChapterProgression ChapterProgression { get; }

        public IStoreService Store { get; }

        public IAccountService Account { get; }

        public MirraProgressionService Progression =>
            ResolveProgression<MirraProgressionService>();

        public T ResolveProgression<T>() where T : class, IChapterProgression
        {
            if (ChapterProgression is T direct)
            {
                return direct;
            }

            if (ChapterProgression is IChapterProgressionCoordinator coordinator)
            {
                return coordinator.RequireActive<T>();
            }

            return null;
        }

        public ISceneTransition Scenes { get; private set; }

        public void ConfigureSceneTransition(ISceneTransition scenes)
        {
            if (scenes == null)
            {
                throw new ArgumentNullException(nameof(scenes));
            }

            if (Scenes != null && !ReferenceEquals(Scenes, scenes))
            {
                throw new InvalidOperationException(
                    "Surface scene transition ownership can only be assigned once.");
            }

            Scenes = scenes;
        }
    }
}
