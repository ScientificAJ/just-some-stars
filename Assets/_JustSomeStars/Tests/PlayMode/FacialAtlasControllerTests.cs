using System.Collections.Generic;
using JustSomeStars.Runtime.Animation2D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class FacialAtlasControllerTests
    {
        private readonly List<Object> m_Owned = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = m_Owned.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(m_Owned[index]);
            }
            m_Owned.Clear();
        }

        [Test]
        public void ExpressionsAndVisemesResolveDeterministicallyThenResetNeutral()
        {
            var set = CreateFaceSet("mira");
            var root = Own(new GameObject("Face"));
            var image = root.AddComponent<Image>();
            var speechRoot = Own(new GameObject("Speech"));
            speechRoot.transform.SetParent(root.transform, false);
            var speech = speechRoot.AddComponent<Image>();
            var controller = root.AddComponent<FacialAtlasController2D>();
            controller.Configure(new[] { set }, image, speech);

            Assert.That(controller.ShowExpression("crew.mira", "concerned"), Is.True);
            Assert.That(controller.CurrentExpression, Is.EqualTo("worried"));
            Assert.That(image.sprite.name, Is.EqualTo("mira-worried"));
            Assert.That(controller.ShowViseme("mira", 4), Is.True);
            Assert.That(image.sprite.name, Is.EqualTo("mira-worried"),
                "A mouth overlay must not replace the complete face portrait.");
            Assert.That(image.enabled, Is.True);
            Assert.That(speech.enabled, Is.True);
            Assert.That(speech.sprite.name, Is.EqualTo("mira-speech-4"));
            controller.ResetNeutral();
            Assert.That(controller.CurrentExpression, Is.EqualTo("neutral"));
            Assert.That(image.sprite.name, Is.EqualTo("mira-neutral"));
            Assert.That(speech.enabled, Is.False);
        }

        [Test]
        public void UnknownActorExpressionAndVisemeFailClosedWithoutChangingPortrait()
        {
            var set = CreateFaceSet("ori");
            var root = Own(new GameObject("Face"));
            var image = root.AddComponent<Image>();
            var speechRoot = Own(new GameObject("Speech"));
            speechRoot.transform.SetParent(root.transform, false);
            var speech = speechRoot.AddComponent<Image>();
            var controller = root.AddComponent<FacialAtlasController2D>();
            controller.Configure(new[] { set }, image, speech);
            controller.ShowExpression("ori", "happy");
            var previous = image.sprite;

            Assert.That(controller.ShowExpression("captain", "happy"), Is.False);
            Assert.That(controller.ShowExpression("ori", "unmapped"), Is.False);
            Assert.That(controller.ShowViseme("ori", 99), Is.False);
            Assert.That(image.sprite, Is.SameAs(previous));
            Assert.That(speech.enabled, Is.False);
        }

        private CharacterSpriteSet CreateFaceSet(string actorId)
        {
            var clips = new List<SpriteAnimationClipDefinition>();
            foreach (var expression in FacialAtlasController2D.RequiredExpressions)
            {
                clips.Add(CreateClip(
                    $"{actorId}.expression.{expression}",
                    $"{actorId}-{expression}"));
            }
            for (var viseme = 0; viseme < FacialAtlasController2D.VisemeCount; viseme++)
            {
                clips.Add(CreateClip(
                    $"{actorId}.speech.{viseme}",
                    $"{actorId}-speech-{viseme}"));
            }
            var set = Own(ScriptableObject.CreateInstance<CharacterSpriteSet>());
            set.Configure(actorId, clips.ToArray());
            return set;
        }

        private SpriteAnimationClipDefinition CreateClip(string id, string spriteName)
        {
            var texture = Own(new Texture2D(2, 2));
            var sprite = Own(Sprite.Create(
                texture,
                new Rect(0, 0, 2, 2),
                Vector2.one * 0.5f,
                2f));
            sprite.name = spriteName;
            var clip = Own(ScriptableObject.CreateInstance<SpriteAnimationClipDefinition>());
            clip.Configure(
                id,
                SpriteFacing.Neutral,
                SpriteAnimationLoopMode.HoldLast,
                new[] { sprite },
                new[] { 1f / 12f },
                System.Array.Empty<SpriteFrameEvent>());
            return clip;
        }

        private T Own<T>(T value) where T : Object
        {
            m_Owned.Add(value);
            return value;
        }
    }
}
