using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Accessibility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class MirraQualityControllerTests
    {
        private const string SceneName = "Mirra";
        private const string QualityTypeName =
            "JustSomeStars.Runtime.Rendering2D.MirraQualityController2D";

        [UnityTest]
        public IEnumerator ProductionMirra_AppliesEveryReachableQualityWithoutGameplayLoss()
        {
            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            var qualityType = FindType(QualityTypeName);
            Assert.That(qualityType, Is.Not.Null);
            var controller = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(qualityType, true))
                .Cast<Component>()
                .Single();

            var expected = new[]
            {
                ("Performance", "mirra.performance", 30, 0.80f, 2, true),
                ("Balanced", "mirra.balanced", 30, 1.00f, 3, false),
                ("Cinematic", "mirra.cinematic", 30, 1.00f, 3, false),
                ("HighFrameRate", "mirra.high-frame-rate", 60, 0.90f, 2, true),
            };
            foreach (var item in expected)
            {
                var quality = Enum.Parse(typeof(PresentationQuality), item.Item1);
                Invoke(controller, "ApplyQuality", quality);
                var qualityCamera = ReadField<Camera>(controller, "qualityCamera");
                yield return null;

                Assert.That(Read<string>(controller, "ActiveProfileId"),
                    Is.EqualTo(item.Item2));
                Assert.That(Read<int>(controller, "ActiveTargetFrameRate"),
                    Is.EqualTo(item.Item3));
                Assert.That(Read<float>(controller, "ActiveRenderScale"),
                    Is.EqualTo(item.Item4).Within(0.001f));
                Assert.That(Read<int>(controller, "ActiveLightCount"),
                    Is.EqualTo(item.Item5));
                Assert.That(Read<bool>(controller,
                        "ActiveUsesScalableBufferPath"),
                    Is.EqualTo(item.Item6),
                    $"{item.Item1} must request URP's scalable-buffer path " +
                    "whenever its render scale is below 1.0.");
                Assert.That(Application.targetFrameRate, Is.EqualTo(item.Item3));
                if (SystemInfo.supportsDynamicResolution)
                {
                    Assert.That(qualityCamera.allowDynamicResolution,
                        Is.EqualTo(item.Item6),
                        $"{item.Item1} must activate the supported camera " +
                        "dynamic-resolution path.");
                    Assert.That(ScalableBufferManager.widthScaleFactor,
                        Is.EqualTo(item.Item4).Within(0.001f));
                    Assert.That(ScalableBufferManager.heightScaleFactor,
                        Is.EqualTo(item.Item4).Within(0.001f));
                }

                foreach (var name in new[]
                {
                    "Captain",
                    "Mira",
                    "Juno",
                    "Ori",
                    "OwnedPlayerShipPresentation",
                    "PlayableRoute",
                    "SignalConsole",
                    "SignalSpireLensTarget",
                })
                {
                    var gameObject = FindNamed(name);
                    Assert.That(gameObject, Is.Not.Null, name);
                    Assert.That(gameObject.activeInHierarchy, Is.True, name);
                }
            }

            Invoke(controller, "RestoreGlobalState");
            Assert.That(Read<string>(controller, "ActiveProfileId"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ProductionMirra_QualityControllerIsLiveSettingsExtension()
        {
            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            var type = FindType(QualityTypeName);
            Assert.That(type, Is.Not.Null);
            Assert.That(type.GetInterfaces().Select(item => item.FullName),
                Does.Contain("JustSomeStars.Runtime.Player.ISurfaceGameplayExtension"));
            Assert.That(type.GetMethod("Configure", BindingFlags.Public |
                BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetMethod("Release", BindingFlags.Public |
                BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetMethod("ApplyQuality", BindingFlags.Public |
                BindingFlags.Instance), Is.Not.Null);
            yield return null;
        }

        private static GameObject FindNamed(string name)
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == name)?.gameObject;
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            return target.GetType().GetMethod(
                method,
                BindingFlags.Public | BindingFlags.Instance)?.Invoke(target, args);
        }

        private static T Read<T>(object target, string property)
        {
            return (T)target.GetType().GetProperty(
                property,
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
        }

        private static T ReadField<T>(object target, string field)
        {
            return (T)target.GetType().GetField(
                field,
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
        }
    }
}
