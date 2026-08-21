using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class ServiceRegistryTests
    {
        private interface ITestService
        {
        }

        private interface ISecondTestService
        {
        }

        private sealed class TestService : ITestService, ISecondTestService
        {
        }

        [Test]
        public void RegisteringSameContractTwice_Throws()
        {
            var registry = new ServiceRegistry();
            registry.Register<ITestService>(new TestService());

            Assert.Throws<InvalidOperationException>(() =>
                registry.Register<ITestService>(new TestService()));
        }

        [Test]
        public void RegisteringNull_Throws()
        {
            var registry = new ServiceRegistry();

            Assert.Throws<ArgumentNullException>(() =>
                registry.Register<ITestService>(null));
        }

        [Test]
        public void Get_MissingContractThrowsWithContractName()
        {
            var registry = new ServiceRegistry();

            var exception = Assert.Throws<KeyNotFoundException>(() =>
                registry.Get<ITestService>());

            Assert.That(exception.Message, Does.Contain(typeof(ITestService).FullName));
        }

        [Test]
        public void TryGet_MissingContractReturnsFalseAndNull()
        {
            var registry = new ServiceRegistry();

            var found = registry.TryGet<ITestService>(out var service);

            Assert.That(found, Is.False);
            Assert.That(service, Is.Null);
        }

        [Test]
        public void RegisteredContract_ResolvesTheExactInstance()
        {
            var registry = new ServiceRegistry();
            var expected = new TestService();
            registry.Register<ITestService>(expected);

            Assert.That(registry.Get<ITestService>(), Is.SameAs(expected));
            Assert.That(registry.TryGet<ITestService>(out var actual), Is.True);
            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void RegisteredContracts_PreserveRegistrationOrder()
        {
            var registry = new ServiceRegistry();
            var service = new TestService();
            registry.Register<ISecondTestService>(service);
            registry.Register<ITestService>(service);

            Assert.That(registry.RegisteredContracts, Is.EqualTo(new[]
            {
                typeof(ISecondTestService),
                typeof(ITestService),
            }));
        }
    }

    public sealed class BootSceneAssetTests
    {
        private const string BootScenePath =
            "Assets/_JustSomeStars/Scenes/Core/Boot.unity";

        [Test]
        public void BootScene_IsTheFirstEnabledBuildScene()
        {
            var enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(enabledScenePaths, Is.Not.Empty);
            Assert.That(enabledScenePaths[0], Is.EqualTo(BootScenePath));
        }

        [Test]
        public void BootScene_ContainsExactlyOneBootstrapRootAndComponent()
        {
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(
                    BootScenePath,
                    OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();

                Assert.That(roots, Has.Length.EqualTo(1));
                Assert.That(roots[0].name, Is.EqualTo("GameBootstrap"));
                Assert.That(
                    roots[0].GetComponents<Component>().Select(component => component.GetType()),
                    Is.EqualTo(new[]
                    {
                        typeof(Transform),
                        typeof(GameBootstrap),
                    }));
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }
    }
}
