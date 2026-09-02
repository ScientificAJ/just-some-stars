using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class AudioDirectorTests
    {
        private GameObject m_Root;
        private AudioCueLibrary m_Library;
        private AudioClip m_Foundation;
        private AudioClip m_Signal;
        private AudioClip m_Foundation2;
        private AudioClip m_Signal2;
        private AudioClip m_Dialogue;
        private AudioClip m_Effect;
        private SettingsService m_Settings;
        private string m_SettingsPath;

        [SetUp]
        public void SetUp()
        {
            m_SettingsPath = Path.Combine(
                Application.temporaryCachePath,
                $"task29-settings-{System.Guid.NewGuid():N}.json");
            m_Settings = new SettingsService(m_SettingsPath);
            m_Settings.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            m_Foundation = AudioClip.Create("foundation", 4410, 2, 44100, false);
            m_Signal = AudioClip.Create("signal", 4410, 2, 44100, false);
            m_Foundation2 = AudioClip.Create(
                "foundation-2", 4410, 2, 44100, false);
            m_Signal2 = AudioClip.Create("signal-2", 4410, 2, 44100, false);
            m_Dialogue = AudioClip.Create("dialogue", 2205, 1, 44100, false);
            m_Effect = AudioClip.Create("effect", 2205, 1, 44100, false);
            m_Library = ScriptableObject.CreateInstance<AudioCueLibrary>();
            m_Library.Configure(
                new[]
                {
                    new AudioCueDefinition("cue.music", AudioBus.Music,
                        m_Foundation, true, 1f),
                    new AudioCueDefinition("cue.signal", AudioBus.Music,
                        m_Signal, true, 0.8f),
                    new AudioCueDefinition("cue.music.2", AudioBus.Music,
                        m_Foundation2, true, 0.9f),
                    new AudioCueDefinition("cue.signal.2", AudioBus.Music,
                        m_Signal2, true, 0.7f),
                    new AudioCueDefinition("cue.dialogue", AudioBus.Dialogue,
                        m_Dialogue, false, 0.7f),
                    new AudioCueDefinition("cue.effect", AudioBus.Effects,
                        m_Effect, false, 0.6f),
                },
                new[]
                {
                    new MusicStateDefinition(
                        "music.test", "cue.music", "cue.signal", 0.5f, 0.2f),
                    new MusicStateDefinition(
                        "music.test.2", "cue.music.2", "cue.signal.2", 0.6f, 0.2f),
                });
            m_Root = new GameObject("AudioDirectorTests");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(m_SettingsPath)) File.Delete(m_SettingsPath);
            Object.DestroyImmediate(m_Root);
            Object.DestroyImmediate(m_Library);
            Object.DestroyImmediate(m_Foundation);
            Object.DestroyImmediate(m_Signal);
            Object.DestroyImmediate(m_Foundation2);
            Object.DestroyImmediate(m_Signal2);
            Object.DestroyImmediate(m_Dialogue);
            Object.DestroyImmediate(m_Effect);
        }

        [UnityTest]
        public IEnumerator MusicState_StartsSampleAlignedAndSettingsUpdateEveryBusLive()
        {
            var director = CreateDirector(out var foundation, out var signal,
                out var dialogue, out var effects);
            Assert.That(director.SetMusicState("music.test"), Is.True);
            Assert.That(foundation.clip, Is.SameAs(m_Foundation));
            Assert.That(signal.clip, Is.SameAs(m_Signal));
            Assert.That(foundation.loop, Is.True);
            Assert.That(signal.loop, Is.True);
            Assert.That(foundation.timeSamples, Is.EqualTo(signal.timeSamples));
            Assert.That(foundation.volume, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(signal.volume, Is.EqualTo(0.32f).Within(0.001f));

            var changed = m_Settings.Current;
            changed.MusicVolume = 0.25f;
            changed.DialogueVolume = 0.4f;
            changed.EffectsVolume = 0.6f;
            Assert.That(m_Settings.Apply(changed), Is.True);
            yield return null;

            Assert.That(foundation.volume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(signal.volume, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(dialogue.volume, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(effects.volume, Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void CueRouting_IsFailClosedAndKeepsDialogueAndEffectsIndependent()
        {
            var director = CreateDirector(out _, out _, out var dialogue,
                out var effects);
            Assert.That(director.PlayCue("cue.dialogue"), Is.True);
            Assert.That(dialogue.clip, Is.SameAs(m_Dialogue));
            Assert.That(effects.clip, Is.Null);

            Assert.That(director.PlayCue("cue.effect"), Is.True);
            Assert.That(effects.clip, Is.SameAs(m_Effect));
            Assert.That(dialogue.clip, Is.SameAs(m_Dialogue));
            Assert.That(director.PlayCue("cue.missing"), Is.False);
        }

        [Test]
        public void MusicState_CrossfadesAlignedPairsInsteadOfHardStoppingTheScore()
        {
            var director = CreateDirector(
                out var oldFoundation,
                out var oldSignal,
                out _,
                out _);
            Assert.That(director.SetMusicState("music.test"), Is.True);

            Assert.That(director.SetMusicState("music.test.2"), Is.True);

            Assert.That(director.IsCrossfading, Is.True);
            var newFoundation = m_Root.GetComponents<AudioSource>()
                .Single(source => source.clip == m_Foundation2);
            var newSignal = m_Root.GetComponents<AudioSource>()
                .Single(source => source.clip == m_Signal2);
            Assert.That(newFoundation.timeSamples, Is.EqualTo(newSignal.timeSamples));
            Assert.That(oldFoundation.clip, Is.SameAs(m_Foundation));
            Assert.That(oldSignal.clip, Is.SameAs(m_Signal));

            director.AdvanceCrossfade(0.1f);

            Assert.That(oldFoundation.volume, Is.GreaterThan(0f));
            Assert.That(newFoundation.volume, Is.GreaterThan(0f));
            Assert.That(director.CrossfadeProgress, Is.EqualTo(0.5f).Within(0.01f));

            director.AdvanceCrossfade(0.1f);

            Assert.That(director.IsCrossfading, Is.False);
            Assert.That(oldFoundation.clip, Is.Null);
            Assert.That(oldSignal.clip, Is.Null);
            Assert.That(newFoundation.volume, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(newSignal.volume, Is.EqualTo(0.336f).Within(0.001f));
        }

        private AudioDirector CreateDirector(
            out AudioSource foundation,
            out AudioSource signal,
            out AudioSource dialogue,
            out AudioSource effects)
        {
            foundation = m_Root.AddComponent<AudioSource>();
            signal = m_Root.AddComponent<AudioSource>();
            dialogue = m_Root.AddComponent<AudioSource>();
            effects = m_Root.AddComponent<AudioSource>();
            var director = m_Root.AddComponent<AudioDirector>();
            director.Configure(
                m_Library,
                m_Settings,
                foundation,
                signal,
                dialogue,
                effects);
            return director;
        }
    }
}
