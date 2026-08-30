using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Missions;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    public enum LandingSequenceState
    {
        Flight = 0,
        Approach = 1,
        Transitioning = 2,
        Completed = 3,
    }

    [DisallowMultipleComponent]
    public sealed class LandingSequence : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Mirra2DProof";
        [SerializeField] private string destinationId = "destination.mirra.surface";
        [SerializeField] private PlayerShipPresentation2D presentation;
        [SerializeField] private FlightMotor2D motor;
        [SerializeField, Min(0.05f)] private float landingVisualSeconds = 0.32f;

        private ISceneTransition scenes;
        private GameEventBus events;
        private GameModeController modes;
        private MirraProgressionService progression;
        private CancellationTokenSource transitionCancellation;
        private bool attemptInProgress;
        private bool releaseRequested;
        private bool publicationCompleted;
        private bool approachPublished;

        public LandingSequenceState State { get; private set; }

        public void Configure(ISceneTransition scenes, GameEventBus events)
        {
            Configure(scenes, events, null, null);
        }

        public void Configure(
            ISceneTransition scenes,
            GameEventBus events,
            GameModeController modes,
            MirraProgressionService progression)
        {
            if (scenes == null)
            {
                throw new ArgumentNullException(nameof(scenes));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (this.scenes != null)
            {
                if (ReferenceEquals(this.scenes, scenes) &&
                    ReferenceEquals(this.events, events))
                {
                    releaseRequested = false;
                    return;
                }

                throw new InvalidOperationException(
                    "LandingSequence is already owned by another composition.");
            }

            _ = new ContentId(destinationId);
            if (string.IsNullOrWhiteSpace(destinationScene))
            {
                throw new InvalidOperationException(
                    "LandingSequence requires a destination scene.");
            }

            this.scenes = scenes;
            this.events = events;
            this.modes = modes;
            this.progression = progression;
            State = LandingSequenceState.Flight;
        }

        public async Task<bool> TryLandAsync(
            bool approachIsValid,
            CancellationToken cancellationToken)
        {
            if (scenes == null || events == null)
            {
                throw new InvalidOperationException(
                    "LandingSequence must be configured before use.");
            }

            if (!approachIsValid || attemptInProgress ||
                State == LandingSequenceState.Completed)
            {
                return false;
            }
            cancellationToken.ThrowIfCancellationRequested();

            FlightState? rollbackState = null;
            if (motor != null)
            {
                if (!motor.TryLockForLanding(out var capturedState))
                {
                    return false;
                }

                rollbackState = capturedState;
            }

            var ownedScenes = scenes;
            var ownedEvents = events;
            State = LandingSequenceState.Approach;
            var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            transitionCancellation = attemptCancellation;
            attemptInProgress = true;
            releaseRequested = false;
            State = LandingSequenceState.Transitioning;
            try
            {
                if (progression != null && !approachPublished)
                {
                    ownedEvents.Publish(new ApproachCompleted(
                        progression.Content.ApproachId));
                    await progression.FlushPendingAsync(attemptCancellation.Token);
                    approachPublished = true;
                }

                await PlayLandingVisualsAsync(attemptCancellation.Token);
                if (modes != null && modes.CurrentMode != GameMode.Surface)
                {
                    await modes.EnterAsync(GameMode.Surface, attemptCancellation.Token);
                }
                await ownedScenes.RouteAsync(
                    destinationScene,
                    attemptCancellation.Token);

                // Unity cannot cancel a scene load after it has accepted the
                // request. A successful RouteAsync therefore owns completion,
                // even though releasing the source scene synchronously clears
                // its live bindings while the load is in progress.
                if (!publicationCompleted)
                {
                    ownedEvents.Publish(
                        new LandingCompleted(new ContentId(destinationId)));
                    if (progression != null)
                    {
                        // RouteAsync has already activated the destination and
                        // released the source scene. Source teardown therefore
                        // cannot cancel the durable landing commit: at this
                        // point cancellation cannot roll the route back.
                        await progression.FlushPendingAsync(
                            CancellationToken.None);
                    }
                    publicationCompleted = true;
                }

                State = LandingSequenceState.Completed;
                return true;
            }
            catch
            {
                ResetLandingVisuals();
                if (rollbackState.HasValue && motor != null)
                {
                    motor.RestoreAfterLandingFailure(rollbackState.Value);
                }

                if (modes != null && modes.CurrentMode == GameMode.Surface)
                {
                    await modes.EnterAsync(GameMode.Flight, CancellationToken.None);
                }

                State = LandingSequenceState.Flight;
                throw;
            }
            finally
            {
                attemptInProgress = false;
                if (ReferenceEquals(transitionCancellation, attemptCancellation))
                {
                    transitionCancellation = null;
                }

                attemptCancellation.Dispose();
                if (releaseRequested)
                {
                    ClearBindings();
                }
            }
        }

        public void Cancel()
        {
            transitionCancellation?.Cancel();
            if (!attemptInProgress)
            {
                State = LandingSequenceState.Flight;
            }
        }

        public void Release()
        {
            if (attemptInProgress)
            {
                // RouteAsync releases the source scene before it starts the
                // non-cancellable Unity load. Keep this attempt's captured
                // dependencies alive until its continuation publishes once.
                releaseRequested = true;
                return;
            }

            ClearBindings();
        }

        private void ClearBindings()
        {
            scenes = null;
            events = null;
            modes = null;
            progression = null;
            publicationCompleted = false;
            approachPublished = false;
            releaseRequested = false;
            State = LandingSequenceState.Flight;
        }

        private async Task PlayLandingVisualsAsync(
            CancellationToken cancellationToken)
        {
            if (presentation == null)
            {
                return;
            }

            presentation.SetDoorOpen(false);
            presentation.SetLandingProgress(0f);
            var elapsed = 0f;
            while (elapsed < landingVisualSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                elapsed += Time.unscaledDeltaTime;
                presentation.SetLandingProgress(
                    Mathf.Clamp01(elapsed / landingVisualSeconds));
            }

            cancellationToken.ThrowIfCancellationRequested();
            presentation.SetLandingProgress(1f);
            presentation.SetDoorOpen(true);
        }

        private void ResetLandingVisuals()
        {
            if (presentation == null)
            {
                return;
            }

            presentation.SetLandingProgress(0f);
            presentation.SetDoorOpen(false);
        }
    }
}
