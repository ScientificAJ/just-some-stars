using System;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class CompositionCamera2DProductionTests
    {
        private const string ControllerTypeName =
            "JustSomeStars.Runtime.Player.CompositionCamera2D";
        private const string ProfileTypeName =
            "JustSomeStars.Runtime.Player.CompositionCameraProfile";

        [Test]
        public void EveryCameraPolicy_HasOneValidatedAuthoredProfile()
        {
            var fixture = CreateFixture();
            try
            {
                var policies = Enum.GetValues(typeof(GameCameraPolicy))
                    .Cast<GameCameraPolicy>()
                    .ToArray();
                ConfigureProfiles(fixture, policies);
                var configured = (Array)ReadProperty(fixture.Controller, "Profiles");
                Assert.That(configured.Length, Is.EqualTo(policies.Length));

                foreach (var policy in policies)
                {
                    Invoke(fixture.Controller, "SetPolicy", policy);
                    Assert.That(Read<GameCameraPolicy>(
                        fixture.Controller,
                        "CurrentPolicy"), Is.EqualTo(policy));
                    Assert.That(Read<object>(fixture.Controller, "ActiveProfile"),
                        Is.Not.Null, policy.ToString());
                }
                Assert.That(Read<bool>(fixture.Controller, "AllowsFreeOrbit"),
                    Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ContentSafeBounds_AccountForViewportAndZoom()
        {
            var fixture = CreateFixture(1616f / 720f);
            try
            {
                ConfigureProfiles(fixture, AllPolicies());
                fixture.Target.position = new Vector3(100f, 100f, 0f);
                for (var index = 0; index < 120; index++)
                {
                    Invoke(fixture.Controller, "Sample", 1f / 60f);
                }

                Assert.That(fixture.Camera.transform.position.x,
                    Is.EqualTo(2.2667f).Within(0.02f));
                Assert.That(fixture.Camera.transform.position.y,
                    Is.EqualTo(1.5f).Within(0.02f));
                Assert.That(fixture.Camera.transform.rotation,
                    Is.EqualTo(Quaternion.identity));
                Assert.That(fixture.Camera.orthographic, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReducedMotion_RemovesVelocityEffectsButKeepsTrackingUsable()
        {
            var fixture = CreateFixture();
            try
            {
                ConfigureProfiles(fixture, AllPolicies());
                var settings = GameSettings.CreateDefaults();
                settings.ReducedMotion = true;
                Invoke(fixture.Controller, "ApplySettings", settings);
                Invoke(fixture.Controller, "SetTargetVelocity", new Vector2(9f, 4f));
                fixture.Target.position = new Vector3(5f, 0f, 0f);
                Invoke(fixture.Controller, "Sample", 0.02f);

                Assert.That(Read<float>(fixture.Controller, "EffectiveLookAhead"),
                    Is.EqualTo(0f));
                Assert.That(Read<bool>(fixture.Controller, "VelocityZoomEnabled"),
                    Is.False);
                Assert.That(fixture.Camera.transform.position.x,
                    Is.GreaterThan(0f),
                    "Reduced motion must not freeze composition tracking.");
                Assert.That(fixture.Camera.transform.position.x,
                    Is.LessThan(5f),
                    "Reduced motion must not introduce a same-frame snap.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ThirtyAndSixtyHertzTraces_ConvergeToTheSameComposition()
        {
            var sixty = CreateFixture();
            var thirty = CreateFixture();
            try
            {
                ConfigureProfiles(sixty, AllPolicies());
                ConfigureProfiles(thirty, AllPolicies());
                sixty.Target.position = new Vector3(5f, 1f, 0f);
                thirty.Target.position = sixty.Target.position;
                for (var index = 0; index < 60; index++)
                {
                    Invoke(sixty.Controller, "Sample", 1f / 60f);
                }
                for (var index = 0; index < 30; index++)
                {
                    Invoke(thirty.Controller, "Sample", 1f / 30f);
                }

                Assert.That(sixty.Camera.transform.position.x,
                    Is.EqualTo(thirty.Camera.transform.position.x).Within(0.02f));
                Assert.That(sixty.Camera.transform.position.y,
                    Is.EqualTo(thirty.Camera.transform.position.y).Within(0.02f));
                Assert.That(sixty.Camera.orthographicSize,
                    Is.EqualTo(thirty.Camera.orthographicSize).Within(0.01f));
            }
            finally
            {
                sixty.Dispose();
                thirty.Dispose();
            }
        }

        private static CameraFixture CreateFixture(float aspect = 16f / 9f)
        {
            var cameraRoot = new GameObject("ProductionCompositionCamera");
            var camera = cameraRoot.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.aspect = aspect;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var targetRoot = new GameObject("PrimaryCameraTarget");
            var controller = cameraRoot.AddComponent(RequireType(ControllerTypeName));
            return new CameraFixture(cameraRoot, targetRoot, camera, controller);
        }

        private static GameCameraPolicy[] AllPolicies()
        {
            return Enum.GetValues(typeof(GameCameraPolicy))
                .Cast<GameCameraPolicy>()
                .ToArray();
        }

        private static void ConfigureProfiles(
            CameraFixture fixture,
            GameCameraPolicy[] policies)
        {
            var profileType = RequireType(ProfileTypeName);
            var profiles = Array.CreateInstance(profileType, policies.Length);
            for (var index = 0; index < policies.Length; index++)
            {
                var profile = Activator.CreateInstance(profileType);
                Set(profile, "Policy", policies[index]);
                Set(profile, "DeadZone", new Vector2(2f, 1f));
                Set(profile, "LookAheadDistance", 1.2f);
                Set(profile, "SmoothingSeconds", 0.12f);
                Set(profile, "ZoomRange", new Vector2(2.5f, 4f));
                Set(profile, "DefaultZoom", 3f);
                Set(profile, "CenterRails",
                    new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f)));
                Set(profile, "ContentSafeBounds",
                    new Bounds(Vector3.zero, new Vector3(18f, 9f, 1f)));
                Set(profile, "PrimaryTarget", fixture.Target);
                Set(profile, "CompositionTargets", Array.Empty<Transform>());
                profiles.SetValue(profile, index);
            }

            Invoke(
                fixture.Controller,
                "ConfigureProfiles",
                fixture.Camera,
                profiles,
                policies[0]);
        }

        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", JustSomeStars.Runtime");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(candidate =>
                    candidate.Name == name &&
                    candidate.GetParameters().Length == args.Length);
            Assert.That(method, Is.Not.Null, target.GetType().FullName + "." + name);
            return method.Invoke(target, args);
        }

        private static object ReadProperty(object target, string name)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, target.GetType().FullName + "." + name);
            return property.GetValue(target);
        }

        private static T Read<T>(object target, string name)
        {
            return (T)ReadProperty(target, name);
        }

        private static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, target.GetType().FullName + "." + name);
            property.SetValue(target, value);
        }

        private readonly struct CameraFixture
        {
            public CameraFixture(
                GameObject cameraRoot,
                GameObject targetRoot,
                Camera camera,
                Component controller)
            {
                CameraRoot = cameraRoot;
                TargetRoot = targetRoot;
                Camera = camera;
                Controller = controller;
            }

            public GameObject CameraRoot { get; }
            public GameObject TargetRoot { get; }
            public Camera Camera { get; }
            public Transform Target => TargetRoot.transform;
            public Component Controller { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(CameraRoot);
                UnityEngine.Object.DestroyImmediate(TargetRoot);
            }
        }
    }
}
