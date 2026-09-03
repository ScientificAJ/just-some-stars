using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JustSomeStars.Runtime.Core
{
    public sealed class GameServiceRegistration
    {
        public GameServiceRegistration(
            GameServiceRole role,
            IGameService service)
        {
            if (!Enum.IsDefined(typeof(GameServiceRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            Role = role;
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public GameServiceRole Role { get; }

        public IGameService Service { get; }

        public ServiceRequirement Requirement =>
            Role <= GameServiceRole.ModeController ||
            Role == GameServiceRole.Progression ||
            Role == GameServiceRole.QualityProfile
            ? ServiceRequirement.Required
            : ServiceRequirement.Optional;
    }

    public sealed class GameBootstrapComposition
    {
        private static readonly GameServiceRole[] s_RequiredRoles =
        {
            GameServiceRole.Settings,
            GameServiceRole.LocalSave,
            GameServiceRole.Input,
            GameServiceRole.ContentCatalogue,
            GameServiceRole.ModeController,
        };

        public GameBootstrapComposition(
            IEnumerable<GameServiceRegistration> services,
            ISceneTransition sceneTransition,
            Func<string> initialDestinationResolver = null,
            Func<GameMode> initialModeResolver = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var serviceArray = services.ToArray();
            if (serviceArray.Any(service => service == null))
            {
                throw new ArgumentException(
                    "Bootstrap service registrations cannot contain null entries.",
                    nameof(services));
            }

            var duplicateRole = serviceArray
                .GroupBy(service => service.Role)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateRole != null)
            {
                throw new InvalidOperationException(
                    $"Service role '{duplicateRole.Key}' is registered more than once.");
            }

            for (var firstIndex = 0; firstIndex < serviceArray.Length; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1;
                     secondIndex < serviceArray.Length;
                     secondIndex++)
                {
                    var first = serviceArray[firstIndex];
                    var second = serviceArray[secondIndex];
                    if (ReferenceEquals(first.Service, second.Service))
                    {
                        throw new InvalidOperationException(
                            "The same game service instance cannot be registered for " +
                            $"multiple roles: {first.Role}, {second.Role}.");
                    }
                }
            }

            Services = new ReadOnlyCollection<GameServiceRegistration>(serviceArray);
            SceneTransition = sceneTransition ??
                throw new ArgumentNullException(nameof(sceneTransition));
            HasCustomInitialModeResolver = initialModeResolver != null;
            InitialDestinationResolver = initialDestinationResolver ??
                (() => InitialExperiencePolicy.CurrentDestination);
            InitialModeResolver = initialModeResolver ??
                (() => InitialExperiencePolicy.CurrentMode);
        }

        public IReadOnlyList<GameServiceRegistration> Services { get; }

        public ISceneTransition SceneTransition { get; }

        internal Func<string> InitialDestinationResolver { get; }

        internal Func<GameMode> InitialModeResolver { get; }

        internal bool HasCustomInitialModeResolver { get; }

        internal string ResolveInitialDestination()
        {
            var destination = InitialDestinationResolver();
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new InvalidOperationException(
                    "The initial destination resolver returned no scene.");
            }

            return destination;
        }

        internal GameMode ResolveInitialMode()
        {
            var mode = InitialModeResolver();
            if (!Enum.IsDefined(typeof(GameMode), mode))
            {
                throw new InvalidOperationException(
                    "The initial mode resolver returned an invalid mode.");
            }

            return mode;
        }

        internal IReadOnlyList<GameServiceRole> FindMissingRequiredRoles()
        {
            var registeredRoles = new HashSet<GameServiceRole>(
                Services.Select(registration => registration.Role));
            return s_RequiredRoles
                .Where(role => !registeredRoles.Contains(role))
                .ToArray();
        }
    }
}
