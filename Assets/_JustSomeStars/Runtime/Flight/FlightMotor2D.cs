using System;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    public sealed class FlightMotor2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private FlightDepthLane[] depthLanes =
            Array.Empty<FlightDepthLane>();
        [SerializeField] private GravityAssistVolume2D[] gravityAssists =
            Array.Empty<GravityAssistVolume2D>();
        [SerializeField] private FlightPredictionArc2D predictionArc;
        [SerializeField] private PlayerShipPresentation2D presentation;
        [SerializeField] private Vector2 routeMinimum = new Vector2(-12f, -5f);
        [SerializeField] private Vector2 routeMaximum = new Vector2(12f, 5f);
        [SerializeField] private int initialLane = 1;
        [SerializeField] private int predictionSteps = 30;
        [SerializeField] private float maximumSpeed = 9f;

        private FlightCheckpointSnapshot? latestCheckpoint;
        private InputRouter input;
        private SettingsService settings;
        private FlightModel2D model;
        private bool inputSuppressed;
        private bool laneInputHeld;
        private float failureElapsed;

        public FlightState State { get; private set; }

        public bool IsBound { get; private set; }

        public FlightCheckpointSnapshot? LatestCheckpoint => latestCheckpoint;

        public FlightModel2D Model => model;

        public Rigidbody2D Body => body;

        public bool InputSuppressed => inputSuppressed;

        public float ConfiguredMaximumSpeed => maximumSpeed;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                State = new FlightState(body.position, Vector2.zero, initialLane);
            }
        }

        public void BindInput(InputRouter input, SettingsService settings)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (IsBound)
            {
                if (ReferenceEquals(this.input, input) &&
                    ReferenceEquals(this.settings, settings))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "FlightMotor2D is already bound to another composition.");
            }

            if (body == null)
            {
                throw new InvalidOperationException(
                    "FlightMotor2D requires a Rigidbody2D presentation body.");
            }

            this.input = input;
            this.settings = settings;
            settings.SettingsChanged += OnSettingsChanged;
            RebuildModel(settings.Current.PilotingAssist);
            IsBound = true;
        }

        public void ReleaseInput(InputRouter input)
        {
            if (!IsBound)
            {
                return;
            }

            if (!ReferenceEquals(this.input, input))
            {
                throw new InvalidOperationException(
                    "FlightMotor2D can only release its owning InputRouter.");
            }

            settings.SettingsChanged -= OnSettingsChanged;
            this.input = null;
            settings = null;
            model = null;
            inputSuppressed = false;
            laneInputHeld = false;
            failureElapsed = 0f;
            IsBound = false;
        }

        public void CaptureCheckpoint(FlightCheckpoint checkpoint)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            var candidate = checkpoint.Capture(State);
            if (!latestCheckpoint.HasValue ||
                candidate.Order >= latestCheckpoint.Value.Order)
            {
                latestCheckpoint = candidate;
            }
        }

        public void SetStateForTests(FlightState state)
        {
            State = state;
            ApplyPresentation();
        }

        public bool RecoverLatestCheckpoint()
        {
            if (!latestCheckpoint.HasValue)
            {
                return false;
            }

            var checkpoint = latestCheckpoint.Value.State;
            State = new FlightState(
                checkpoint.Position,
                checkpoint.Velocity,
                checkpoint.Lane,
                failurePending: false,
                landingLocked: false,
                checkpoint.ElapsedSeconds,
                checkpoint.LaneTransitionTarget,
                checkpoint.LaneTransitionProgress);
            failureElapsed = 0f;
            laneInputHeld = false;
            ApplyPresentation();
            return true;
        }

        public void TriggerSoftFailure()
        {
            State = new FlightState(
                State.Position,
                State.Velocity,
                State.Lane,
                failurePending: true,
                State.LandingLocked,
                State.ElapsedSeconds,
                State.LaneTransitionTarget,
                State.LaneTransitionProgress);
            failureElapsed = 0f;
        }

        public void SetInputSuppressed(bool suppressed)
        {
            inputSuppressed = suppressed;
            if (suppressed)
            {
                laneInputHeld = false;
            }
        }

        public bool TryLockForLanding(out FlightState rollbackState)
        {
            rollbackState = State;
            if (State.FailurePending || State.LandingLocked)
            {
                return false;
            }

            State = new FlightState(
                State.Position,
                Vector2.zero,
                State.Lane,
                failurePending: false,
                landingLocked: true,
                State.ElapsedSeconds,
                State.LaneTransitionTarget,
                State.LaneTransitionProgress);
            laneInputHeld = false;
            ApplyPresentation();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            return true;
        }

        public void RestoreAfterLandingFailure(FlightState rollbackState)
        {
            State = rollbackState;
            laneInputHeld = false;
            ApplyPresentation();
        }

        private void FixedUpdate()
        {
            if (!IsBound || model == null)
            {
                return;
            }

            if (State.FailurePending)
            {
                failureElapsed += Time.fixedDeltaTime;
                if (failureElapsed >= model.Config.FailureRecoverySeconds)
                {
                    RecoverLatestCheckpoint();
                }

                return;
            }

            if (State.LandingLocked)
            {
                body.linearVelocity = Vector2.zero;
                presentation?.SetMotion(0f, settings.Current.ReducedMotion);
                return;
            }

            var frame = inputSuppressed
                ? new FlightInputFrame(Vector2.zero, false, false)
                : ReadInputFrame();
            State = model.Step(State, frame, Time.fixedDeltaTime);
            ApplyPresentation();
            presentation?.SetMotion(
                frame.PrimaryHeld ? 1f : State.Velocity.magnitude /
                    Mathf.Max(0.01f, model.Config.MaximumSpeed),
                settings.Current.ReducedMotion);
            predictionArc?.Present(
                model.Predict(State, frame, Time.fixedDeltaTime, predictionSteps),
                settings.Current.ReducedMotion);
        }

        private FlightInputFrame ReadInputFrame()
        {
            var lookY = input.ReadLook().y;
            var laneDelta = 0;
            if (!laneInputHeld && Mathf.Abs(lookY) >= 0.55f)
            {
                laneDelta = Math.Sign(lookY);
                laneInputHeld = true;
            }
            else if (Mathf.Abs(lookY) < 0.25f)
            {
                laneInputHeld = false;
            }

            return new FlightInputFrame(
                input.ReadMove() * settings.Current.TouchSensitivity,
                input.IsCommandPressed(SemanticGameplayCommand.Primary),
                input.IsCommandPressed(SemanticGameplayCommand.Secondary),
                laneDelta);
        }

        private void RebuildModel(AssistLevel assist)
        {
            var transitions = depthLanes
                .Where(candidate => candidate != null)
                .SelectMany(source => source.DeclaredDestinations.Select(
                    destination => new FlightLaneTransition(
                        source.LaneIndex,
                        destination,
                        0.35f)))
                .ToArray();
            var gravity = gravityAssists
                .Where(candidate => candidate != null)
                .Select(candidate => candidate.Sample)
                .ToArray();
            var envelope = Rect.MinMaxRect(
                routeMinimum.x,
                routeMinimum.y,
                routeMaximum.x,
                routeMaximum.y);
            var defaults = FlightSimulationConfig.Default;
            model = new FlightModel2D(
                new FlightSimulationConfig(
                    envelope,
                    defaults.Acceleration,
                    defaults.BoostMultiplier,
                    defaults.PassiveDrag,
                    defaults.BrakeDeceleration,
                    defaults.SteeringAcceleration,
                    defaults.DriftSteeringMultiplier,
                    maximumSpeed,
                    defaults.FailureRecoverySeconds),
                assist,
                transitions,
                gravity);
        }

        private void OnSettingsChanged(GameSettings current)
        {
            RebuildModel(current.PilotingAssist);
        }

        private void ApplyPresentation()
        {
            if (body == null)
            {
                return;
            }

            body.position = State.Position;
            body.linearVelocity = State.Velocity;
            var lane = depthLanes.FirstOrDefault(candidate =>
                candidate != null && candidate.LaneIndex == State.Lane);
            if (lane != null)
            {
                transform.localScale = Vector3.one * lane.PresentationScale;
                foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
                {
                    renderer.sortingOrder = lane.SortingOrder +
                        renderer.transform.GetSiblingIndex();
                }
            }
        }

        private void OnDestroy()
        {
            if (IsBound)
            {
                ReleaseInput(input);
            }
        }
    }
}
