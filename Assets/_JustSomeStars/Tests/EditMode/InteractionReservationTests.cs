using System;
using System.Collections.Generic;
using System.Threading;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class InteractionReservationTests
    {
        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = m_OwnedObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(m_OwnedObjects[index]);
            }

            m_OwnedObjects.Clear();
        }

        [Test]
        public void ExclusiveAnchor_RejectsSecondActorUntilOwningLeaseReleases()
        {
            var now = 10d;
            var service = new InteractionReservationService(() => now);
            var anchorId = new ContentId("anchor.probe.player");

            Assert.That(
                service.TryReserve(
                    anchorId,
                    new ContentId("actor.captain"),
                    exclusive: true,
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None,
                    out var captainLease),
                Is.True);
            Assert.That(
                service.TryReserve(
                    anchorId,
                    new ContentId("actor.juno"),
                    exclusive: true,
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None,
                    out _),
                Is.False);
            Assert.That(service.ActiveLeaseCount, Is.EqualTo(1));

            captainLease.Dispose();

            Assert.That(
                service.TryReserve(
                    anchorId,
                    new ContentId("actor.juno"),
                    exclusive: true,
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None,
                    out var junoLease),
                Is.True);
            Assert.That(junoLease.IsActive, Is.True);
            junoLease.Dispose();
            Assert.That(service.ActiveLeaseCount, Is.Zero);
        }

        [Test]
        public void Cancellation_ReleasesLeaseExactlyOnce()
        {
            var service = new InteractionReservationService(() => 4d);
            using var cancellation = new CancellationTokenSource();
            Assert.That(
                service.TryReserve(
                    new ContentId("anchor.probe.ori"),
                    new ContentId("actor.ori"),
                    exclusive: true,
                    TimeSpan.FromSeconds(3),
                    cancellation.Token,
                    out var lease),
                Is.True);

            cancellation.Cancel();

            Assert.That(lease.IsActive, Is.False);
            Assert.That(service.ActiveLeaseCount, Is.Zero);
            Assert.DoesNotThrow(lease.Dispose);
            Assert.DoesNotThrow(lease.Dispose);
            Assert.That(service.ActiveLeaseCount, Is.Zero);
        }

        [Test]
        public void ExpiredLease_IsRecoveredBeforeNextReservation()
        {
            var now = 20d;
            var service = new InteractionReservationService(() => now);
            var anchorId = new ContentId("anchor.probe.crew");
            Assert.That(
                service.TryReserve(
                    anchorId,
                    new ContentId("actor.juno"),
                    exclusive: true,
                    TimeSpan.FromSeconds(0.5d),
                    CancellationToken.None,
                    out var staleLease),
                Is.True);

            now = 20.51d;

            Assert.That(staleLease.IsActive, Is.False);
            Assert.That(
                service.TryReserve(
                    anchorId,
                    new ContentId("actor.captain"),
                    exclusive: true,
                    TimeSpan.FromSeconds(1d),
                    CancellationToken.None,
                    out var recoveredLease),
                Is.True);
            Assert.That(service.ActiveLeaseCount, Is.EqualTo(1));
            recoveredLease.Dispose();
        }

        [Test]
        public void ContextualSelection_FiltersModeToolDepthLayerFacingAndDistance()
        {
            var definition = CreateDefinition(
                requiredTool: "tool.probe-wrench",
                maxDistance: 4f);
            var actor = new SelectionParticipant(
                new ContentId("actor.juno"),
                InteractionActorKind.Crew,
                Vector2.zero,
                InteractionFacing.Right,
                InteractionDepthBand.Gameplay,
                1 << 9,
                GameMode.Surface,
                new[] { new ContentId("tool.probe-wrench") });
            var validFar = CreateAnchor(
                "anchor.valid.far",
                InteractionActorKind.Crew,
                new Vector2(3f, 0f),
                InteractionFacing.Right,
                InteractionDepthBand.Gameplay,
                9);
            var validNear = CreateAnchor(
                "anchor.valid.near",
                InteractionActorKind.Crew,
                new Vector2(2f, 0f),
                InteractionFacing.Right,
                InteractionDepthBand.Gameplay,
                9);
            var wrongDepth = CreateAnchor(
                "anchor.wrong-depth",
                InteractionActorKind.Crew,
                new Vector2(1f, 0f),
                InteractionFacing.Right,
                InteractionDepthBand.Midground,
                9);
            var wrongLayer = CreateAnchor(
                "anchor.wrong-layer",
                InteractionActorKind.Crew,
                new Vector2(1f, 0f),
                InteractionFacing.Right,
                InteractionDepthBand.Gameplay,
                8);
            var wrongFacing = CreateAnchor(
                "anchor.wrong-facing",
                InteractionActorKind.Crew,
                new Vector2(1f, 0f),
                InteractionFacing.Left,
                InteractionDepthBand.Gameplay,
                9);
            var behindActor = CreateAnchor(
                "anchor.behind-actor",
                InteractionActorKind.Crew,
                new Vector2(-1f, 0f),
                InteractionFacing.Right,
                InteractionDepthBand.Gameplay,
                9);
            var tooFar = CreateAnchor(
                "anchor.too-far",
                InteractionActorKind.Crew,
                new Vector2(5f, 0f),
                InteractionFacing.Right,
                InteractionDepthBand.Gameplay,
                9);

            var candidates = InteractionRunner.SelectEligibleAnchors(
                definition,
                actor,
                new[]
                {
                    wrongDepth,
                    validFar,
                    tooFar,
                    wrongLayer,
                    validNear,
                    wrongFacing,
                    behindActor,
                });

            Assert.That(candidates, Is.EqualTo(new[] { validNear, validFar }));

            var missingTool = actor.WithTools(Array.Empty<ContentId>());
            Assert.That(
                InteractionRunner.SelectEligibleAnchors(
                    definition,
                    missingTool,
                    new[] { validNear }),
                Is.Empty);

            var wrongMode = actor.WithMode(GameMode.Flight);
            Assert.That(
                InteractionRunner.SelectEligibleAnchors(
                    definition,
                    wrongMode,
                    new[] { validNear }),
                Is.Empty);
        }

        [Test]
        public void Definition_RejectsDuplicateActorClipIdentities()
        {
            var definition = ScriptableObject.CreateInstance<InteractionDefinition>();
            m_OwnedObjects.Add(definition);
            var duplicatedClip = CreateClip("clip.probe.duplicated");

            Assert.Throws<InvalidOperationException>(() => definition.Configure(
                "interaction.probe-repair",
                "tool.probe-wrench",
                duplicatedClip,
                duplicatedClip,
                CreateClip("clip.probe.ori"),
                new[] { GameMode.Surface },
                Array.Empty<InteractionEventBinding>(),
                maxDistance: 4f,
                reservationTimeoutSeconds: 2f));
        }

        private InteractionDefinition CreateDefinition(
            string requiredTool,
            float maxDistance)
        {
            var definition = ScriptableObject.CreateInstance<InteractionDefinition>();
            m_OwnedObjects.Add(definition);
            var player = CreateClip("clip.probe.captain");
            var crew = CreateClip("clip.probe.juno");
            var ori = CreateClip("clip.probe.ori");
            definition.Configure(
                "interaction.probe-repair",
                requiredTool,
                player,
                crew,
                ori,
                new[] { GameMode.Surface },
                new[]
                {
                    new InteractionEventBinding(
                        InteractionEventKind.InstrumentUsed,
                        "tool.probe-wrench"),
                },
                maxDistance,
                reservationTimeoutSeconds: 2f);
            return definition;
        }

        private SpriteAnimationClipDefinition CreateClip(string id)
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            var clip = ScriptableObject.CreateInstance<SpriteAnimationClipDefinition>();
            m_OwnedObjects.Add(clip);
            m_OwnedObjects.Add(sprite);
            m_OwnedObjects.Add(texture);
            clip.Configure(
                id,
                SpriteFacing.Right,
                SpriteAnimationLoopMode.Once,
                new[] { sprite },
                new[] { 0.1f },
                Array.Empty<SpriteFrameEvent>());
            return clip;
        }

        private InteractionAnchor2D CreateAnchor(
            string id,
            InteractionActorKind actorKind,
            Vector2 position,
            InteractionFacing facing,
            InteractionDepthBand depthBand,
            int layer)
        {
            var gameObject = new GameObject(id);
            m_OwnedObjects.Add(gameObject);
            gameObject.layer = layer;
            gameObject.transform.position = position;
            var anchor = gameObject.AddComponent<InteractionAnchor2D>();
            anchor.Configure(
                id,
                actorKind,
                facing,
                depthBand,
                exclusive: true,
                requireApproachFacing: true,
                recoveryOffset: new Vector2(0f, 0.25f));
            return anchor;
        }

        private sealed class SelectionParticipant : IInteractionParticipant2D
        {
            private readonly IReadOnlyCollection<ContentId> m_Tools;

            public SelectionParticipant(
                ContentId actorId,
                InteractionActorKind actorKind,
                Vector2 position,
                InteractionFacing facing,
                InteractionDepthBand depthBand,
                int allowedPhysicsLayers,
                GameMode mode,
                IReadOnlyCollection<ContentId> tools)
            {
                ActorId = actorId;
                ActorKind = actorKind;
                Position = position;
                Facing = facing;
                DepthBand = depthBand;
                AllowedPhysicsLayers = allowedPhysicsLayers;
                Mode = mode;
                m_Tools = tools;
            }

            public ContentId ActorId { get; }
            public InteractionActorKind ActorKind { get; }
            public Vector2 Position { get; private set; }
            public InteractionFacing Facing { get; }
            public InteractionDepthBand DepthBand { get; }
            public int AllowedPhysicsLayers { get; }
            public GameMode Mode { get; }
            public IReadOnlyCollection<ContentId> Tools => m_Tools;

            public SelectionParticipant WithTools(
                IReadOnlyCollection<ContentId> tools)
            {
                return new SelectionParticipant(
                    ActorId,
                    ActorKind,
                    Position,
                    Facing,
                    DepthBand,
                    AllowedPhysicsLayers,
                    Mode,
                    tools);
            }

            public SelectionParticipant WithMode(GameMode mode)
            {
                return new SelectionParticipant(
                    ActorId,
                    ActorKind,
                    Position,
                    Facing,
                    DepthBand,
                    AllowedPhysicsLayers,
                    mode,
                    m_Tools);
            }

            public System.Threading.Tasks.ValueTask MoveToAsync(
                Vector2 destination,
                CancellationToken cancellationToken)
            {
                Position = destination;
                return default;
            }

            public System.Threading.Tasks.ValueTask PlayAsync(
                SpriteAnimationClipDefinition clip,
                CancellationToken cancellationToken)
            {
                return default;
            }

            public void Recover(Vector2 recoveryPosition)
            {
                Position = recoveryPosition;
            }
        }
    }
}
