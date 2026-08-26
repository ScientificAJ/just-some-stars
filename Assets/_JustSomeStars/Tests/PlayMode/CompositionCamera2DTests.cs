using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class CompositionCamera2DTests
    {
        [Test]
        public void DeadZoneLookAheadAndBounds_PreserveAuthoredComposition()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Target.position = new Vector3(0.5f, 0.25f, 0f);
                Stage1RuntimeReflection.Invoke(fixture.Controller, "Sample", 1f);
                Assert.That(fixture.Camera.transform.position.x,
                    Is.EqualTo(0f).Within(0.001f));
                Assert.That(fixture.Camera.transform.position.y,
                    Is.EqualTo(0f).Within(0.001f));

                fixture.Target.position = new Vector3(4.5f, 2.5f, 0f);
                Stage1RuntimeReflection.Invoke(
                    fixture.Controller,
                    "SetTargetVelocity",
                    new Vector2(5f, 0f));
                Stage1RuntimeReflection.Invoke(fixture.Controller, "Sample", 1f);

                Assert.That(fixture.Camera.transform.position.x,
                    Is.EqualTo(5f).Within(0.001f));
                Assert.That(fixture.Camera.transform.position.y,
                    Is.EqualTo(2f).Within(0.001f));
                Assert.That(fixture.Camera.transform.rotation,
                    Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReducedMotion_RemovesLookAheadAndAppliesBoundedZoom()
        {
            var fixture = CreateFixture();
            try
            {
                var settings = GameSettings.CreateDefaults();
                settings.ReducedMotion = true;
                Stage1RuntimeReflection.Invoke(
                    fixture.Controller,
                    "ApplySettings",
                    settings);

                Assert.That(
                    Stage1RuntimeReflection.Read<float>(
                        fixture.Controller,
                        "EffectiveLookAhead"),
                    Is.EqualTo(0f));
                fixture.Target.position = new Vector3(4f, 0f, 0f);
                Stage1RuntimeReflection.Invoke(
                    fixture.Controller,
                    "SetTargetVelocity",
                    new Vector2(8f, 0f));
                Stage1RuntimeReflection.Invoke(fixture.Controller, "SetZoom", 4.5f);
                Stage1RuntimeReflection.Invoke(fixture.Controller, "Sample", 1f);
                Assert.That(fixture.Camera.orthographicSize,
                    Is.EqualTo(4.5f).Within(0.001f));
                Assert.That(fixture.Camera.transform.position.x,
                    Is.EqualTo(3f).Within(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void CameraPolicy_IsExplicitAndNeverProvidesFreeOrbit()
        {
            var fixture = CreateFixture();
            try
            {
                Stage1RuntimeReflection.Invoke(
                    fixture.Controller,
                    "SetPolicy",
                    GameCameraPolicy.Surface);

                Assert.That(
                    Stage1RuntimeReflection.Read<GameCameraPolicy>(
                        fixture.Controller,
                        "CurrentPolicy"),
                    Is.EqualTo(GameCameraPolicy.Surface));
                Assert.That(
                    Stage1RuntimeReflection.Read<bool>(
                        fixture.Controller,
                        "AllowsFreeOrbit"),
                    Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SerializedSceneBinding_PreservesTheAuthoredInitialZoom()
        {
            var cameraObject = new GameObject("SerializedCompositionCamera");
            var targetObject = new GameObject("SerializedTarget");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                var controller = Stage1RuntimeReflection.AddComponent(
                    cameraObject,
                    "JustSomeStars.Runtime.Player.CompositionCamera2D");
                SetField(controller, "controlledCamera", camera);
                SetField(controller, "target", targetObject.transform);
                SetField(controller, "movementBounds",
                    new Bounds(Vector3.zero, new Vector3(40f, 18f, 1f)));
                SetField(controller, "zoomRange", new Vector2(4.4f, 5.4f));

                InvokeNonPublic(controller, "OnEnable");
                Stage1RuntimeReflection.Invoke(controller, "Sample", 0.02f);

                Assert.That(camera.orthographicSize,
                    Is.EqualTo(5f).Within(0.001f),
                    "Play Mode must begin at the scene-authored composition, " +
                    "not the field initializer's fallback zoom.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void InvokeNonPublic(object target, string name)
        {
            var method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, null);
        }

        private static CameraFixture CreateFixture()
        {
            var cameraObject = new GameObject("CompositionCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var targetObject = new GameObject("Target");
            var controller = Stage1RuntimeReflection.AddComponent(
                cameraObject,
                "JustSomeStars.Runtime.Player.CompositionCamera2D");
            Stage1RuntimeReflection.Invoke(
                controller,
                "Configure",
                camera,
                targetObject.transform,
                new Bounds(Vector3.zero, new Vector3(10f, 6f, 1f)),
                new Vector2(2f, 1f),
                2f,
                0f,
                new Vector2(3f, 6f));
            return new CameraFixture(
                cameraObject,
                targetObject,
                camera,
                targetObject.transform,
                controller);
        }

        private readonly struct CameraFixture
        {
            public CameraFixture(
                GameObject cameraRoot,
                GameObject targetRoot,
                Camera camera,
                Transform target,
                Component controller)
            {
                CameraRoot = cameraRoot;
                TargetRoot = targetRoot;
                Camera = camera;
                Target = target;
                Controller = controller;
            }

            public GameObject CameraRoot { get; }
            public GameObject TargetRoot { get; }
            public Camera Camera { get; }
            public Transform Target { get; }
            public Component Controller { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(CameraRoot);
                Object.DestroyImmediate(TargetRoot);
            }
        }
    }
}
