using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Dialogue;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class CinematicDirectorTests
    {
        private readonly List<Object> m_Owned = new();
        private string m_SettingsPath;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(m_SettingsPath) && File.Exists(m_SettingsPath))
            {
                File.Delete(m_SettingsPath);
            }
            for (var index = m_Owned.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(m_Owned[index]);
            }
            m_Owned.Clear();
        }

        [Test]
        public void Timeline_UsesFallbackAndFiresCrossedBeatsOnceInCanonicalOrder()
        {
            var still = CreateSprite("fallback");
            var sequence = Own(ScriptableObject.CreateInstance<CinematicSequenceDefinition>());
            sequence.Configure(
                "cinematic.test",
                null,
                still,
                new[]
                {
                    new CinematicBeatDefinition(0f, CinematicBeatKind.Audio,
                        string.Empty, "cue.test", 0f),
                    new CinematicBeatDefinition(0.25f, CinematicBeatKind.Caption,
                        "crew.mira", "cinematic.test.caption", 2f),
                    new CinematicBeatDefinition(0.25f, CinematicBeatKind.Expression,
                        "crew.mira", "curious", 0f),
                    new CinematicBeatDefinition(0.5f,
                        CinematicBeatKind.InteractionRelease,
                        string.Empty, "continue", 0f),
                });
            var english = Own(ScriptableObject.CreateInstance<LocalizedEnglishCatalog>());
            english.Configure(new[]
            {
                new LocalizedEnglishText("actor.mira", "Mira"),
                new LocalizedEnglishText("cinematic.test.caption", "A small signal."),
            });
            var settings = CreateSettings();
            var root = Own(new GameObject("CinematicDirectorTests"));
            var fallback = root.AddComponent<SpriteRenderer>();
            var speaker = CreateText("Speaker", root.transform);
            var body = CreateText("Body", root.transform);
            var director = root.AddComponent<CinematicDirector>();
            director.ConfigureForTests(
                sequence, english, settings, fallback, speaker, body);
            var fired = new List<string>();
            director.BeatFired += beat => fired.Add($"{beat.Kind}:{beat.Value}");

            director.Begin();
            director.Advance(0.6f);

            Assert.That(director.IsUsingFallback, Is.True);
            Assert.That(fallback.sprite, Is.SameAs(still));
            Assert.That(fired, Is.EqualTo(new[]
            {
                "Audio:cue.test",
                "Expression:curious",
                "Caption:cinematic.test.caption",
                "InteractionRelease:continue",
            }));
            Assert.That(speaker.text, Is.EqualTo("Mira"));
            Assert.That(body.text, Is.EqualTo("A small signal."));
            Assert.That(director.InteractionIsReleased, Is.True);
        }

        [Test]
        public void Cancel_ClearsCaptionAndCannotEmitRemainingInteraction()
        {
            var sequence = Own(ScriptableObject.CreateInstance<CinematicSequenceDefinition>());
            sequence.Configure(
                "cinematic.cancel",
                null,
                CreateSprite("fallback"),
                new[]
                {
                    new CinematicBeatDefinition(0f, CinematicBeatKind.Caption,
                        "ori", "cinematic.cancel.caption", 2f),
                    new CinematicBeatDefinition(1f,
                        CinematicBeatKind.InteractionRelease,
                        string.Empty, "continue", 0f),
                });
            var english = Own(ScriptableObject.CreateInstance<LocalizedEnglishCatalog>());
            english.Configure(new[]
            {
                new LocalizedEnglishText("actor.ori", "Ori"),
                new LocalizedEnglishText("cinematic.cancel.caption", "Wait."),
            });
            var root = Own(new GameObject("CinematicDirectorCancelTests"));
            var director = root.AddComponent<CinematicDirector>();
            var speaker = CreateText("Speaker", root.transform);
            var body = CreateText("Body", root.transform);
            director.ConfigureForTests(
                sequence,
                english,
                CreateSettings(),
                root.AddComponent<SpriteRenderer>(),
                speaker,
                body);

            director.Begin();
            director.Cancel();
            director.Advance(2f);

            Assert.That(director.IsPlaying, Is.False);
            Assert.That(director.InteractionIsReleased, Is.False);
            Assert.That(speaker.text, Is.Empty);
            Assert.That(body.text, Is.Empty);
        }

        [Test]
        public void ActorFrameEvents_DriveCaptionAndDelayReleaseUntilReadable()
        {
            var root = Own(new GameObject("FrameEventCinematic"));
            var actor = CreateActor(root.transform, out var animator);
            var sequence = Own(ScriptableObject.CreateInstance<
                CinematicSequenceDefinition>());
            sequence.Configure(
                "cinematic.frame-events",
                null,
                CreateSprite("fallback"),
                new[]
                {
                    new CinematicBeatDefinition(
                        0f,
                        CinematicBeatKind.BodyClip,
                        "mira",
                        "cinematic.frame-events",
                        0f),
                });
            var english = Own(ScriptableObject.CreateInstance<LocalizedEnglishCatalog>());
            english.Configure(new[]
            {
                new LocalizedEnglishText("actor.mira", "Mira"),
                new LocalizedEnglishText(
                    "cinematic.test.caption",
                    "The Signal is still moving beyond the ridge."),
            });
            var speaker = CreateText("Speaker", root.transform);
            var body = CreateText("Body", root.transform);
            var director = root.AddComponent<CinematicDirector>();
            director.ConfigureForTests(
                sequence,
                english,
                CreateSettings(),
                root.AddComponent<SpriteRenderer>(),
                speaker,
                body,
                new[] { actor });

            director.Begin();
            animator.Advance(0.31f);
            director.Advance(0.31f);

            Assert.That(speaker.text, Is.EqualTo("Mira"));
            Assert.That(body.text, Is.EqualTo(
                "The Signal is still moving beyond the ridge."));
            Assert.That(director.InteractionIsReleased, Is.False,
                "A final-frame release cannot erase a caption before it is readable.");

            director.Advance(5f);

            Assert.That(director.InteractionIsReleased, Is.True);
            Assert.That(director.IsPlaying, Is.False);
        }

        [Test]
        public void VideoDecodeFailure_RestoresAuthoredFallbackAndKeepsSequenceAlive()
        {
            var root = Own(new GameObject("VideoFallback"));
            var sequence = Own(ScriptableObject.CreateInstance<
                CinematicSequenceDefinition>());
            sequence.Configure(
                "cinematic.video-failure",
                null,
                CreateSprite("fallback"),
                new[]
                {
                    new CinematicBeatDefinition(
                        0.5f,
                        CinematicBeatKind.InteractionRelease,
                        string.Empty,
                        "continue",
                        0f),
                });
            var english = Own(ScriptableObject.CreateInstance<LocalizedEnglishCatalog>());
            english.Configure(new[]
            {
                new LocalizedEnglishText("actor.mira", "Mira"),
            });
            var fallback = root.AddComponent<SpriteRenderer>();
            var director = root.AddComponent<CinematicDirector>();
            director.ConfigureForTests(
                sequence,
                english,
                CreateSettings(),
                fallback,
                CreateText("Speaker", root.transform),
                CreateText("Body", root.transform));
            var playerObject = Own(new GameObject("VideoPlayer"));
            var player = playerObject.AddComponent<VideoPlayer>();
            SetPrivate(director, "videoPlayer", player);

            director.Begin();
            fallback.enabled = false;
            InvokePrivate(director, "OnVideoError", player, "decode failed");

            Assert.That(fallback.enabled, Is.True);
            Assert.That(director.IsUsingFallback, Is.True);
            Assert.That(director.IsPlaying, Is.True);
        }

        [Test]
        public void MissionDialogueTiming_ScalesWithCopyLengthAndDialogueSpeed()
        {
            var shortFast = MirraDialoguePresenter2D.CalculateReadableDuration(
                "A small signal.", 2f, 0.2f);
            var longFast = MirraDialoguePresenter2D.CalculateReadableDuration(
                "The Signal is moving beyond the ridge, and it is getting brighter.",
                2f,
                0.2f);
            var longSlow = MirraDialoguePresenter2D.CalculateReadableDuration(
                "The Signal is moving beyond the ridge, and it is getting brighter.",
                0.5f,
                0.2f);

            Assert.That(shortFast, Is.GreaterThanOrEqualTo(0.2f));
            Assert.That(longFast, Is.GreaterThan(shortFast));
            Assert.That(longSlow, Is.GreaterThan(longFast));
        }

        private SettingsService CreateSettings()
        {
            m_SettingsPath = Path.Combine(
                Application.temporaryCachePath,
                $"task29-cinematic-{System.Guid.NewGuid():N}.json");
            var settings = new SettingsService(m_SettingsPath);
            settings.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            return settings;
        }

        private TMP_Text CreateText(string name, Transform parent)
        {
            var target = Own(new GameObject(name));
            target.transform.SetParent(parent, false);
            return target.AddComponent<TextMeshProUGUI>();
        }

        private Sprite CreateSprite(string name)
        {
            var texture = Own(new Texture2D(4, 4));
            var sprite = Own(Sprite.Create(
                texture,
                new Rect(0, 0, 4, 4),
                Vector2.one * 0.5f,
                4f));
            sprite.name = name;
            return sprite;
        }

        private CinematicActor2D CreateActor(
            Transform parent,
            out SpriteAtlasAnimator animator)
        {
            var actorRoot = Own(new GameObject("Actor-mira"));
            actorRoot.transform.SetParent(parent, false);
            var renderer = actorRoot.AddComponent<SpriteRenderer>();
            animator = actorRoot.AddComponent<SpriteAtlasAnimator>();
            animator.Configure(renderer);
            var frames = new[]
            {
                CreateSprite("performance-0"),
                CreateSprite("performance-1"),
                CreateSprite("performance-2"),
                CreateSprite("performance-3"),
            };
            var performance = Own(ScriptableObject.CreateInstance<
                SpriteAnimationClipDefinition>());
            performance.Configure(
                "mira.cinematic.frame-events.right",
                SpriteFacing.Right,
                SpriteAnimationLoopMode.Once,
                frames,
                new[] { 0.1f, 0.1f, 0.1f, 0.1f },
                new[]
                {
                    new SpriteFrameEvent(
                        0,
                        SpriteFrameEventKind.Caption,
                        "cinematic.test.caption"),
                    new SpriteFrameEvent(
                        0,
                        SpriteFrameEventKind.Expression,
                        "curious"),
                    new SpriteFrameEvent(1, SpriteFrameEventKind.Viseme, "2"),
                    new SpriteFrameEvent(
                        3,
                        SpriteFrameEventKind.InteractionRelease,
                        "continue"),
                },
                authoredPlaybackMotionId: "mira.interact.right");
            var set = Own(ScriptableObject.CreateInstance<CharacterSpriteSet>());
            set.Configure("mira", new[] { performance });
            var actor = actorRoot.AddComponent<CinematicActor2D>();
            actor.Configure("mira", null, animator, set, set);
            return actor;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static void InvokePrivate(
            object target,
            string method,
            params object[] arguments)
        {
            var targetMethod = target.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(targetMethod, Is.Not.Null, method);
            targetMethod.Invoke(target, arguments);
        }

        private T Own<T>(T value) where T : Object
        {
            m_Owned.Add(value);
            return value;
        }
    }
}
