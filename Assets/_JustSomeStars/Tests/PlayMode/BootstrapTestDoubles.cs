using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;

namespace JustSomeStars.Tests.PlayMode
{
    internal static class BootstrapTestComposition
    {
        public static readonly GameServiceRole[] RequiredRoles =
        {
            GameServiceRole.Settings,
            GameServiceRole.LocalSave,
            GameServiceRole.Input,
            GameServiceRole.ContentCatalogue,
            GameServiceRole.ModeController,
        };

        public static IReadOnlyList<GameServiceRegistration> CompleteRequired(
            ICollection<string> events,
            IReadOnlyDictionary<GameServiceRole, IGameService> overrides = null)
        {
            return RequiredRoles
                .Select(role => new GameServiceRegistration(
                    role,
                    overrides != null && overrides.TryGetValue(role, out var service)
                        ? service
                        : RecordingGameService.Available(role.ToString(), events)))
                .ToArray();
        }

        public static IReadOnlyList<GameServiceRegistration> CompleteWithOptional(
            ICollection<string> events,
            IEnumerable<GameServiceRegistration> optionalRegistrations,
            IReadOnlyDictionary<GameServiceRole, IGameService> overrides = null)
        {
            return CompleteRequired(events, overrides)
                .Concat(optionalRegistrations ?? Array.Empty<GameServiceRegistration>())
                .ToArray();
        }
    }

    internal sealed class RecordingGameService : IGameService
    {
        private readonly string m_Name;
        private readonly ICollection<string> m_Events;
        private readonly Func<CancellationToken, ValueTask<StartupResult>> m_Initialize;
        private readonly Func<ValueTask> m_Shutdown;

        public RecordingGameService(
            string name,
            ICollection<string> events,
            Func<CancellationToken, ValueTask<StartupResult>> initialize,
            Func<ValueTask> shutdown = null)
        {
            m_Name = name;
            m_Events = events;
            m_Initialize = initialize;
            m_Shutdown = shutdown ?? (() => default);
        }

        public int InitializeCount { get; private set; }

        public int ShutdownCount { get; private set; }

        public CancellationToken LastInitializeToken { get; private set; }

        public static RecordingGameService Available(
            string name,
            ICollection<string> events,
            Func<ValueTask> shutdown = null)
        {
            return new RecordingGameService(
                name,
                events,
                _ => new ValueTask<StartupResult>(StartupResult.Available()),
                shutdown);
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            InitializeCount++;
            LastInitializeToken = cancellationToken;
            m_Events.Add($"initialize:{m_Name}");
            return m_Initialize(cancellationToken);
        }

        public ValueTask ShutdownAsync()
        {
            ShutdownCount++;
            m_Events.Add($"shutdown:{m_Name}");
            return m_Shutdown();
        }
    }

    internal sealed class RecordingSceneTransition : ISceneTransition
    {
        private readonly ICollection<string> m_Events;
        private readonly Func<string, CancellationToken, ValueTask> m_Route;
        private readonly List<string> m_Destinations = new List<string>();
        private readonly List<CancellationToken> m_Tokens =
            new List<CancellationToken>();

        public RecordingSceneTransition(
            ICollection<string> events,
            Func<string, CancellationToken, ValueTask> route = null)
        {
            m_Events = events;
            m_Route = route ?? ((_, _) => default);
        }

        public IReadOnlyList<string> Destinations => m_Destinations;

        public IReadOnlyList<CancellationToken> Tokens => m_Tokens;

        public ValueTask RouteAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            m_Destinations.Add(destination);
            m_Tokens.Add(cancellationToken);
            m_Events.Add($"route:{destination}");
            return m_Route(destination, cancellationToken);
        }
    }

    internal static class BoundedTestTask
    {
        private const int DefaultTimeoutMilliseconds = 3000;

        public static async Task Complete(
            Task task,
            string operation,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            var completed = await Task.WhenAny(
                task,
                Task.Delay(timeoutMilliseconds));
            Assert.That(
                completed,
                Is.SameAs(task),
                $"{operation} did not complete within {timeoutMilliseconds} ms.");
            await task;
        }

        public static async Task<T> Complete<T>(
            Task<T> task,
            string operation,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            await Complete((Task)task, operation, timeoutMilliseconds);
            return await task;
        }
    }
}
