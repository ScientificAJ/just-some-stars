using System;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Cosmetics;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class SurfaceMotor2DProductionTests
    {
        private const string MotorTypeName =
            "JustSomeStars.Runtime.Player.SurfaceMotor2D";
        private const string CalibrationTypeName =
            "JustSomeStars.Runtime.Player.BodySpriteCalibration";

        [Test]
        public void PersistentWind_IsFixedStepDeterministicAndReportedByState()
        {
            var coarse = CreateFixture();
            var fine = CreateFixture();
            try
            {
                Invoke(coarse.Motor, "SetExternalAcceleration", new Vector2(2f, 0f));
                Invoke(fine.Motor, "SetExternalAcceleration", new Vector2(2f, 0f));
                Simulate(coarse.Motor, 50, 0.02f);
                Simulate(fine.Motor, 100, 0.01f);

                Assert.That(coarse.Body.linearVelocity.x,
                    Is.EqualTo(2f).Within(0.02f));
                Assert.That(fine.Body.linearVelocity.x,
                    Is.EqualTo(2f).Within(0.02f));
                Assert.That(coarse.Body.linearVelocity.x,
                    Is.EqualTo(fine.Body.linearVelocity.x).Within(0.0001f));

                var state = ReadProperty(coarse.Motor, "State");
                Assert.That(Read<Vector2>(state, "ExternalAcceleration"),
                    Is.EqualTo(new Vector2(2f, 0f)));
                Assert.That(Read<Vector2>(state, "RelativeVelocity").x,
                    Is.EqualTo(2f).Within(0.02f));
            }
            finally
            {
                coarse.Dispose();
                fine.Dispose();
            }
        }

        [Test]
        public void SlopeAndStepLimits_AreExplicitAtTheAuthoredBoundary()
        {
            var fixture = CreateFixture();
            try
            {
                SetConfig(fixture.Config, "MaximumSlopeAngle", 45f);
                SetConfig(fixture.Config, "MaximumStepHeight", 0.30f);

                Assert.That((bool)Invoke(
                    fixture.Motor,
                    "IsSurfaceWalkable",
                    NormalForSlope(40f)), Is.True);
                Assert.That((bool)Invoke(
                    fixture.Motor,
                    "IsSurfaceWalkable",
                    NormalForSlope(50f)), Is.False);
                Assert.That((bool)Invoke(
                    fixture.Motor,
                    "CanTraverseStep",
                    0.25f), Is.True);
                Assert.That((bool)Invoke(
                    fixture.Motor,
                    "CanTraverseStep",
                    0.35f), Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ContactedMovingPlatform_IsDerivedWithoutManualVelocityInjection()
        {
            var fixture = CreateFixture(new Vector2(0f, 0.56f));
            var platform = new GameObject("ProductionMovingPlatform");
            try
            {
                platform.transform.position = new Vector3(0f, -0.5f, 0f);
                var platformBody = platform.AddComponent<Rigidbody2D>();
                platformBody.bodyType = RigidbodyType2D.Kinematic;
                platformBody.linearVelocity = new Vector2(1.5f, 0f);
                var platformCollider = platform.AddComponent<BoxCollider2D>();
                platformCollider.size = new Vector2(8f, 1f);
                Physics2D.SyncTransforms();

                Invoke(fixture.Motor, "Simulate", 0.02f);
                var state = ReadProperty(fixture.Motor, "State");
                Assert.That(Read<bool>(state, "IsGrounded"), Is.True);
                Assert.That(Read<Vector2>(state, "ActiveSurfaceVelocity").x,
                    Is.EqualTo(1.5f).Within(0.05f));
                Assert.That(fixture.Body.linearVelocity.x,
                    Is.EqualTo(1.5f).Within(0.05f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(platform);
                fixture.Dispose();
            }
        }

        [Test]
        public void FallCapRunsAfterForcesAndRecoveryClearsTransientState()
        {
            var fixture = CreateFixture(new Vector2(0f, 4f));
            try
            {
                fixture.Body.linearVelocity = new Vector2(3f, -17.9f);
                Invoke(fixture.Motor, "SetExternalAcceleration", new Vector2(0f, -50f));
                Invoke(fixture.Motor, "SetJetHeld", true);
                Invoke(fixture.Motor, "Simulate", 0.02f);
                Assert.That(fixture.Body.linearVelocity.y,
                    Is.GreaterThanOrEqualTo(-18.001f));

                Invoke(fixture.Motor, "SetRecoveryAnchor", new Vector2(-2f, 1f));
                Invoke(fixture.Motor, "Recover");
                var state = ReadProperty(fixture.Motor, "State");
                Assert.That(fixture.Body.position,
                    Is.EqualTo(new Vector2(-2f, 1f)));
                Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(Read<Vector2>(state, "ActiveSurfaceVelocity"),
                    Is.EqualTo(Vector2.zero));
                Assert.That(Read<Vector2>(state, "ExternalAcceleration"),
                    Is.EqualTo(Vector2.zero));
                Assert.That(Read<bool>(state, "IsJetActive"), Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void BodyFamilies_KeepRootScaleAndCapabilityWhileApplyingFitData()
        {
            var root = new GameObject("BodyCalibrationFixture");
            var visual = new GameObject("VisualRoot").transform;
            var shadow = new GameObject("ContactShadow").transform;
            var cameraAnchor = new GameObject("CameraAnchor").transform;
            try
            {
                visual.SetParent(root.transform, false);
                shadow.SetParent(root.transform, false);
                cameraAnchor.SetParent(root.transform, false);
                var capsule = root.AddComponent<CapsuleCollider2D>();
                var calibration = root.AddComponent(RequireType(CalibrationTypeName));
                Invoke(calibration, "Configure", visual, capsule, shadow, cameraAnchor);

                var expectedHeights = new[] { 1.46f, 1.56f, 1.66f };
                var families = new[]
                {
                    CaptainBodyFamily.Compact,
                    CaptainBodyFamily.Average,
                    CaptainBodyFamily.TallBroad,
                };
                var bootBaselines = new float[families.Length];
                for (var index = 0; index < families.Length; index++)
                {
                    Invoke(calibration, "ApplyFamily", families[index]);
                    Assert.That(Read<CaptainBodyFamily>(calibration, "ActiveFamily"),
                        Is.EqualTo(families[index]));
                    Assert.That(Read<float>(calibration, "CalibratedHeight"),
                        Is.EqualTo(expectedHeights[index]).Within(0.02f));
                    Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                    bootBaselines[index] = Read<float>(calibration, "BootBaselineWorldY");
                }

                Assert.That(bootBaselines.Max() - bootBaselines.Min(),
                    Is.LessThanOrEqualTo(0.08f));
                Assert.That(Read<bool>(calibration, "ChangesGameplayCapability"),
                    Is.False);
                Assert.That(Read<bool>(calibration, "ChangesAnimationCadence"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Vector2 NormalForSlope(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
        }

        private static void Simulate(Component motor, int steps, float deltaTime)
        {
            for (var index = 0; index < steps; index++)
            {
                Invoke(motor, "Simulate", deltaTime);
            }
        }

        private static MotorFixture CreateFixture()
        {
            return CreateFixture(new Vector2(0f, 4f));
        }

        private static MotorFixture CreateFixture(Vector2 position)
        {
            var root = new GameObject("ProductionMotorFixture");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.position = position;
            var collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.6f, 1f);
            var config = ScriptableObject.CreateInstance(RequireType(
                "JustSomeStars.Runtime.Player.SurfaceMotor2DConfig"));
            SetConfig(config, "MoveSpeed", 5f);
            SetConfig(config, "GroundAcceleration", 20f);
            SetConfig(config, "AirAcceleration", 10f);
            SetConfig(config, "GroundDeceleration", 24f);
            SetConfig(config, "JumpVelocity", 7f);
            SetConfig(config, "JetAcceleration", 12f);
            SetConfig(config, "JetDuration", 0.35f);
            SetConfig(config, "GroundProbeDistance", 0.1f);
            SetConfig(config, "MaxFallSpeed", 18f);
            var motor = root.AddComponent(RequireType(MotorTypeName));
            Invoke(motor, "Configure", body, collider, config);
            return new MotorFixture(root, body, motor, config);
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

        private static void SetConfig(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, target.GetType().FullName + "." + name);
            property.SetValue(target, value);
        }

        private readonly struct MotorFixture
        {
            public MotorFixture(
                GameObject root,
                Rigidbody2D body,
                Component motor,
                ScriptableObject config)
            {
                Root = root;
                Body = body;
                Motor = motor;
                Config = config;
            }

            public GameObject Root { get; }
            public Rigidbody2D Body { get; }
            public Component Motor { get; }
            public ScriptableObject Config { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Config);
            }
        }
    }
}
