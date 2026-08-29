using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Rendering2D;
using UnityEngine;

namespace JustSomeStars.Runtime.Discovery
{
    public sealed class DiscoveryLensController : IDisposable
    {
        private const float AimUnitsPerSecond = 4f;

        private readonly InputRouter m_Input;
        private readonly GameModeController m_Modes;
        private readonly SettingsService m_Settings;
        private readonly EvidenceRecorder m_Recorder;
        private readonly Camera m_Camera;
        private readonly Func<Vector2> m_AimOrigin;
        private readonly DiscoveryLensTarget2D[] m_Targets;
        private readonly InstrumentDefinition[] m_Instruments;
        private readonly Prediction[] m_Predictions;
        private readonly CancellationTokenSource m_Lifetime =
            new CancellationTokenSource();

        private InstrumentDefinition m_SelectedInstrument;
        private Prediction m_SelectedPrediction;
        private DiscoveryLensTarget2D m_FocusedTarget;
        private Vector2 m_AimWorld;
        private float m_ScanProgress;
        private bool m_IsBound;
        private bool m_IsDisposed;
        private bool m_CompletedCurrentFocus;
        private bool m_IsToggleTransitionStarting;

        public DiscoveryLensController(
            InputRouter input,
            GameModeController modes,
            SettingsService settings,
            EvidenceRecorder recorder,
            Camera compositionCamera,
            Func<Vector2> aimOrigin,
            IEnumerable<DiscoveryLensTarget2D> targets,
            IEnumerable<InstrumentDefinition> instruments,
            IEnumerable<Prediction> predictions = null)
        {
            m_Input = input ?? throw new ArgumentNullException(nameof(input));
            m_Modes = modes ?? throw new ArgumentNullException(nameof(modes));
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_Recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            m_Camera = compositionCamera ?? throw new ArgumentNullException(
                nameof(compositionCamera));
            m_AimOrigin = aimOrigin ?? throw new ArgumentNullException(
                nameof(aimOrigin));
            m_Targets = targets?.ToArray() ?? throw new ArgumentNullException(
                nameof(targets));
            m_Instruments = instruments?.ToArray() ?? throw new ArgumentNullException(
                nameof(instruments));
            m_Predictions = predictions?.ToArray() ?? Array.Empty<Prediction>();
            if (!m_Camera.orthographic)
            {
                throw new InvalidOperationException(
                    "Discovery Lens requires the orthographic composition camera.");
            }

            if (m_Targets.Any(target => target == null || !target.IsConfigured) ||
                m_Targets.Select(target => target.Phenomenon.StableId)
                    .Distinct().Count() != m_Targets.Length)
            {
                throw new InvalidOperationException(
                    "Discovery Lens targets must be configured and uniquely identified.");
            }

            if (m_Instruments.Any(instrument => instrument == null))
            {
                throw new InvalidOperationException(
                    "Discovery Lens instruments cannot contain null entries.");
            }

            foreach (var instrument in m_Instruments)
            {
                instrument.ValidateOrThrow();
            }

            if (m_Instruments.Select(instrument => instrument.StableId)
                .Distinct().Count() != m_Instruments.Length)
            {
                throw new InvalidOperationException(
                    "Discovery Lens instrument IDs must be unique.");
            }

            if (m_Predictions.Any(prediction => prediction == null))
            {
                throw new InvalidOperationException(
                    "Discovery Lens predictions cannot contain null entries.");
            }
            foreach (var prediction in m_Predictions)
            {
                prediction.ValidateOrThrow();
            }
            if (m_Predictions.Select(prediction => prediction.StableId)
                .Distinct().Count() != m_Predictions.Length ||
                m_Predictions.Any(prediction => !m_Targets.Any(target =>
                    target.Phenomenon.StableId == prediction.PhenomenonId)))
            {
                throw new InvalidOperationException(
                    "Discovery Lens predictions must be unique and target owned phenomena.");
            }

            SelectedMode = LensMode.Imaging;
            SelectedBand = m_Targets.Length == 0 || m_Targets.Any(target =>
                    target.Phenomenon.DepthBand == LayerBand.Gameplay)
                ? LayerBand.Gameplay
                : m_Targets.OrderBy(target => target.Phenomenon.DepthBand)
                    .ThenBy(target => target.Phenomenon.StableId.Value,
                        StringComparer.Ordinal)
                    .Select(target => target.Phenomenon.DepthBand)
                    .FirstOrDefault();
            ReticleState = LensReticleState.Inactive;
            ActiveTransition = Task.CompletedTask;
        }

