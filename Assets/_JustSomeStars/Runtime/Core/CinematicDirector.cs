using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Cinematics;
using JustSomeStars.Runtime.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace JustSomeStars.Runtime.Core
{
    public enum CinematicBeatKind
    {
        BodyClip = 0,
        Expression = 1,
        Viseme = 2,
        Audio = 3,
        Vfx = 4,
        Caption = 5,
        InteractionRelease = 6,
    }

    [Serializable]
    public sealed class CinematicBeatDefinition
    {
        [SerializeField, Min(0f)] private float startSeconds;
        [SerializeField] private CinematicBeatKind kind;
        [SerializeField] private string actorId;
        [SerializeField] private string value;
        [SerializeField, Min(0f)] private float durationSeconds;

        public CinematicBeatDefinition(
            float authoredStartSeconds,
            CinematicBeatKind authoredKind,
            string authoredActorId,
            string authoredValue,
            float authoredDurationSeconds)
        {
            startSeconds = authoredStartSeconds;
            kind = authoredKind;
            actorId = authoredActorId ?? string.Empty;
            value = authoredValue ?? string.Empty;
            durationSeconds = authoredDurationSeconds;
        }

        public float StartSeconds => startSeconds;
        public CinematicBeatKind Kind => kind;
        public string ActorId => actorId;
        public string Value => value;
        public float DurationSeconds => durationSeconds;

        public void ValidateOrThrow()
        {
            if (startSeconds < 0f || float.IsNaN(startSeconds) ||
                float.IsInfinity(startSeconds) ||
                !Enum.IsDefined(typeof(CinematicBeatKind), kind) ||
                string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                durationSeconds < 0f || float.IsNaN(durationSeconds) ||
                float.IsInfinity(durationSeconds))
            {
                throw new InvalidOperationException(
                    "Cinematic beats require finite timing and canonical values.");
            }

            var needsActor = kind == CinematicBeatKind.BodyClip ||
                kind == CinematicBeatKind.Expression ||
                kind == CinematicBeatKind.Viseme ||
                kind == CinematicBeatKind.Caption;
            if (needsActor && (string.IsNullOrWhiteSpace(actorId) ||
                !string.Equals(actorId, actorId.Trim(), StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Cinematic {kind} beat requires a canonical actor id.");
            }

            if (kind == CinematicBeatKind.Caption && durationSeconds <= 0f)
            {
                throw new InvalidOperationException(
                    "Cinematic caption beats require readable duration.");
            }
            if (kind != CinematicBeatKind.Caption && durationSeconds != 0f)
            {
                throw new InvalidOperationException(
                    $"Only cinematic captions may declare duration ({kind}).");
            }
            if (kind == CinematicBeatKind.Viseme &&
                (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                     out var viseme) || viseme < 0 ||
                 viseme >= FacialAtlasController2D.VisemeCount))
            {
                throw new InvalidOperationException(
                    "Cinematic viseme beat must identify an authored speech frame.");
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class CinematicDirector : MonoBehaviour,
        IChapterOneSequenceExtension,
        IChapterOnePresentationGate
    {
        [SerializeField] private CinematicSequenceDefinition sequence;
        [SerializeField] private LocalizedEnglishCatalog english;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private SpriteRenderer fallbackRenderer;
        [SerializeField] private GameObject captionRoot;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text captionLabel;
        [SerializeField] private FacialAtlasController2D facialAtlas;
        [SerializeField] private CinematicActor2D[] actors =
            Array.Empty<CinematicActor2D>();

        private SettingsService m_Settings;
        private AudioDirector m_Audio;
        private float m_Elapsed;
        private float m_CaptionEndsAt;
        private int m_NextBeat;
        private bool m_FrameReleaseRequested;
        private bool m_VideoEventsBound;

        public event Action<CinematicBeatDefinition> BeatFired;
        public event Action<string> InteractionReleased;
        public event Action<string> VfxRequested;

        public bool IsPlaying { get; private set; }
        public bool IsUsingFallback { get; private set; }
        public bool InteractionIsReleased { get; private set; }
        public string SequenceId => sequence != null ? sequence.StableId : string.Empty;
        public bool HasFrameEventReleaseRoute => sequence != null &&
            actors != null && sequence.Beats
                .Where(beat => beat.Kind == CinematicBeatKind.BodyClip)
                .Any(beat => FindActor(beat.ActorId)?.HasPerformanceEvent(
                    beat.Value,
                    SpriteFrameEventKind.InteractionRelease) == true);

        public void ConfigureAuthored(
            CinematicSequenceDefinition definition,
            LocalizedEnglishCatalog localization,
            SpriteRenderer fallback,
            GameObject authoredCaptionRoot,
            TMP_Text speaker,
            TMP_Text caption,
            FacialAtlasController2D facialController,
            CinematicActor2D[] authoredActors)
        {
            sequence = definition != null
                ? definition
                : throw new ArgumentNullException(nameof(definition));
            english = localization != null
                ? localization
                : throw new ArgumentNullException(nameof(localization));
            fallbackRenderer = fallback != null
                ? fallback
                : throw new ArgumentNullException(nameof(fallback));
            captionRoot = authoredCaptionRoot != null
                ? authoredCaptionRoot
                : throw new ArgumentNullException(nameof(authoredCaptionRoot));
            speakerLabel = speaker != null
                ? speaker
                : throw new ArgumentNullException(nameof(speaker));
            captionLabel = caption != null
                ? caption
                : throw new ArgumentNullException(nameof(caption));
            facialAtlas = facialController;
            actors = authoredActors != null
                ? (CinematicActor2D[])authoredActors.Clone()
                : Array.Empty<CinematicActor2D>();
            ValidateAuthoredOrThrow();
        }

        public void ConfigureForTests(
            CinematicSequenceDefinition definition,
            LocalizedEnglishCatalog localization,
            SettingsService settings,
            SpriteRenderer fallback,
            TMP_Text speaker,
            TMP_Text caption,
            CinematicActor2D[] authoredActors = null)
        {
            sequence = definition;
            english = localization;
            m_Settings = settings;
            fallbackRenderer = fallback;
            speakerLabel = speaker;
            captionLabel = caption;
            captionRoot = speaker != null ? speaker.transform.parent.gameObject : null;
            actors = authoredActors != null
                ? (CinematicActor2D[])authoredActors.Clone()
                : Array.Empty<CinematicActor2D>();
            ValidateOrThrow();
        }

        public void Configure(ChapterOneSequenceDependencies dependencies)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
            if (m_Settings != null && !ReferenceEquals(m_Settings, dependencies.Settings))
            {
                throw new InvalidOperationException(
                    "CinematicDirector is already owned by another sequence.");
            }
            m_Settings = dependencies.Settings ?? throw new InvalidOperationException(
                "CinematicDirector requires composed settings.");
            m_Audio = AudioDirector.Instance;
            ValidateOrThrow();
        }

        public void BeginAfterSceneStateApplied() => Begin();

        public void Release(ChapterOneSequenceDependencies dependencies)
        {
            if (dependencies != null && m_Settings != null &&
                !ReferenceEquals(m_Settings, dependencies.Settings))
            {
                throw new InvalidOperationException(
                    "CinematicDirector can only release its owning sequence.");
            }
            Cancel();
            m_Settings = null;
            m_Audio = null;
        }

        public void Begin()
        {
            ValidateOrThrow();
            CancelPresentation(clearFallback: false);
            UnbindActorEvents();
            foreach (var actor in actors)
            {
                actor.FrameEventEmitted -= OnActorFrameEvent;
                actor.FrameEventEmitted += OnActorFrameEvent;
            }
            m_Elapsed = 0f;
            m_CaptionEndsAt = 0f;
            m_NextBeat = 0;
            m_FrameReleaseRequested = false;
            InteractionIsReleased = false;
            IsPlaying = true;
            ShowFallback();
            var canPlayVideo = sequence.OptionalVideo != null && videoPlayer != null;
            if (canPlayVideo)
            {
                BindVideoEvents();
                videoPlayer.clip = sequence.OptionalVideo;
                videoPlayer.isLooping = false;
                videoPlayer.Prepare();
            }
            DispatchDueBeats();
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }
            if (!IsPlaying || deltaSeconds == 0f) return;
            m_Elapsed += deltaSeconds;
            DispatchDueBeats();
            if (m_CaptionEndsAt > 0f && m_Elapsed >= m_CaptionEndsAt)
            {
                ClearCaption();
                facialAtlas?.EndSpeech();
                m_CaptionEndsAt = 0f;
                if (m_FrameReleaseRequested) CompleteInteractionRelease("continue");
            }
            if (InteractionIsReleased && m_CaptionEndsAt <= 0f)
            {
                IsPlaying = false;
                UnbindActorEvents();
                UnbindVideoEvents();
                foreach (var actor in actors) actor?.EndPerformance();
            }
        }

        public void Cancel()
        {
            IsPlaying = false;
            InteractionIsReleased = false;
            m_FrameReleaseRequested = false;
            UnbindActorEvents();
            UnbindVideoEvents();
            foreach (var actor in actors ?? Array.Empty<CinematicActor2D>())
                actor?.EndPerformance();
            CancelPresentation(clearFallback: false);
        }

        public void ValidateOrThrow()
        {
            ValidateAuthoredOrThrow();
            if (m_Settings == null)
            {
                throw new InvalidOperationException(
                    "CinematicDirector requires composed settings.");
            }
        }

        public void ValidateAuthoredOrThrow()
        {
            if (sequence == null || english == null || fallbackRenderer == null ||
                captionRoot == null || speakerLabel == null || captionLabel == null)
            {
                throw new InvalidOperationException(
                    "CinematicDirector requires sequence, localization, fallback " +
                    "and caption presentation.");
            }
            sequence.ValidateOrThrow();
            english.ValidateOrThrow();
            actors ??= Array.Empty<CinematicActor2D>();
            if (actors.Any(actor => actor == null ||
                    string.IsNullOrWhiteSpace(actor.ActorId)) ||
                actors.Select(actor => actor.ActorId)
                    .Distinct(StringComparer.Ordinal).Count() != actors.Length)
            {
                throw new InvalidOperationException(
                    "Cinematic actors must be unique and configured.");
            }
            foreach (var caption in sequence.Beats.Where(beat =>
                         beat.Kind == CinematicBeatKind.Caption))
            {
                _ = english.Resolve(caption.Value);
            }
            foreach (var actor in actors)
            {
                actor.ValidateOrThrow();
            }
            if (sequence.Beats.Any(beat =>
                    beat.Kind == CinematicBeatKind.BodyClip))
            {
                foreach (var beat in sequence.Beats.Where(beat =>
                             beat.Kind == CinematicBeatKind.BodyClip))
                {
                    var actor = FindActor(beat.ActorId);
                    if (actor == null || !actor.HasPerformance(beat.Value))
                    {
                        throw new InvalidOperationException(
                            $"Cinematic body performance '{beat.ActorId}/" +
                            $"{beat.Value}' is not authored.");
                    }
                }
                if (!HasFrameEventReleaseRoute)
                {
                    throw new InvalidOperationException(
                        "Body-driven cinematics require one frame-event release route.");
                }
            }
        }

        private void Update()
        {
            if (IsPlaying) Advance(Time.unscaledDeltaTime);
        }

        private void OnDestroy() => Cancel();

        private void DispatchDueBeats()
        {
            while (IsPlaying && m_NextBeat < sequence.Beats.Count &&
                   sequence.Beats[m_NextBeat].StartSeconds <= m_Elapsed + 0.00001f)
            {
                var beat = sequence.Beats[m_NextBeat++];
                Dispatch(beat);
                BeatFired?.Invoke(beat);
            }
        }

        private void Dispatch(CinematicBeatDefinition beat)
        {
            switch (beat.Kind)
            {
                case CinematicBeatKind.BodyClip:
                    FindActor(beat.ActorId)?.PlayBody(beat.Value);
                    break;
                case CinematicBeatKind.Expression:
                    facialAtlas?.ShowExpression(beat.ActorId, beat.Value);
                    break;
                case CinematicBeatKind.Viseme:
                    if (int.TryParse(
                            beat.Value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var viseme))
                    {
                        facialAtlas?.ShowViseme(beat.ActorId, viseme);
                    }
                    break;
                case CinematicBeatKind.Audio:
                    if (beat.Value.StartsWith("state:", StringComparison.Ordinal))
                    {
                        m_Audio?.SetMusicState(beat.Value.Substring("state:".Length));
                    }
                    else
                    {
                        m_Audio?.PlayCue(beat.Value);
                    }
                    break;
                case CinematicBeatKind.Vfx:
                    VfxRequested?.Invoke(beat.Value);
                    break;
                case CinematicBeatKind.Caption:
                    PresentCaption(beat);
                    break;
                case CinematicBeatKind.InteractionRelease:
                    CompleteInteractionRelease(beat.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnActorFrameEvent(
            CinematicActor2D actor,
            SpriteFrameEvent frameEvent)
        {
            if (!IsPlaying) return;
            switch (frameEvent.Kind)
            {
                case SpriteFrameEventKind.FootContact:
                    m_Audio?.PlayCue("cue.sfx.footstep.soil");
                    break;
                case SpriteFrameEventKind.ToolAttach:
                    m_Audio?.PlayCue("cue.sfx.tool.attach");
                    break;
                case SpriteFrameEventKind.ToolDetach:
                    m_Audio?.PlayCue("cue.sfx.tool.detach");
                    break;
                case SpriteFrameEventKind.Audio:
                    if (frameEvent.Id.StartsWith("state:", StringComparison.Ordinal))
                    {
                        m_Audio?.SetMusicState(
                            frameEvent.Id.Substring("state:".Length));
                    }
                    else
                    {
                        m_Audio?.PlayCue(frameEvent.Id);
                    }
                    break;
                case SpriteFrameEventKind.Vfx:
                    VfxRequested?.Invoke(frameEvent.Id);
                    break;
                case SpriteFrameEventKind.Caption:
                    PresentCaption(new CinematicBeatDefinition(
                        m_Elapsed,
                        CinematicBeatKind.Caption,
                        actor.ActorId,
                        frameEvent.Id,
                        2f));
                    break;
                case SpriteFrameEventKind.Expression:
                    facialAtlas?.ShowExpression(actor.ActorId, frameEvent.Id);
                    break;
                case SpriteFrameEventKind.Viseme:
                    if (int.TryParse(
                            frameEvent.Id,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var viseme))
                    {
                        facialAtlas?.ShowViseme(actor.ActorId, viseme);
                    }
                    break;
                case SpriteFrameEventKind.InteractionRelease:
                    RequestFrameEventRelease(frameEvent.Id);
                    break;
                case SpriteFrameEventKind.Interaction:
                    if (string.Equals(
                            frameEvent.Id,
                            "interact-commit",
                            StringComparison.Ordinal))
                    {
                        RequestFrameEventRelease(frameEvent.Id);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UnbindActorEvents()
        {
            foreach (var actor in actors ?? Array.Empty<CinematicActor2D>())
            {
                if (actor != null) actor.FrameEventEmitted -= OnActorFrameEvent;
            }
        }

        private CinematicActor2D FindActor(string actorId)
        {
            var canonical = CanonicalActor(actorId);
            return actors.SingleOrDefault(actor => string.Equals(
                actor.ActorId,
                canonical,
                StringComparison.Ordinal));
        }

        private void PresentCaption(CinematicBeatDefinition beat)
        {
            var settings = m_Settings.Current;
            var actor = CanonicalActor(beat.ActorId);
            var localized = english.Resolve(beat.Value);
            if (settings.CaptionsEnabled)
            {
                captionRoot.SetActive(true);
                speakerLabel.text = english.Resolve("actor." + actor);
                captionLabel.text = localized;
            }
            else
            {
                ClearCaption();
            }
            var readable = MirraDialoguePresenter2D.CalculateReadableDuration(
                localized,
                settings.DialogueSpeed,
                beat.DurationSeconds);
            m_CaptionEndsAt = m_Elapsed + readable;
        }

        private void CancelPresentation(bool clearFallback)
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            ClearCaption();
            facialAtlas?.Hide();
            if (clearFallback && fallbackRenderer != null)
            {
                fallbackRenderer.enabled = false;
            }
        }

        private void RequestFrameEventRelease(string releaseId)
        {
            if (m_CaptionEndsAt > m_Elapsed)
            {
                m_FrameReleaseRequested = true;
                return;
            }
            CompleteInteractionRelease(releaseId);
        }

        private void CompleteInteractionRelease(string releaseId)
        {
            if (InteractionIsReleased) return;
            m_FrameReleaseRequested = false;
            InteractionIsReleased = true;
            InteractionReleased?.Invoke(releaseId);
        }

        private void ShowFallback()
        {
            IsUsingFallback = true;
            if (fallbackRenderer == null) return;
            fallbackRenderer.sprite = sequence.FallbackStill;
            fallbackRenderer.enabled = true;
        }

        private void BindVideoEvents()
        {
            if (videoPlayer == null || m_VideoEventsBound) return;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.started += OnVideoStarted;
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.loopPointReached += OnVideoFinished;
            m_VideoEventsBound = true;
        }

        private void UnbindVideoEvents()
        {
            if (videoPlayer == null || !m_VideoEventsBound) return;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.started -= OnVideoStarted;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.loopPointReached -= OnVideoFinished;
            m_VideoEventsBound = false;
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            if (!IsPlaying || source != videoPlayer) return;
            source.Play();
        }

        private void OnVideoStarted(VideoPlayer source)
        {
            if (!IsPlaying || source != videoPlayer) return;
            IsUsingFallback = false;
            if (fallbackRenderer != null) fallbackRenderer.enabled = false;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            if (source != videoPlayer) return;
            source.Stop();
            ShowFallback();
            Debug.LogWarning(
                $"Cinematic '{SequenceId}' video failed; using authored still. " +
                message,
                this);
        }

        private void OnVideoFinished(VideoPlayer source)
        {
            if (source != videoPlayer) return;
            ShowFallback();
        }

        private void ClearCaption()
        {
            if (speakerLabel != null) speakerLabel.text = string.Empty;
            if (captionLabel != null) captionLabel.text = string.Empty;
            if (captionRoot != null) captionRoot.SetActive(false);
        }

        private static string CanonicalActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId)) return string.Empty;
            var value = actorId.Trim().ToLowerInvariant();
            if (value.StartsWith("crew.", StringComparison.Ordinal))
                value = value.Substring("crew.".Length);
            if (value.StartsWith("robot.", StringComparison.Ordinal))
                value = value.Substring("robot.".Length);
            return value;
        }
    }
}
