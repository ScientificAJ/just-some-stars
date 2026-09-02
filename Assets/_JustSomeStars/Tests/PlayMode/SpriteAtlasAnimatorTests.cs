using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class SpriteAtlasAnimatorTests
    {
        private readonly List<Texture2D> textures = new();
        private readonly List<Sprite> sprites = new();
        private readonly List<ScriptableObject> definitions = new();
        private readonly List<GameObject> objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in objects)
            {
                Object.DestroyImmediate(target);
            }
            foreach (var definition in definitions)
            {
                Object.DestroyImmediate(definition);
            }
            foreach (var sprite in sprites)
            {
                Object.DestroyImmediate(sprite);
            }
            foreach (var texture in textures)
            {
                Object.DestroyImmediate(texture);
            }
            objects.Clear();
            definitions.Clear();
            sprites.Clear();
            textures.Clear();
        }

        [UnityTest]
        public IEnumerator OnceClip_EmitsEachCrossedFrameEventExactlyOnce()
        {
            var clip = CreateClip(
                SpriteAnimationLoopMode.Once,
                new SpriteFrameEvent(0, SpriteFrameEventKind.Vfx, "ready"),
                new SpriteFrameEvent(1, SpriteFrameEventKind.FootContact, "left"),
                new SpriteFrameEvent(3, SpriteFrameEventKind.FootContact, "right"));
            var animator = CreateAnimator(out var renderer);
            var observed = new List<string>();
            animator.FrameEventEmitted += frameEvent => observed.Add(frameEvent.Id);

            animator.Play(clip);
            animator.Advance(0.41f);
            yield return null;

            Assert.That(observed, Is.EqualTo(new[] { "ready", "left", "right" }));
            Assert.That(animator.CurrentFrameIndex, Is.EqualTo(3));
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(renderer.sprite, Is.SameAs(clip.Frames[3]));
        }

        [UnityTest]
        public IEnumerator LoopClip_LargeDeltaPreservesFrameOrderAndOccurrenceCounts()
        {
            var clip = CreateClip(
                SpriteAnimationLoopMode.Loop,
                new SpriteFrameEvent(0, SpriteFrameEventKind.Audio, "zero"),
                new SpriteFrameEvent(2, SpriteFrameEventKind.Vfx, "two"));
            var animator = CreateAnimator(out _);
            var observed = new List<string>();
            animator.FrameEventEmitted += frameEvent => observed.Add(frameEvent.Id);

            animator.Play(clip);
            animator.Advance(0.85f);
            yield return null;

            Assert.That(observed, Is.EqualTo(new[]
            {
                "zero", "two", "zero", "two", "zero",
            }));
            Assert.That(animator.CurrentFrameIndex, Is.EqualTo(0));
            Assert.That(animator.IsPlaying, Is.True);
        }

        [Test]
        public void Play_RejectsInvalidClipBeforeChangingRenderer()
        {
            var clip = ScriptableObject.CreateInstance<SpriteAnimationClipDefinition>();
            definitions.Add(clip);
            var animator = CreateAnimator(out var renderer);

            Assert.Throws<System.InvalidOperationException>(() => animator.Play(clip));
            Assert.That(renderer.sprite, Is.Null);
            Assert.That(animator.IsPlaying, Is.False);
        }

        [Test]
        public void CharacterSpriteSet_RejectsDuplicateIdsAndFindsCanonicalClip()
        {
            var clip = CreateClip(SpriteAnimationLoopMode.Loop);
            var spriteSet = ScriptableObject.CreateInstance<CharacterSpriteSet>();
            definitions.Add(spriteSet);
            spriteSet.Configure("test-character", new[] { clip });
            Assert.That(spriteSet.FindClip(clip.StableId), Is.SameAs(clip));

            Assert.Throws<System.InvalidOperationException>(() =>
                spriteSet.Configure("test-character", new[] { clip, clip }));
        }

        [Test]
        public void Clip_RejectsNonCanonicalFrameEventOrder()
        {
            var clip = CreateClip(SpriteAnimationLoopMode.Once);
            Assert.Throws<System.InvalidOperationException>(() => clip.Configure(
                "test.event-order.right",
                SpriteFacing.Right,
                SpriteAnimationLoopMode.Once,
                clip.Frames.ToArray(),
                clip.FrameDurations.ToArray(),
                new[]
                {
                    new SpriteFrameEvent(2, SpriteFrameEventKind.Audio, "later"),
                    new SpriteFrameEvent(1, SpriteFrameEventKind.Expression, "earlier"),
                }));
        }

        [Test]
        public void ReplacingClip_DropsUnreachedEventsFromThePreviousClip()
        {
            var oldClip = CreateClip(
                SpriteAnimationLoopMode.Once,
                new SpriteFrameEvent(3, SpriteFrameEventKind.Audio, "stale"));
            var replacement = CreateClip(
                SpriteAnimationLoopMode.Once,
                new SpriteFrameEvent(0, SpriteFrameEventKind.Audio, "fresh"));
            var animator = CreateAnimator(out _);
            var observed = new List<string>();
            animator.FrameEventEmitted += frameEvent => observed.Add(frameEvent.Id);

            animator.Play(oldClip);
            animator.Play(replacement);
            animator.Advance(0.5f);

            Assert.That(observed, Is.EqualTo(new[] { "fresh" }));
        }

        [UnityTest]
        public IEnumerator InteractionParticipant_ReleasesOnAuthoredCommitBeforeClipTail()
        {
            var clip = CreateClip(
                SpriteAnimationLoopMode.Once,
                new SpriteFrameEvent(
                    2,
                    SpriteFrameEventKind.Interaction,
                    "interact-commit"));
            var idle = CreateClip(SpriteAnimationLoopMode.Loop);
            idle.Configure(
                "test.idle.right",
                SpriteFacing.Right,
                SpriteAnimationLoopMode.Loop,
                idle.Frames.ToArray(),
                idle.FrameDurations.ToArray(),
                System.Array.Empty<SpriteFrameEvent>());
            var set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
            definitions.Add(set);
            set.Configure("mira", new[] { clip, idle });
            var animator = CreateAnimator(out _);
            var participant = animator.gameObject.AddComponent<
                MirraInteractionParticipant2D>();
            SetPrivate(participant, "actorId", "crew.mira");
            SetPrivate(participant, "actorKind", InteractionActorKind.Crew);
            SetPrivate(participant, "facing", InteractionFacing.Right);
            SetPrivate(participant, "depthBand", InteractionDepthBand.Gameplay);
            SetPrivate(participant, "atlasAnimator", animator);
            SetPrivate(participant, "spriteSet", set);
            SetPrivate(participant, "idleClipId", idle.StableId);

            var operation = participant.PlayAsync(clip, CancellationToken.None).AsTask();
            animator.Advance(0.21f);
            for (var frame = 0; frame < 10 && !operation.IsCompleted; frame++)
                yield return null;

            Assert.That(operation.IsCompletedSuccessfully, Is.True);
            Assert.That(animator.CurrentClip, Is.SameAs(idle));
        }

        private SpriteAnimationClipDefinition CreateClip(
            SpriteAnimationLoopMode loopMode,
            params SpriteFrameEvent[] events)
        {
            var frames = new Sprite[4];
            for (var index = 0; index < frames.Length; index++)
            {
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                textures.Add(texture);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, 4, 4),
                    new Vector2(0.5f, 0.125f),
                    4f);
                sprite.name = $"frame-{index}";
                sprites.Add(sprite);
                frames[index] = sprite;
            }

            var clip = ScriptableObject.CreateInstance<SpriteAnimationClipDefinition>();
            definitions.Add(clip);
            clip.Configure(
                "test.run.right",
                SpriteFacing.Right,
                loopMode,
                frames,
                new[] { 0.1f, 0.1f, 0.1f, 0.1f },
                events);
            return clip;
        }

        private SpriteAtlasAnimator CreateAnimator(out SpriteRenderer renderer)
        {
            var target = new GameObject("SpriteAtlasAnimatorTests");
            objects.Add(target);
            renderer = target.AddComponent<SpriteRenderer>();
            var animator = target.AddComponent<SpriteAtlasAnimator>();
            animator.Configure(renderer);
            return animator;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var targetField = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(targetField, Is.Not.Null, field);
            targetField.SetValue(target, value);
        }
    }
}
