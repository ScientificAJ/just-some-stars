using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.UI;
using UnityEngine;

namespace JustSomeStars.Runtime.Core
{
    public enum AudioBus
    {
        Music = 0,
        Dialogue = 1,
        Effects = 2,
    }

    [Serializable]
    public sealed class AudioCueDefinition
    {
        [SerializeField] private string stableId;
        [SerializeField] private AudioBus bus;
        [SerializeField] private AudioClip clip;
        [SerializeField] private bool loop;
        [SerializeField, Range(0f, 1f)] private float gain = 1f;

        public AudioCueDefinition(
            string id,
            AudioBus targetBus,
            AudioClip authoredClip,
            bool shouldLoop,
            float authoredGain)
        {
            stableId = id;
            bus = targetBus;
            clip = authoredClip;
            loop = shouldLoop;
            gain = authoredGain;
        }

        public string StableId => stableId;
        public AudioBus Bus => bus;
        public AudioClip Clip => clip;
        public bool Loop => loop;
        public float Gain => gain;

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                !string.Equals(stableId, stableId.Trim(), StringComparison.Ordinal) ||
                clip == null ||
                !Enum.IsDefined(typeof(AudioBus), bus) ||
                gain < 0f || gain > 1f || float.IsNaN(gain))
            {
                throw new InvalidOperationException(
                    "Audio cues require canonical ids, clips, buses and bounded gain.");
            }