        public LensMode SelectedMode { get; private set; }

        public LayerBand SelectedBand { get; private set; }

        public LensReticleState ReticleState { get; private set; }

        public float ScanProgress => m_ScanProgress;

        public Vector2 AimWorld => m_AimWorld;

        public Camera CompositionCamera => m_Camera;

        public bool IsActive => m_IsBound &&
            m_Modes.CurrentMode == GameMode.Lens;

        public DiscoveryLensTarget2D FocusedTarget => m_FocusedTarget;

        public EvidenceRecord LastEvidence { get; private set; }

        public Exception LastTransitionFailure { get; private set; }

        public Task ActiveTransition { get; private set; }

        public ScienceDepth ScienceDepth => m_Settings.Current.ScienceDepth;

        public void Bind()
        {
            ThrowIfDisposed();
            if (m_IsBound)
            {
                return;
            }

            m_Input.GameplayCommandPerformed += OnGameplayCommand;
            m_Modes.StateChanged += OnModeStateChanged;
            m_IsBound = true;
            SynchronizeMode(m_Modes.CurrentPolicy);
        }

        public void SelectMode(LensMode mode)
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(typeof(LensMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (mode == SelectedMode)
            {
                return;
            }

            SelectedMode = mode;
            var compatible = m_Instruments.FirstOrDefault(item => item.Supports(mode));
            if (compatible != null)
            {
                m_SelectedInstrument = compatible;
            }
            ResetScan(clearFocus: true);
        }

        public void CycleMode()
        {
            var count = Enum.GetValues(typeof(LensMode)).Length;
            SelectMode((LensMode)(((int)SelectedMode + 1) % count));
        }

        public void SetSelectedBand(LayerBand band)
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(typeof(LayerBand), band) || band == LayerBand.Hud)
            {
                throw new ArgumentOutOfRangeException(nameof(band));
            }

            if (band == SelectedBand)
            {
                return;
            }

            SelectedBand = band;
            ResetScan(clearFocus: true);
        }

        public void SelectInstrument(InstrumentDefinition instrument)
        {
            ThrowIfDisposed();
            if (instrument == null)
            {
                throw new ArgumentNullException(nameof(instrument));
            }

            instrument.ValidateOrThrow();
            if (!m_Instruments.Any(candidate =>
                    candidate.StableId == instrument.StableId))
            {
                throw new InvalidOperationException(
                    "The selected instrument is not owned by this Lens controller.");
            }

            m_SelectedInstrument = instrument;
            ResetScan(clearFocus: false);
        }

        public void SelectPrediction(Prediction prediction)
        {
            ThrowIfDisposed();
            m_SelectedPrediction = prediction ?? throw new ArgumentNullException(
                nameof(prediction));
            ResetScan(clearFocus: false);
        }

        public void Tick(float deltaTime)
        {
            Advance(
                deltaTime,
                m_Input.ReadLook(),
                m_Input.IsCommandPressed(SemanticGameplayCommand.Primary));
        }

