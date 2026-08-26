using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Input;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class SurfaceMotor2DTests
    {
        [Test]
        public void FixedStepMovement_ReachesConfiguredSpeedDeterministically()
        {
            var fixture = CreateFixture();
            try
            {
                Stage1RuntimeReflection.Invoke(
                    fixture.Motor,
                    "SetMoveInput",
                    Vector2.right);
                for (var step = 0; step < 40; step++)
                {
                    Stage1RuntimeReflection.Invoke(fixture.Motor, "Simulate", 0.02f);
                }

                Assert.That(fixture.Body.linearVelocity.x,
                    Is.EqualTo(5f).Within(0.02f));
                Assert.That(fixture.Body.linearVelocity.y,
                    Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void JumpAndJet_UseGroundedStateAndBoundedAssistance()
        {
            var fixture = CreateFixture();
            var ground = CreateGround();
            try
            {
                Physics2D.SyncTransforms();
                Stage1RuntimeReflection.Invoke(fixture.Motor, "RequestJump");
                Stage1RuntimeReflection.Invoke(fixture.Motor, "Simulate", 0.02f);
                Assert.That(fixture.Body.linearVelocity.y,
                    Is.EqualTo(7f).Within(0.05f));
                Assert.That(
                    Stage1RuntimeReflection.Read<bool>(fixture.Motor, "IsGrounded"),
                    Is.True);

                fixture.Body.position += Vector2.up * 1.5f;
                Physics2D.SyncTransforms();
                Stage1RuntimeReflection.Invoke(fixture.Motor, "SetJetHeld", true);
                var beforeJet = fixture.Body.linearVelocity.y;
                Stage1RuntimeReflection.Invoke(fixture.Motor, "Simulate", 0.02f);
                Assert.That(fixture.Body.linearVelocity.y,
                    Is.GreaterThan(beforeJet));
                Assert.That(
                    Stage1RuntimeReflection.Read<float>(
                        fixture.Motor,
                        "RemainingJetSeconds"),
                    Is.LessThan(0.35f));
            }
            finally
            {
                Object.DestroyImmediate(ground);
                fixture.Dispose();
            }
        }

        [Test]
        public void SlopeProjectionMovingSurfaceExternalVelocityAndRecovery_AreExplicit()
        {
            var fixture = CreateFixture();
            try
            {
                var slopeNormal = new Vector2(-0.5f, 0.8660254f);
                var projected = (Vector2)Stage1RuntimeReflection.InvokeStatic(
                    "JustSomeStars.Runtime.Player.SurfaceMotor2D",
                    "ProjectInputAlongGround",
                    Vector2.right,
                    slopeNormal);
                Assert.That(Vector2.Dot(projected, slopeNormal),
                    Is.EqualTo(0f).Within(0.001f));
                Assert.That(projected.x, Is.GreaterThan(0f));
                Assert.That(projected.y, Is.GreaterThan(0f));

                Stage1RuntimeReflection.Invoke(
                    fixture.Motor,
                    "SetMovingSurfaceVelocity",
                    new Vector2(1.25f, 0f));
                Stage1RuntimeReflection.Invoke(
                    fixture.Motor,
                    "AddExternalVelocity",
                    new Vector2(2f, 0f));
                Stage1RuntimeReflection.Invoke(fixture.Motor, "Simulate", 0.02f);
                Assert.That(fixture.Body.linearVelocity.x, Is.GreaterThan(3f));

                var anchor = new Vector2(-3f, 2f);
                Stage1RuntimeReflection.Invoke(
                    fixture.Motor,
                    "SetRecoveryAnchor",
                    anchor);
                fixture.Body.position = new Vector2(10f, -12f);
                fixture.Body.linearVelocity = new Vector2(4f, -9f);
                Stage1RuntimeReflection.Invoke(fixture.Motor, "Recover");
                Assert.That(fixture.Body.position, Is.EqualTo(anchor));
                Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Motor_DeclaresSemanticInputAndSettingsBinding()
        {
            var motorType = Stage1RuntimeReflection.RequireType(
                "JustSomeStars.Runtime.Player.SurfaceMotor2D");
            var method = motorType.GetMethod("BindInput");
            Assert.That(method, Is.Not.Null);
            Assert.That(
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType.FullName),
                Is.EqualTo(new[]
                {
                    "JustSomeStars.Runtime.Input.InputRouter",
                    "JustSomeStars.Runtime.Accessibility.SettingsService",
                }));
        }

        [Test]
        public void SecondaryRequestsJumpWhilePrimaryRemainsContextual()
        {
            var fixture = CreateFixture();
            var ground = CreateGround();
            try
            {
                InvokeCommand(
                    fixture.Motor,
                    SemanticGameplayCommand.Primary);
                Stage1RuntimeReflection.Invoke(fixture.Motor, "Simulate", 0.02f);
                Assert.That(fixture.Body.linearVelocity.y,
                    Is.EqualTo(0f).Within(0.001f),
                    "Primary must remain available for contextual interaction.");

                InvokeCommand(
                    fixture.Motor,
                    SemanticGameplayCommand.Secondary);
                Stage1RuntimeReflection.Invoke(fixture.Motor, "Simulate", 0.02f);
                Assert.That(fixture.Body.linearVelocity.y,
                    Is.EqualTo(7f).Within(0.05f),
                    "Secondary must start the shared jump/held-jet action.");
            }
            finally
            {
                Object.DestroyImmediate(ground);
                fixture.Dispose();
            }
        }

        private static void InvokeCommand(
            Component motor,
            SemanticGameplayCommand command)
        {
            var method = motor.GetType().GetMethod(
                "OnGameplayCommand",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(motor, new object[]
            {
                GameplayInputMode.Surface,
                command,
            });
        }

        private static MotorFixture CreateFixture()
        {
            var root = new GameObject("SurfaceMotorFixture");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.position = new Vector2(0f, 0.56f);
            var collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.6f, 1f);

            var config = Stage1RuntimeReflection.CreateConfig(
                "JustSomeStars.Runtime.Player.SurfaceMotor2DConfig");
            Stage1RuntimeReflection.Set(config, "MoveSpeed", 5f);
            Stage1RuntimeReflection.Set(config, "GroundAcceleration", 20f);
            Stage1RuntimeReflection.Set(config, "AirAcceleration", 10f);
            Stage1RuntimeReflection.Set(config, "GroundDeceleration", 24f);
            Stage1RuntimeReflection.Set(config, "JumpVelocity", 7f);
            Stage1RuntimeReflection.Set(config, "JetAcceleration", 12f);
            Stage1RuntimeReflection.Set(config, "JetDuration", 0.35f);
            Stage1RuntimeReflection.Set(config, "GroundProbeDistance", 0.1f);
            Stage1RuntimeReflection.Set(config, "MaxFallSpeed", 18f);
            var motor = Stage1RuntimeReflection.AddComponent(
                root,
                "JustSomeStars.Runtime.Player.SurfaceMotor2D");
            Stage1RuntimeReflection.Invoke(
                motor,
                "Configure",
                body,
                collider,
                config);
            return new MotorFixture(root, body, motor, config);
        }

        private static GameObject CreateGround()
        {
            var ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            var collider = ground.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(20f, 1f);
            return ground;
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
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Config);
            }
        }
    }
}
