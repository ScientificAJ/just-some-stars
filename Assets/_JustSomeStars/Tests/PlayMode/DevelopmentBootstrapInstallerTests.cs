using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Development;
using JustSomeStars.Runtime.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class DevelopmentBootstrapInstallerTests
    {
        private static readonly GameServiceRole[] s_RequiredRoles =
        {
            GameServiceRole.Settings,
            GameServiceRole.LocalSave,
            GameServiceRole.Input,
            GameServiceRole.ContentCatalogue,
            GameServiceRole.ModeController,
        };

        private static readonly Type[] s_DevelopmentServiceTypes =
        {
            typeof(SettingsService),
            typeof(DevelopmentLocalSaveService),
            typeof(InputRouter),
            typeof(DevelopmentContentCatalogueService),
            typeof(DevelopmentModeControllerService),
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameBootstrap.CompositionFactory = null;
            DestroyAllBootstraps();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBootstrap.CompositionFactory = null;
            DestroyAllBootstraps();

            const int cleanupFrames = 5;
            for (var frame = 0; frame < cleanupFrames; frame++)
            {
                yield return null;
            }
        }

        [Test]
        public void RuntimeInstaller_IsTheSoleBeforeSceneLoadFactoryWriter()
        {
            var runtimeAssembly = typeof(GameBootstrap).Assembly;
            var beforeSceneLoadMethods = runtimeAssembly
                .GetTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static))
                .SelectMany(method => method
                    .GetCustomAttributes<RuntimeInitializeOnLoadMethodAttribute>(false)
                    .Where(attribute =>
                        attribute.loadType ==
                        RuntimeInitializeLoadType.BeforeSceneLoad)
                    .Select(_ => method))
                .ToArray();

            var compositionFactorySetter = typeof(GameBootstrap)
                .GetProperty(
                    nameof(GameBootstrap.CompositionFactory),
                    BindingFlags.Public | BindingFlags.Static)
                ?.SetMethod;
            Assert.That(compositionFactorySetter, Is.Not.Null);
            var compositionFactoryWriters = beforeSceneLoadMethods
                .Where(method => CallsMethod(
                    method,
                    compositionFactorySetter))
                .ToArray();

            Assert.That(compositionFactoryWriters, Has.Length.EqualTo(1));
            Assert.That(
                compositionFactoryWriters[0].DeclaringType,
                Is.EqualTo(typeof(DevelopmentBootstrapInstaller)));
            Assert.That(compositionFactoryWriters[0].Name, Is.EqualTo("Install"));
            Assert.That(compositionFactoryWriters[0].GetParameters(), Is.Empty);
            Assert.That(
                compositionFactoryWriters[0].ReturnType,
                Is.EqualTo(typeof(void)));
        }

        [Test]
        public void Install_WhenFactoryIsMissing_InstallsOnceAndKeepsInstalledFactory()
        {
            DevelopmentBootstrapInstaller.Install();
            var installedFactory = GameBootstrap.CompositionFactory;

            DevelopmentBootstrapInstaller.Install();

            Assert.That(installedFactory, Is.Not.Null);
            Assert.That(GameBootstrap.CompositionFactory, Is.SameAs(installedFactory));
        }

        [Test]
        public void Install_WhenExplicitFactoryExists_DoesNotClobberIt()
        {
            Func<GameBootstrapComposition> explicitFactory = () =>
                throw new InvalidOperationException(
                    "The explicit factory must remain installed.");
            GameBootstrap.CompositionFactory = explicitFactory;

            DevelopmentBootstrapInstaller.Install();

            Assert.That(GameBootstrap.CompositionFactory, Is.SameAs(explicitFactory));
        }

        [Test]
        public void InstalledFactory_EachCallCreatesExactFreshRequiredComposition()
        {
            DevelopmentBootstrapInstaller.Install();

            var first = GameBootstrap.CompositionFactory();
            var second = GameBootstrap.CompositionFactory();

            AssertCanonicalDevelopmentComposition(first, expectedTransition: null);
            AssertCanonicalDevelopmentComposition(second, expectedTransition: null);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.SceneTransition, Is.Not.SameAs(first.SceneTransition));

            for (var index = 0; index < first.Services.Count; index++)
            {
                Assert.That(
                    second.Services[index].Service,
                    Is.Not.SameAs(first.Services[index].Service));
            }
        }

        [TestCase(GameServiceRole.Settings)]
        [TestCase(GameServiceRole.LocalSave)]
        [TestCase(GameServiceRole.Input)]
        [TestCase(GameServiceRole.ContentCatalogue)]
        [TestCase(GameServiceRole.ModeController)]
        public async Task MissingRequiredRole_NeverInitializesAnyServiceOrRoutes(
            GameServiceRole missingRole)
        {
            var observer = new RecordingDevelopmentLifecycleObserver();
            var transition = new RecordingTransition();
            var complete = DevelopmentBootstrapInstaller.CreateComposition(
                transition,
                observer);
            var incomplete = new GameBootstrapComposition(
                complete.Services.Where(registration =>
                    registration.Role != missingRole),
                transition);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    incomplete,
                    CancellationToken.None);

                Assert.That(report.IsSuccessful, Is.False);
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(report.Services, Is.Empty);
                Assert.That(
                    report.PrimaryFailure?.Message,
                    Is.EqualTo($"Missing required service roles: {missingRole}."));
                Assert.That(observer.Events, Is.Empty);
                Assert.That(transition.Destinations, Is.Empty);
            }
            finally
            {
                await coordinator.ShutdownAsync();
            }
        }

        [Test]
        public async Task DevelopmentServices_PreCancelledInitializationObservesCancellation()
        {
            var observer = new RecordingDevelopmentLifecycleObserver();
            var composition = DevelopmentBootstrapInstaller.CreateComposition(
                new RecordingTransition(),
                observer);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            foreach (var registration in composition.Services)
            {
                Exception cancellationFailure = null;
                try
                {
                    await registration.Service.InitializeAsync(cancellation.Token);
                }
                catch (Exception exception)
                {
                    cancellationFailure = exception;
                }

                Assert.That(
                    cancellationFailure,
                    Is.InstanceOf<OperationCanceledException>());
            }

            Assert.That(observer.Events, Is.Empty);
        }

        [Test]
        public async Task DevelopmentServices_ShutdownTwiceObservesExactlyOncePerService()
        {
            var observer = new RecordingDevelopmentLifecycleObserver();
            var transition = new RecordingTransition();
            var composition = DevelopmentBootstrapInstaller.CreateComposition(
                transition,
                observer);
            AssertCanonicalDevelopmentComposition(composition, transition);

            foreach (var registration in composition.Services)
            {
                var result = await registration.Service.InitializeAsync(
                    CancellationToken.None);
                Assert.That(result.IsAvailable, Is.True);
            }

            foreach (var registration in composition.Services.Reverse())
            {
                await registration.Service.ShutdownAsync();
                await registration.Service.ShutdownAsync();

                if (registration.Service is DevelopmentRequiredService)
                {
                    Assert.That(
                        observer.InitializeCount(registration.Service),
                        Is.EqualTo(1));
                    Assert.That(
                        observer.ShutdownCount(registration.Service),
                        Is.EqualTo(1));
                }
            }

            Assert.That(
                observer.Events.Count(entry => entry.StartsWith(
                    "initialize:",
                    StringComparison.Ordinal)),
                Is.EqualTo(3));
            Assert.That(
                observer.Events.Count(entry => entry.StartsWith(
                    "shutdown:",
                    StringComparison.Ordinal)),
                Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Boot_InitializesCanonicalServicesOnceRoutesFrontendAndCleansInReverse()
        {
            var observer = new RecordingDevelopmentLifecycleObserver();
            var transition = new RecordingTransition();
            var composition = DevelopmentBootstrapInstaller.CreateComposition(
                transition,
                observer);
            AssertCanonicalDevelopmentComposition(composition, transition);

            var bootstrap = new GameObject("Task5DevelopmentBootstrap")
                .AddComponent<GameBootstrap>();
            var startup = bootstrap.StartupAsync(
                composition,
                CancellationToken.None).AsTask();

            yield return WaitForTask(startup, "development bootstrap startup");
            var report = startup.GetAwaiter().GetResult();

            Assert.That(report.IsSuccessful, Is.True);
            Assert.That(report.RoutedToFrontend, Is.True);
            Assert.That(report.RequestedDestination, Is.EqualTo("Frontend"));
            Assert.That(
                report.Services.Select(service => service.Role),
                Is.EqualTo(s_RequiredRoles));
            Assert.That(
                report.Services.Select(service => service.State),
                Is.All.EqualTo(ServiceStartupState.Available));
            Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
            Assert.That(observer.Events, Is.EqualTo(new[]
            {
                "initialize:DevelopmentLocalSaveService",
                "initialize:DevelopmentContentCatalogueService",
                "initialize:DevelopmentModeControllerService",
            }));
            Assert.That(
                composition.Services.Select(registration => registration.Service)
                    .OfType<DevelopmentRequiredService>()
                    .All(service => observer.InitializeCount(service) == 1),
                Is.True);
            Assert.That(
                composition.Services.Select(registration => registration.Service)
                    .OfType<DevelopmentRequiredService>()
                    .All(service => observer.ShutdownCount(service) == 0),
                Is.True,
                "Successful services must remain owned until explicit shutdown.");

            var firstShutdown = bootstrap.ShutdownAsync().AsTask();
            var secondShutdown = bootstrap.ShutdownAsync().AsTask();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));
            yield return WaitForTask(
                firstShutdown,
                "development bootstrap explicit shutdown");

            Assert.That(observer.Events, Is.EqualTo(new[]
            {
                "initialize:DevelopmentLocalSaveService",
                "initialize:DevelopmentContentCatalogueService",
                "initialize:DevelopmentModeControllerService",
                "shutdown:DevelopmentModeControllerService",
                "shutdown:DevelopmentContentCatalogueService",
                "shutdown:DevelopmentLocalSaveService",
            }));
            Assert.That(
                composition.Services.Select(registration => registration.Service)
                    .OfType<DevelopmentRequiredService>()
                    .All(service => observer.ShutdownCount(service) == 1),
                Is.True);

            UnityEngine.Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        private static void AssertCanonicalDevelopmentComposition(
            GameBootstrapComposition composition,
            ISceneTransition expectedTransition)
        {
            Assert.That(composition, Is.Not.Null);
            if (expectedTransition == null)
            {
                Assert.That(
                    composition.SceneTransition,
                    Is.TypeOf<UnitySceneTransition>());
            }
            else
            {
                Assert.That(
                    composition.SceneTransition,
                    Is.SameAs(expectedTransition));
            }

            Assert.That(composition.Services, Has.Count.EqualTo(5));
            Assert.That(
                composition.Services.Select(registration => registration.Role),
                Is.EqualTo(s_RequiredRoles));
            Assert.That(
                composition.Services.Select(registration => registration.Requirement),
                Is.All.EqualTo(ServiceRequirement.Required));
            Assert.That(
                composition.Services.Select(registration =>
                    registration.Service.GetType()),
                Is.EqualTo(s_DevelopmentServiceTypes));
            Assert.That(
                composition.Services.Select(registration => registration.Service)
                    .Distinct(ReferenceIdentityComparer.Instance)
                    .Count(),
                Is.EqualTo(5));
            Assert.That(
                composition.Services.Any(registration =>
                    registration.Role > GameServiceRole.ModeController),
                Is.False);
        }

        private static IEnumerator WaitForTask(Task task, string operation)
        {
            const int maximumFrames = 120;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                if (task.IsCompleted)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"{operation} did not complete within {maximumFrames} frames.");
        }

        private static bool CallsMethod(MethodInfo method, MethodInfo target)
        {
            var body = method.GetMethodBody();
            var bytes = body?.GetILAsByteArray();
            if (bytes == null)
            {
                return false;
            }

            var oneByte = new OpCode[0x100];
            var twoByte = new OpCode[0x100];
            foreach (var field in typeof(OpCodes).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is not OpCode code)
                {
                    continue;
                }

                var value = unchecked((ushort)code.Value);
                if (value < 0x100)
                {
                    oneByte[value] = code;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    twoByte[value & 0xff] = code;
                }
            }

            for (var offset = 0; offset < bytes.Length;)
            {
                var first = bytes[offset++];
                var code = first == 0xfe
                    ? twoByte[bytes[offset++]]
                    : oneByte[first];
                if (code.OperandType == OperandType.InlineMethod)
                {
                    var token = BitConverter.ToInt32(bytes, offset);
                    var called = method.Module.ResolveMethod(token);
                    if (called.Module == target.Module &&
                        called.MetadataToken == target.MetadataToken)
                    {
                        return true;
                    }
                }

                offset += OperandSize(code.OperandType, bytes, offset);
            }

            return false;
        }

        private static int OperandSize(
            OperandType operandType,
            byte[] bytes,
            int offset)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + (BitConverter.ToInt32(bytes, offset) * 4);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(operandType),
                        operandType,
                        "Unsupported IL operand type.");
            }
        }

        private static void DestroyAllBootstraps()
        {
            foreach (var bootstrap in UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (bootstrap != null)
                {
                    UnityEngine.Object.Destroy(bootstrap.gameObject);
                }
            }
        }

        private sealed class RecordingDevelopmentLifecycleObserver :
            IDevelopmentServiceLifecycleObserver
        {
            private readonly List<string> m_Events = new List<string>();
            private readonly Dictionary<IGameService, int> m_InitializeCounts =
                new Dictionary<IGameService, int>(ReferenceIdentityComparer.Instance);
            private readonly Dictionary<IGameService, int> m_ShutdownCounts =
                new Dictionary<IGameService, int>(ReferenceIdentityComparer.Instance);

            public IReadOnlyList<string> Events => m_Events;

            public void OnInitialized(IGameService service)
            {
                Increment(m_InitializeCounts, service);
                m_Events.Add($"initialize:{service.GetType().Name}");
            }

            public void OnShutdown(IGameService service)
            {
                Increment(m_ShutdownCounts, service);
                m_Events.Add($"shutdown:{service.GetType().Name}");
            }

            public int InitializeCount(IGameService service)
            {
                return Count(m_InitializeCounts, service);
            }

            public int ShutdownCount(IGameService service)
            {
                return Count(m_ShutdownCounts, service);
            }

            private static void Increment(
                IDictionary<IGameService, int> counts,
                IGameService service)
            {
                counts.TryGetValue(service, out var count);
                counts[service] = count + 1;
            }

            private static int Count(
                IReadOnlyDictionary<IGameService, int> counts,
                IGameService service)
            {
                return counts.TryGetValue(service, out var count) ? count : 0;
            }
        }

        private sealed class RecordingTransition : ISceneTransition
        {
            private readonly List<string> m_Destinations = new List<string>();

            public IReadOnlyList<string> Destinations => m_Destinations;

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                m_Destinations.Add(destination);
                return default;
            }
        }

        private sealed class ReferenceIdentityComparer :
            IEqualityComparer<IGameService>
        {
            public static readonly ReferenceIdentityComparer Instance =
                new ReferenceIdentityComparer();

            public bool Equals(IGameService first, IGameService second)
            {
                return ReferenceEquals(first, second);
            }

            public int GetHashCode(IGameService service)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(service);
            }
        }
    }
}