        public void Advance(
            float deltaTime,
            Vector2 semanticLook,
            bool scanHeld)
        {
            ThrowIfDisposed();
            if (deltaTime < 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (!IsActive)
            {
                ResetScan(clearFocus: true);
                ReticleState = LensReticleState.Inactive;
                return;
            }

            m_AimWorld = ClampToCamera(
                m_AimWorld + semanticLook * (AimUnitsPerSecond * deltaTime));
            ResolveFocus();
            if (m_FocusedTarget == null)
            {
                ResetScan(clearFocus: false);
                ReticleState = LensReticleState.Searching;
                return;
            }

            var phenomenon = m_FocusedTarget.Phenomenon;
            var compatible = m_SelectedInstrument != null &&
                m_SelectedInstrument.IsCompatibleWith(phenomenon, SelectedMode) &&
                m_SelectedPrediction != null &&
                m_SelectedPrediction.PhenomenonId == phenomenon.StableId;
            if (!compatible)
            {
                ResetScan(clearFocus: false);
                ReticleState = m_SelectedInstrument == null &&
                    m_SelectedPrediction == null
                    ? LensReticleState.Focused
                    : LensReticleState.Incompatible;
                return;
            }

            if (m_CompletedCurrentFocus)
            {
                ReticleState = LensReticleState.Complete;
                return;
            }

            if (!scanHeld)
            {
                m_ScanProgress = 0f;
                ReticleState = LensReticleState.Focused;
                return;
            }

            ReticleState = LensReticleState.Scanning;
            m_ScanProgress = Mathf.Clamp01(
                m_ScanProgress + deltaTime /
                    m_SelectedInstrument.ScanDurationSeconds);
            if (m_ScanProgress < 1f)
            {
                return;
            }

            LastEvidence = m_Recorder.Record(
                m_SelectedPrediction,
                phenomenon,
                m_SelectedInstrument,
                SelectedMode);
            m_CompletedCurrentFocus = true;
            ReticleState = LensReticleState.Complete;
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            if (m_IsBound)
            {
                m_Input.GameplayCommandPerformed -= OnGameplayCommand;
                m_Modes.StateChanged -= OnModeStateChanged;
                m_IsBound = false;
            }

            var pendingTransition = ActiveTransition ?? Task.CompletedTask;
            m_Lifetime.Cancel();
            ResetScan(clearFocus: true);
            ReticleState = LensReticleState.Inactive;
            m_IsDisposed = true;
            ActiveTransition = CompleteDisposalAsync(pendingTransition);
        }

        private void OnGameplayCommand(
            GameplayInputMode inputMode,
            SemanticGameplayCommand command)
        {
            if (m_IsDisposed || !m_IsBound)
            {
                return;
            }

            if (command == SemanticGameplayCommand.Lens &&
                (inputMode == GameplayInputMode.Surface ||
                 inputMode == GameplayInputMode.Lens))
            {
                QueueToggle();
            }
            else if (inputMode == GameplayInputMode.Lens &&
                command == SemanticGameplayCommand.Secondary)
            {
                CycleMode();
            }
        }

        private void QueueToggle()
        {
            if (m_IsToggleTransitionStarting ||
                ActiveTransition != null && !ActiveTransition.IsCompleted)
            {
                return;
            }

            var destination = m_Modes.CurrentMode == GameMode.Lens
                ? GameMode.Surface
                : GameMode.Lens;
            m_IsToggleTransitionStarting = true;
            ActiveTransition = TransitionToAsync(
                destination,
                m_Lifetime.Token,
                clearToggleGuard: true);
        }

        private async Task TransitionToAsync(
            GameMode destination,
            CancellationToken cancellationToken,
            bool clearToggleGuard = false)
        {
            try
            {
                LastTransitionFailure = null;
                await m_Modes.EnterAsync(
                    destination,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (m_IsDisposed)
            {
                // Mode-service shutdown may cancel the teardown transition.
                // The controller is already unsubscribed and owns no active scan.
                LastTransitionFailure = null;
            }
            catch (Exception exception)
            {
                LastTransitionFailure = exception;
                Debug.LogException(exception);
            }
            finally
            {
                if (clearToggleGuard)
                {
                    m_IsToggleTransitionStarting = false;
                }
            }
        }

        private async Task CompleteDisposalAsync(Task pendingTransition)
        {
            try
            {
                await pendingTransition;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the expected way to quiesce an in-flight toggle.
            }
            catch (Exception exception)
            {
                LastTransitionFailure = exception;
                Debug.LogException(exception);
            }

            try
            {
                if (m_Modes.IsInitialized &&
                    m_Modes.CurrentMode != GameMode.Surface)
                {
                    await m_Modes.EnterAsync(
                        GameMode.Surface,
                        CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                // Composition shutdown may cancel final mode recovery after release.
            }
            catch (Exception exception)
            {
                LastTransitionFailure = exception;
                Debug.LogException(exception);
            }

            m_Lifetime.Dispose();
        }

        private void OnModeStateChanged(GameModeRuntimePolicy policy)
        {
            SynchronizeMode(policy);
        }

        private void SynchronizeMode(GameModeRuntimePolicy policy)
        {
            if (policy.Mode == GameMode.Lens && policy.Overlay == GameOverlay.None)
            {
                m_AimWorld = ClampToCamera(m_AimOrigin());
                ReticleState = LensReticleState.Searching;
            }
            else
            {
                ResetScan(clearFocus: true);
                ReticleState = LensReticleState.Inactive;
            }
        }

        private void ResolveFocus()
        {
            if (m_FocusedTarget != null && m_FocusedTarget.IsConfigured &&
                m_FocusedTarget.Phenomenon.DepthBand == SelectedBand &&
                m_FocusedTarget.IsVisibleFrom(m_Camera) &&
                m_FocusedTarget.CanRetainTrackFocus(m_AimWorld))
            {
                ApplyPredictionForFocus(m_FocusedTarget);
                return;
            }

            var next = m_Targets
                .Where(target => target != null && target.IsConfigured &&
                    target.Phenomenon.DepthBand == SelectedBand &&
                    target.IsVisibleFrom(m_Camera))
                .Select(target => new
                {
                    Target = target,
                    Distance = target.GetFocusDistanceSquared(m_AimWorld),
                })
                .Where(item => item.Distance <=
                    item.Target.Phenomenon.FocusRadius *
                    item.Target.Phenomenon.FocusRadius)
                .OrderBy(item => item.Distance)
                .ThenBy(item => item.Target.Phenomenon.StableId.Value,
                    StringComparer.Ordinal)
                .Select(item => item.Target)
                .FirstOrDefault();
            if (!ReferenceEquals(next, m_FocusedTarget))
            {
                ResetScan(clearFocus: false);
                m_FocusedTarget = next;
                ApplyPredictionForFocus(next);
            }

            foreach (var target in m_Targets)
            {
                target.SetFocused(ReferenceEquals(target, m_FocusedTarget));
            }
        }

        private void ApplyPredictionForFocus(DiscoveryLensTarget2D target)
        {
            if (target == null)
            {
                m_SelectedPrediction = null;
                return;
            }

            if (m_SelectedPrediction != null &&
                m_SelectedPrediction.PhenomenonId ==
                target.Phenomenon.StableId)
            {
                return;
            }

            m_SelectedPrediction = m_Predictions
                .Where(prediction => prediction.PhenomenonId ==
                    target.Phenomenon.StableId)
                .OrderBy(prediction => prediction.StableId.Value,
                    StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private Vector2 ClampToCamera(Vector2 position)
        {
            var minimum = m_Camera.ViewportToWorldPoint(
                new Vector3(0f, 0f, Mathf.Abs(m_Camera.transform.position.z)));
            var maximum = m_Camera.ViewportToWorldPoint(
                new Vector3(1f, 1f, Mathf.Abs(m_Camera.transform.position.z)));
            return new Vector2(
                Mathf.Clamp(position.x, minimum.x, maximum.x),
                Mathf.Clamp(position.y, minimum.y, maximum.y));
        }

        private void ResetScan(bool clearFocus)
        {
            m_ScanProgress = 0f;
            m_CompletedCurrentFocus = false;
            LastEvidence = null;
            if (!clearFocus)
            {
                return;
            }

            m_FocusedTarget = null;
            foreach (var target in m_Targets)
            {
                target?.SetFocused(false);
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DiscoveryLensController));
            }
        }
    }
}