            if (loop && bus != AudioBus.Music)
            {
                throw new InvalidOperationException(
                    $"Only music cue '{stableId}' may loop.");
            }
        }
    }

    [Serializable]
    public sealed class MusicStateDefinition
    {
        [SerializeField] private string stableId;
        [SerializeField] private string foundationCueId;
        [SerializeField] private string signalStemCueId;
        [SerializeField, Range(0f, 1f)] private float signalLevel;
        [SerializeField, Min(0f)] private float crossfadeSeconds;

        public MusicStateDefinition(
            string id,
            string foundationCue,
            string signalStem,
            float authoredSignalLevel,
            float authoredCrossfadeSeconds)
        {
            stableId = id;
            foundationCueId = foundationCue;
            signalStemCueId = signalStem;
            signalLevel = authoredSignalLevel;
            crossfadeSeconds = authoredCrossfadeSeconds;
        }

        public string StableId => stableId;
        public string FoundationCueId => foundationCueId;
        public string SignalStemCueId => signalStemCueId;
        public float SignalLevel => signalLevel;
        public float CrossfadeSeconds => crossfadeSeconds;

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                string.IsNullOrWhiteSpace(foundationCueId) ||
                string.IsNullOrWhiteSpace(signalStemCueId) ||
                !string.Equals(stableId, stableId.Trim(), StringComparison.Ordinal) ||
                !string.Equals(
                    foundationCueId,
                    foundationCueId.Trim(),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    signalStemCueId,
                    signalStemCueId.Trim(),
                    StringComparison.Ordinal) ||
                signalLevel < 0f || signalLevel > 1f ||
                float.IsNaN(signalLevel) ||
                crossfadeSeconds < 0f || float.IsNaN(crossfadeSeconds))
            {
                throw new InvalidOperationException(
                    "Music states require canonical cues and bounded mix values.");
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour, ISoundtrackPlayer
    {
        private const string ResourceName = "Task29AudioCueLibrary";

        [SerializeField] private AudioCueLibrary library;
        [SerializeField] private AudioSource musicFoundation;
        [SerializeField] private AudioSource musicSignal;
        [SerializeField] private AudioSource incomingMusicFoundation;
        [SerializeField] private AudioSource incomingMusicSignal;
        [SerializeField] private AudioSource dialogue;
        [SerializeField] private AudioSource effects;

        private SettingsService m_Settings;
        private MusicStateDefinition m_CurrentMusicState;
        private float m_FoundationGain = 1f;
        private float m_SignalGain = 1f;
        private float m_DialogueGain = 1f;
        private float m_EffectsGain = 1f;
        private float m_PreviousFoundationGain;
        private float m_PreviousSignalGain;
        private float m_CrossfadeElapsed;
        private float m_CrossfadeDuration;

        public static AudioDirector Instance { get; private set; }
        public static event Action<AudioDirector> Installed;
        public string CurrentMusicStateId =>
            m_CurrentMusicState?.StableId ?? string.Empty;
        public AudioCueLibrary Library => library;
        public bool IsCrossfading => m_CrossfadeDuration > 0f;
        public float CrossfadeProgress => !IsCrossfading
            ? 1f
            : Mathf.Clamp01(m_CrossfadeElapsed / m_CrossfadeDuration);

        public static AudioDirector EnsureInstalled(SettingsService settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (Instance != null) return Instance;
            var authoredLibrary = Resources.Load<AudioCueLibrary>(ResourceName);
            if (authoredLibrary == null)
            {
                Debug.LogWarning(
                    "Task 29 audio library is unavailable; story progression remains enabled.");
                return null;
            }

            var root = new GameObject("[JSS] Audio Director");
            DontDestroyOnLoad(root);
            var director = root.AddComponent<AudioDirector>();
            director.Configure(
                authoredLibrary,
                settings,
                CreateSource(root, "Music Foundation"),
                CreateSource(root, "Signal Stem"),
                CreateSource(root, "Dialogue"),
                CreateSource(root, "Effects"));
            return director;
        }

        public void Configure(
            AudioCueLibrary authoredLibrary,
            SettingsService settings,
            AudioSource foundationSource,
            AudioSource signalSource,
            AudioSource dialogueSource,
            AudioSource effectsSource)
        {
            authoredLibrary = authoredLibrary != null
                ? authoredLibrary
                : throw new ArgumentNullException(nameof(authoredLibrary));
            authoredLibrary.ValidateOrThrow();
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (foundationSource == null || signalSource == null ||
                dialogueSource == null || effectsSource == null)
            {
                throw new ArgumentNullException(
                    nameof(foundationSource),
                    "AudioDirector requires all four isolated output sources.");
            }

            ReleaseSettings();
            library = authoredLibrary;
            m_Settings = settings;
            musicFoundation = foundationSource;
            musicSignal = signalSource;
            incomingMusicFoundation ??= CreateSource(
                gameObject,
                "Incoming Music Foundation");
            incomingMusicSignal ??= CreateSource(
                gameObject,
                "Incoming Signal Stem");
            dialogue = dialogueSource;
            effects = effectsSource;
            ConfigureSource(musicFoundation);
            ConfigureSource(musicSignal);
            ConfigureSource(incomingMusicFoundation);
            ConfigureSource(incomingMusicSignal);
            ConfigureSource(dialogue);
            ConfigureSource(effects);
            m_Settings.SettingsChanged += OnSettingsChanged;
            Instance = this;
            ApplyVolumes(m_Settings.Current);
            Installed?.Invoke(this);
        }

        public bool SetMusicState(string stateId)
        {
            if (library == null || string.IsNullOrWhiteSpace(stateId)) return false;
            MusicStateDefinition state;
            try
            {
                state = library.FindMusicState(stateId);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }

            if (m_CurrentMusicState != null &&
                string.Equals(
                    m_CurrentMusicState.StableId,
                    state.StableId,
                    StringComparison.Ordinal) &&
                musicFoundation.isPlaying && musicSignal.isPlaying)
            {
                ApplyVolumes(m_Settings.Current);
                return true;
            }

            var foundation = library.FindCue(state.FoundationCueId);
            var signal = library.FindCue(state.SignalStemCueId);
            if (m_CurrentMusicState == null || !musicFoundation.isPlaying ||
                state.CrossfadeSeconds <= 0f)
            {
                StartStateImmediately(state, foundation, signal);
                return true;
            }

            CancelCrossfade(discardIncoming: true);
            m_PreviousFoundationGain = m_FoundationGain;
            m_PreviousSignalGain = m_SignalGain;
            m_FoundationGain = foundation.Gain;
            m_SignalGain = signal.Gain * state.SignalLevel;
            m_CurrentMusicState = state;
            m_CrossfadeElapsed = 0f;
            m_CrossfadeDuration = state.CrossfadeSeconds;
            PrepareLoop(incomingMusicFoundation, foundation.Clip);
            PrepareLoop(incomingMusicSignal, signal.Clip);
            incomingMusicFoundation.timeSamples = 0;
            incomingMusicSignal.timeSamples = 0;
            incomingMusicFoundation.Play();
            incomingMusicSignal.Play();
            ApplyVolumes(m_Settings.Current);
            return true;
        }

        public void AdvanceCrossfade(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }
            if (!IsCrossfading || deltaSeconds == 0f) return;
            m_CrossfadeElapsed = Mathf.Min(
                m_CrossfadeElapsed + deltaSeconds,
                m_CrossfadeDuration);
            ApplyVolumes(m_Settings.Current);
            if (m_CrossfadeElapsed < m_CrossfadeDuration) return;

            musicFoundation.Stop();
            musicSignal.Stop();
            musicFoundation.clip = null;
            musicSignal.clip = null;
            (musicFoundation, incomingMusicFoundation) =
                (incomingMusicFoundation, musicFoundation);
            (musicSignal, incomingMusicSignal) =
                (incomingMusicSignal, musicSignal);
            m_CrossfadeElapsed = 0f;
            m_CrossfadeDuration = 0f;
            ApplyVolumes(m_Settings.Current);
        }

        public bool PlayCue(string cueId)
        {
            if (library == null || !library.TryFindCue(cueId, out var cue))
            {
                return false;
            }

            switch (cue.Bus)
            {
                case AudioBus.Music:
                    CancelCrossfade(discardIncoming: true);
                    m_CurrentMusicState = null;
                    m_FoundationGain = cue.Gain;
                    m_SignalGain = 0f;
                    musicSignal.Stop();
                    musicSignal.clip = null;
                    PrepareLoop(musicFoundation, cue.Clip);
                    ApplyVolumes(m_Settings.Current);
                    musicFoundation.Play();
                    return true;
                case AudioBus.Dialogue:
                    m_DialogueGain = cue.Gain;
                    PrepareOneShot(dialogue, cue.Clip);
                    ApplyVolumes(m_Settings.Current);
                    dialogue.Play();
                    return true;
                case AudioBus.Effects:
                    m_EffectsGain = cue.Gain;
                    PrepareOneShot(effects, cue.Clip);
                    ApplyVolumes(m_Settings.Current);
                    effects.Play();
                    return true;
                default:
                    return false;
            }
        }

        public bool Play(SoundtrackTrack track) =>
            track != null && PlayCue(track.CueId);

        public void Stop()
        {
            CancelCrossfade(discardIncoming: true);
            m_CurrentMusicState = null;
            musicFoundation?.Stop();
            musicSignal?.Stop();
        }

        public void StopAll()
        {
            Stop();
            dialogue?.Stop();
            effects?.Stop();
        }

        private static AudioSource CreateSource(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child.AddComponent<AudioSource>();
        }

        private static void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private static void PrepareLoop(AudioSource source, AudioClip clip)
        {
            source.Stop();
            source.clip = clip;
            source.loop = true;
        }

        private static void PrepareOneShot(AudioSource source, AudioClip clip)
        {
            source.Stop();
            source.clip = clip;
            source.loop = false;
        }

        private void Update()
        {
            if (IsCrossfading) AdvanceCrossfade(Time.unscaledDeltaTime);
        }

        private void StartStateImmediately(
            MusicStateDefinition state,
            AudioCueDefinition foundation,
            AudioCueDefinition signal)
        {
            CancelCrossfade(discardIncoming: true);
            m_CurrentMusicState = state;
            m_FoundationGain = foundation.Gain;
            m_SignalGain = signal.Gain * state.SignalLevel;
            PrepareLoop(musicFoundation, foundation.Clip);
            PrepareLoop(musicSignal, signal.Clip);
            musicFoundation.timeSamples = 0;
            musicSignal.timeSamples = 0;
            musicFoundation.Play();
            musicSignal.Play();
            ApplyVolumes(m_Settings.Current);
        }

        private void CancelCrossfade(bool discardIncoming)
        {
            m_CrossfadeElapsed = 0f;
            m_CrossfadeDuration = 0f;
            if (!discardIncoming) return;
            incomingMusicFoundation?.Stop();
            incomingMusicSignal?.Stop();
            if (incomingMusicFoundation != null) incomingMusicFoundation.clip = null;
            if (incomingMusicSignal != null) incomingMusicSignal.clip = null;
        }

        private void OnSettingsChanged(GameSettings settings) => ApplyVolumes(settings);

        private void ApplyVolumes(GameSettings settings)
        {
            if (settings == null) return;
            if (musicFoundation != null)
                musicFoundation.volume = settings.MusicVolume *
                    (IsCrossfading
                        ? m_PreviousFoundationGain * (1f - CrossfadeProgress)
                        : m_FoundationGain);
            if (musicSignal != null)
                musicSignal.volume = settings.MusicVolume *
                    (IsCrossfading
                        ? m_PreviousSignalGain * (1f - CrossfadeProgress)
                        : m_SignalGain);
            if (incomingMusicFoundation != null)
                incomingMusicFoundation.volume = settings.MusicVolume *
                    (IsCrossfading ? m_FoundationGain * CrossfadeProgress : 0f);
            if (incomingMusicSignal != null)
                incomingMusicSignal.volume = settings.MusicVolume *
                    (IsCrossfading ? m_SignalGain * CrossfadeProgress : 0f);
            if (dialogue != null)
                dialogue.volume = settings.DialogueVolume * m_DialogueGain;
            if (effects != null)
                effects.volume = settings.EffectsVolume * m_EffectsGain;
        }

        private void OnDestroy()
        {
            ReleaseSettings();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void ReleaseSettings()
        {
            if (m_Settings != null)
            {
                m_Settings.SettingsChanged -= OnSettingsChanged;
                m_Settings = null;
            }
        }
    }
}
