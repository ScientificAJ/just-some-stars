using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class ApplicationBootstrapInstallerTests
    {
        private static readonly GameServiceRole[] s_RequiredRoles =
        {
            GameServiceRole.Settings,
            GameServiceRole.LocalSave,
            GameServiceRole.Input,
            GameServiceRole.ContentCatalogue,
            GameServiceRole.ModeController,
        };

        private static readonly Type[] s_ApplicationServiceTypes =
        {
            typeof(SettingsService),
            typeof(CloudCheckpointSaveService),
            typeof(InputRouter),
            typeof(SceneStreamService),
            typeof(GameModeController),
            typeof(FirebaseAccountService),
        };

        private static readonly GameServiceRole[] s_ApplicationRoles =
            s_RequiredRoles.Concat(new[] { GameServiceRole.Cloud }).ToArray();

        private static readonly GameServiceRole[] s_ProductionRoles =
            s_ApplicationRoles.Concat(new[] { GameServiceRole.Progression }).ToArray();

        private static readonly Type[] s_ProductionServiceTypes =
            s_ApplicationServiceTypes.Concat(new[] { typeof(MirraProgressionService) })
                .ToArray();

        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();
        private string m_TestRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_TestRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask8ApplicationBootstrap",
                Guid.NewGuid().ToString("N"));
            GameBootstrap.CompositionFactory = null;
            DestroyAllBootstraps();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBootstrap.CompositionFactory = null;
            DestroyAllBootstraps();
            foreach (var owned in m_OwnedObjects)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            m_OwnedObjects.Clear();
            if (Directory.Exists(m_TestRoot))
            {
                Directory.Delete(m_TestRoot, recursive: true);
            }

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
            var setter = typeof(GameBootstrap)
                .GetProperty(
                    nameof(GameBootstrap.CompositionFactory),
                    BindingFlags.Public | BindingFlags.Static)
                ?.SetMethod;
            Assert.That(setter, Is.Not.Null);

            var writers = beforeSceneLoadMethods
                .Where(method => CallsMethod(method, setter))
                .ToArray();

            Assert.That(writers, Has.Length.EqualTo(1));
            Assert.That(writers[0].DeclaringType,
                Is.EqualTo(typeof(ApplicationBootstrapInstaller)));
            Assert.That(writers[0].Name, Is.EqualTo("Install"));
            Assert.That(writers[0].GetParameters(), Is.Empty);
            Assert.That(writers[0].ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void Install_IsIdempotentAndNeverClobbersExplicitFactory()
        {
            ApplicationBootstrapInstaller.Install();
            var installed = GameBootstrap.CompositionFactory;
            ApplicationBootstrapInstaller.Install();
            Assert.That(installed, Is.Not.Null);
            Assert.That(GameBootstrap.CompositionFactory, Is.SameAs(installed));

            Func<GameBootstrapComposition> explicitFactory = () =>
                throw new InvalidOperationException("explicit factory");
            GameBootstrap.CompositionFactory = explicitFactory;
            ApplicationBootstrapInstaller.Install();
            Assert.That(GameBootstrap.CompositionFactory, Is.SameAs(explicitFactory));
        }

        [Test]
        public void InstalledFactory_EachCallCreatesExactFreshTypedComposition()
        {
            ApplicationBootstrapInstaller.Install();

            var first = GameBootstrap.CompositionFactory();
            var second = GameBootstrap.CompositionFactory();

            AssertCanonicalComposition(first, expectedTransition: null);
            AssertCanonicalComposition(second, expectedTransition: null);
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
        public async Task MissingRequiredRole_NeverInitializesOrRoutes(
            GameServiceRole missingRole)
        {
            var source = new CountingCatalogSource(CreateCatalog());
            var transition = new RecordingTransition();
            var complete = CreateComposition(transition, source);
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
                Assert.That(report.Services, Is.Empty);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(
                    report.PrimaryFailure?.Message,
                    Is.EqualTo($"Missing required service roles: {missingRole}."));
                Assert.That(source.LoadCount, Is.EqualTo(0));
                Assert.That(transition.Destinations, Is.Empty);
            }
            finally
            {
                await coordinator.ShutdownAsync();
            }
        }

        [Test]
        public async Task PreCancelledInitialization_IsObservedByEveryPermanentService()
        {
            var source = new CountingCatalogSource(CreateCatalog());
            var composition = CreateComposition(new RecordingTransition(), source);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            foreach (var registration in composition.Services)
            {
                Assert.That(
                    Assert.CatchAsync<OperationCanceledException>(async () =>
                        await registration.Service.InitializeAsync(
                            cancellation.Token)),
                    Is.InstanceOf<OperationCanceledException>());
            }

            Assert.That(source.LoadCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Boot_InitializesPermanentServicesRoutesAndCleansExactlyOnce()
        {
            AssertInitialExperiencePolicy();
            var source = new CountingCatalogSource(CreateCatalog());
            var transition = new RecordingTransition();
            var composition = CreateComposition(transition, source);
            AssertCanonicalComposition(composition, transition);
            var bootstrap = new GameObject("Task8ApplicationBootstrap")
                .AddComponent<GameBootstrap>();
            var startup = bootstrap.StartupAsync(
                composition,
                CancellationToken.None).AsTask();

            yield return WaitForTask(startup, "application bootstrap startup");
            var report = startup.GetAwaiter().GetResult();

            Assert.That(report.IsSuccessful, Is.True);
            Assert.That(report.RoutedToFrontend, Is.True);
            Assert.That(report.RequestedDestination, Is.EqualTo("Frontend"));
            Assert.That(
                report.Services.Select(service => service.Role),
                Is.EqualTo(s_ApplicationRoles));
            Assert.That(
                report.Services.Select(service => service.State),
                Is.All.EqualTo(ServiceStartupState.Available));
            Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
            Assert.That(source.LoadCount, Is.EqualTo(1));
            Assert.That(source.ReleaseCount, Is.EqualTo(0));
            AssertServicesInitialized(composition, expected: true);

            var firstShutdown = bootstrap.ShutdownAsync().AsTask();
            var secondShutdown = bootstrap.ShutdownAsync().AsTask();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));
            yield return WaitForTask(firstShutdown, "application bootstrap shutdown");

            AssertServicesInitialized(composition, expected: false);
            Assert.That(source.ReleaseCount, Is.EqualTo(1));
            UnityEngine.Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        private static void AssertInitialExperiencePolicy()
        {
            var policyType = typeof(GameBootstrap).Assembly.GetType(
                "JustSomeStars.Runtime.Core.InitialExperiencePolicy");
            Assert.That(
                policyType,
                Is.Not.Null,
                "The player needs an explicit release-versus-internal start policy.");
            var resolveDestination = policyType.GetMethod(
                "ResolveDestination",
                BindingFlags.Static | BindingFlags.NonPublic);
            var resolveMode = policyType.GetMethod(
                "ResolveMode",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveDestination, Is.Not.Null);
            Assert.That(resolveMode, Is.Not.Null);
            Assert.That(
                resolveDestination.Invoke(null, new object[] { false }),
                Is.EqualTo("Frontend"));
            Assert.That(
                resolveMode.Invoke(null, new object[] { false }),
                Is.EqualTo(GameMode.Frontend));
            Assert.That(
                resolveDestination.Invoke(null, new object[] { true }),
                Is.EqualTo("Task17FlightGraybox"));
            Assert.That(
                resolveMode.Invoke(null, new object[] { true }),
                Is.EqualTo(GameMode.Flight));
        }

        private GameBootstrapComposition CreateComposition(
            ISceneTransition transition,
            ISceneCatalogSource source)
        {
            return ApplicationBootstrapInstaller.CreateCompositionForTests(
                transition,
                source,
                new IdleSceneBackend(),
                Path.Combine(m_TestRoot, "settings.json"),
                Path.Combine(m_TestRoot, "save.json"));
        }

        private SceneCatalog CreateCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<SceneCatalog>();
            m_OwnedObjects.Add(catalog);
            catalog.ConfigureForTests(
                SceneCatalog.CurrentSchemaVersion,
                "Frontend",
                GameMode.Frontend,
                Array.Empty<SceneCatalogEntry>());
            return catalog;
        }

        private static void AssertCanonicalComposition(
            GameBootstrapComposition composition,
            ISceneTransition expectedTransition)
        {
            Assert.That(composition, Is.Not.Null);
            var production = expectedTransition == null;
            var expectedRoles = production ? s_ProductionRoles : s_ApplicationRoles;
            var expectedTypes = production
                ? s_ProductionServiceTypes
                : s_ApplicationServiceTypes;
            Assert.That(
                composition.Services.Select(registration => registration.Role),
                Is.EqualTo(expectedRoles));
            Assert.That(
                composition.Services.Select(registration =>
                    registration.Service.GetType()),
                Is.EqualTo(expectedTypes));
            Assert.That(
                composition.Services.Select(registration => registration.Service)
                    .Distinct(ReferenceIdentityComparer.Instance)
                    .Count(),
                Is.EqualTo(expectedRoles.Length));
            Assert.That(
                composition.Services.Select(registration => registration.Requirement),
                Is.EqualTo(expectedRoles.Select(role =>
                    role == GameServiceRole.Cloud
                        ? ServiceRequirement.Optional
                        : ServiceRequirement.Required)));
            Assert.That(composition.Services
                    .Where(registration =>
                        registration.Role > GameServiceRole.ModeController)
                    .Select(registration => registration.Role),
                production
                    ? Is.EqualTo(new[]
                    {
                        GameServiceRole.Cloud,
                        GameServiceRole.Progression,
                    })
                    : Is.EqualTo(new[] { GameServiceRole.Cloud }));

            if (expectedTransition == null)
            {
                Assert.That(composition.SceneTransition,
                    Is.TypeOf<UnitySceneTransition>());
                var surfaceDependencies = typeof(UnitySceneTransition).GetField(
                    "m_SurfaceDependencies",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(composition.SceneTransition);
                Assert.That(surfaceDependencies, Is.Not.Null,
                    "The permanent composition must push its real input, settings " +
                    "and mode services into routed 2.5D gameplay scenes.");
                Assert.That(
                    ReadProperty(surfaceDependencies, "Settings"),
                    Is.SameAs(composition.Services[0].Service));
                Assert.That(
                    ReadProperty(surfaceDependencies, "Input"),
                    Is.SameAs(composition.Services[2].Service));
                Assert.That(
                    ReadProperty(surfaceDependencies, "Modes"),
                    Is.SameAs(composition.Services[4].Service));
                Assert.That(
                    ReadProperty(surfaceDependencies, "Progression"),
                    Is.SameAs(composition.Services[6].Service));

                var frontendDependencies = typeof(UnitySceneTransition).GetField(
                    "m_FrontendDependencies",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(composition.SceneTransition);
                Assert.That(frontendDependencies, Is.Not.Null);
                Assert.That(
                    ReadProperty(frontendDependencies, "Account"),
                    Is.SameAs(composition.Services[5].Service),
                    "Frontend must receive the exact optional Cloud authority.");
            }
            else
            {
                Assert.That(composition.SceneTransition, Is.SameAs(expectedTransition));
            }

            var input = (InputRouter)composition.Services[2].Service;
            var stream = (SceneStreamService)composition.Services[3].Service;
            var modes = (GameModeController)composition.Services[4].Service;
            Assert.That(stream.ModeController, Is.SameAs(modes));
            Assert.That(stream.FallbackTransition, Is.SameAs(composition.SceneTransition));
            Assert.That(modes.RuntimeHooks, Is.TypeOf<InputRouterGameModeRuntimeHooks>());
            Assert.That(
                ((InputRouterGameModeRuntimeHooks)modes.RuntimeHooks).Input,
                Is.SameAs(input));
        }

        private static object ReadProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static void AssertServicesInitialized(
            GameBootstrapComposition composition,
            bool expected)
        {
            Assert.That(((SettingsService)composition.Services[0].Service).IsInitialized,
                Is.EqualTo(expected));
            Assert.That(
                ((CloudCheckpointSaveService)composition.Services[1].Service)
                    .IsInitialized,
                Is.EqualTo(expected));
            Assert.That(((InputRouter)composition.Services[2].Service).IsInitialized,
                Is.EqualTo(expected));
            Assert.That(((SceneStreamService)composition.Services[3].Service).IsInitialized,
                Is.EqualTo(expected));
            Assert.That(((GameModeController)composition.Services[4].Service).IsInitialized,
                Is.EqualTo(expected));
        }

        private T Own<T>(T value)
            where T : UnityEngine.Object
        {
            m_OwnedObjects.Add(value);
            return value;
        }

        private static IEnumerator WaitForTask(Task task, string operation)
        {
            const int maximumFrames = 180;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                if (task.IsCompleted)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"{operation} did not complete within {maximumFrames} frames.");
        }

        private static bool CallsMethod(MethodInfo method, MethodInfo target)
        {
            var bytes = method.GetMethodBody()?.GetILAsByteArray();
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
                var code = first == 0xfe ? twoByte[bytes[offset++]] : oneByte[first];
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
            return operandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or
                    OperandType.ShortInlineI or
                    OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI or
                    OperandType.InlineBrTarget or
                    OperandType.InlineField or
                    OperandType.InlineMethod or
                    OperandType.InlineSig or
                    OperandType.InlineString or
                    OperandType.InlineTok or
                    OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch =>
                    4 + (BitConverter.ToInt32(bytes, offset) * 4),
                _ => throw new ArgumentOutOfRangeException(nameof(operandType)),
            };
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

        private sealed class CountingCatalogSource : ISceneCatalogSource
        {
            private readonly SceneCatalog m_Catalog;

            public CountingCatalogSource(SceneCatalog catalog)
            {
                m_Catalog = catalog;
            }

            public int LoadCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public ValueTask<SceneCatalog> LoadAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LoadCount++;
                return new ValueTask<SceneCatalog>(m_Catalog);
            }

            public ValueTask ReleaseAsync()
            {
                ReleaseCount++;
                Assert.That(ReleaseCount, Is.EqualTo(1));
                return default;
            }
        }

        private sealed class IdleSceneBackend : ISceneStreamBackend
        {
            public object CaptureActiveScene() => null;

            public ISceneLoadHandle BeginLoad(string address, string destinationId)
            {
                throw new AssertionException("Boot must not stream a destination.");
            }

            public void RestoreActiveScene(object sceneToken)
            {
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
                return System.Runtime.CompilerServices.RuntimeHelpers
                    .GetHashCode(service);
            }
        }
    }
}
