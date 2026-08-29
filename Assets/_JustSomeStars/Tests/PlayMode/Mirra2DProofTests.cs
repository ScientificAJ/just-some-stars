using System;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Rendering2D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class Mirra2DProofTests
    {
        [Test]
        public void Recovery_RepositionsTheRealMotorAndResetsMotion()
        {
            var root = new GameObject("MirraRecoveryTest");
            ScriptableObject config = null;
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                var collider = root.AddComponent<CapsuleCollider2D>();
                config = Stage1RuntimeReflection.CreateConfig(
                    "JustSomeStars.Runtime.Player.SurfaceMotor2DConfig");
                ConfigureMotor(config);
                var motor = Stage1RuntimeReflection.AddComponent(
                    root,
                    "JustSomeStars.Runtime.Player.SurfaceMotor2D");
                Stage1RuntimeReflection.Invoke(motor, "Configure", body, collider, config);
                var recovery = Stage1RuntimeReflection.AddComponent(
                    root,
                    "JustSomeStars.Runtime.Player.SurfaceRecovery2D");
                Stage1RuntimeReflection.Invoke(
                    recovery,
                    "Configure",
                    motor,
                    body,
                    new Vector2(-1.5f, 2.25f),
                    -5f);

                body.position = new Vector2(4f, -5.1f);
                body.linearVelocity = new Vector2(9f, -12f);
                Stage1RuntimeReflection.Invoke(recovery, "EvaluateNow");

                Assert.That(body.position, Is.EqualTo(new Vector2(-1.5f, 2.25f)));
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(
                    Stage1RuntimeReflection.Read<int>(recovery, "RecoveryCount"),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (config != null)
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            }
        }

        [Test]
        public void LensTarget_IsPassiveAndOnlyControllerFocusMutatesIt()
        {
            var target = new GameObject("MirraLensTargetTest");
            var phenomenon = ScriptableObject.CreateInstance<PhenomenonDefinition>();
            try
            {
                phenomenon.Configure(
                    "phenomenon.mirra.temperature-gradient",
                    "science-source.mirra.tidal-climate",
                    LayerBand.Midground,
                    LensFocusBehavior.Region,
                    new[] { LensMode.Temperature },
                    "hypothesis.mirra.day-night-circulation",
                    "discovery.hint.mirra",
                    "discovery.detail.mirra",
                    0.75f);
                var lens = target.AddComponent<DiscoveryLensTarget2D>();
                lens.Configure(phenomenon);

                Assert.That(lens.IsConfigured, Is.True);
                Assert.That(lens.TargetId, Is.EqualTo(
                    "phenomenon.mirra.temperature-gradient"));
                Assert.That(lens.IsFocused, Is.False);
                lens.SetFocused(true);
                Assert.That(lens.IsFocused, Is.True);
                lens.SetFocused(false);
                Assert.That(lens.IsFocused, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(phenomenon);
            }
        }

        [Test]
        public async Task Presenter_PlaysTheRealMiraAndOriIdleAtlases()
        {
            foreach (var entry in new[]
            {
                (Address: "Characters/Crew/mira", Clip: "mira.idle.right"),
                (Address: "Characters/Crew/ori", Clip: "ori.idle.right"),
            })
            {
                var handle = Addressables.LoadAssetAsync<CharacterSpriteSet>(entry.Address);
                GameObject actor = null;
                try
                {
                    await handle.Task;
                    Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                    actor = new GameObject(entry.Address);
                    var renderer = actor.AddComponent<SpriteRenderer>();
                    var animator = actor.AddComponent<SpriteAtlasAnimator>();
                    animator.Configure(renderer);
                    var presenter = Stage1RuntimeReflection.AddComponent(
                        actor,
                        "JustSomeStars.Runtime.Animation2D.MirraProofActorPresenter");
                    Stage1RuntimeReflection.Invoke(
                        presenter,
                        "Configure",
                        animator,
                        handle.Result,
                        entry.Clip);

                    Assert.That(animator.CurrentClip, Is.SameAs(handle.Result.FindClip(entry.Clip)));
                    Assert.That(animator.IsPlaying, Is.True);
                    Assert.That(renderer.sprite, Is.SameAs(animator.CurrentClip.Frames[0]));
                }
                finally
                {
                    if (actor != null)
                    {
                        UnityEngine.Object.DestroyImmediate(actor);
                    }
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
            }
        }

        private static void ConfigureMotor(ScriptableObject config)
        {
            Stage1RuntimeReflection.Set(config, "MoveSpeed", 5f);
            Stage1RuntimeReflection.Set(config, "GroundAcceleration", 20f);
            Stage1RuntimeReflection.Set(config, "AirAcceleration", 10f);
            Stage1RuntimeReflection.Set(config, "GroundDeceleration", 24f);
            Stage1RuntimeReflection.Set(config, "JumpVelocity", 7f);
            Stage1RuntimeReflection.Set(config, "JetAcceleration", 12f);
            Stage1RuntimeReflection.Set(config, "JetDuration", 0.35f);
            Stage1RuntimeReflection.Set(config, "GroundProbeDistance", 0.1f);
            Stage1RuntimeReflection.Set(config, "MaxFallSpeed", 18f);
        }
    }
}
