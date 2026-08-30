using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Player;
using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.Discovery
{
    public readonly struct MirraClimateSample
    {
        public MirraClimateSample(
            ContentId zoneId,
            float temperatureCelsius,
            Vector2 windAcceleration)
        {
            ZoneId = zoneId;
            TemperatureCelsius = temperatureCelsius;
            WindAcceleration = windAcceleration;
        }

        public ContentId ZoneId { get; }
        public float TemperatureCelsius { get; }
        public Vector2 WindAcceleration { get; }
    }

    [Serializable]
    public struct MirraClimateZone
    {
        [SerializeField] private string stableId;
        [SerializeField] private float minimumX;
        [SerializeField] private float maximumX;
        [SerializeField] private float temperatureCelsius;
        [SerializeField] private Vector2 windAcceleration;

        public MirraClimateZone(
            string id,
            float minimum,
            float maximum,
            float temperature,
            Vector2 wind)
        {
            stableId = id;
            minimumX = minimum;
            maximumX = maximum;
            temperatureCelsius = temperature;
            windAcceleration = wind;
            ValidateOrThrow();
        }

        public ContentId StableId => new ContentId(stableId);
        public float MinimumX => minimumX;
        public float MaximumX => maximumX;
        public float TemperatureCelsius => temperatureCelsius;
        public Vector2 WindAcceleration => windAcceleration;

        public bool Contains(float x) => x >= minimumX && x <= maximumX;

        public void ValidateOrThrow()
        {
            _ = StableId;
            if (!IsFinite(minimumX) || !IsFinite(maximumX) ||
                !IsFinite(temperatureCelsius) || !IsFinite(windAcceleration.x) ||
                !IsFinite(windAcceleration.y) || maximumX <= minimumX)
            {
                throw new InvalidOperationException(
                    $"Climate zone '{stableId}' has invalid authored measurements.");
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [DisallowMultipleComponent]
    public sealed class MirraClimateField : MonoBehaviour
    {
        [SerializeField] private SurfaceMotor2D motor;
        [SerializeField] private Rigidbody2D targetBody;
        [SerializeField] private TMP_Text readout;
        [SerializeField] private string twilightMilestoneId = "route.mirra.twilight";
        [SerializeField] private float twilightMinimumX = -1.4f;
        [SerializeField] private float twilightMaximumX = 1.4f;
        [SerializeField] private MirraClimateZone[] zones = Array.Empty<MirraClimateZone>();

        private readonly HashSet<ContentId> m_ObservedZones = new();
        private GameEventBus m_Events;
        private SettingsService m_Settings;
        private bool m_TwilightObserved;
        private bool m_ObservationEnabled;

        public IReadOnlyCollection<ContentId> ObservedZones => m_ObservedZones;
        public MirraClimateSample CurrentSample { get; private set; }
        public bool IsBound => m_Events != null;

        public void Bind(GameEventBus gameEvents, SettingsService settings)
        {
            if (gameEvents == null || settings == null)
            {
                throw new ArgumentNullException(
                    gameEvents == null ? nameof(gameEvents) : nameof(settings));
            }

            ValidateOrThrow();
            if (m_Events != null && !ReferenceEquals(m_Events, gameEvents))
            {
                throw new InvalidOperationException(
                    "Mirra climate field cannot change event-bus ownership.");
            }

            m_Events = gameEvents;
            m_Settings = settings;
            ApplyCurrentSample();
        }

        public void BeginObservation()
        {
            if (m_Events == null)
            {
                throw new InvalidOperationException(
                    "Mirra climate observation requires composition ownership.");
            }

            m_ObservationEnabled = true;
            EvaluateNow();
        }

        public void Release(GameEventBus gameEvents)
        {
            if (m_Events == null)
            {
                return;
            }

            if (!ReferenceEquals(m_Events, gameEvents))
            {
                throw new InvalidOperationException(
                    "Mirra climate field can only release its owning event bus.");
            }

            motor.SetExternalAcceleration(Vector2.zero);
            m_Events = null;
            m_Settings = null;
            m_ObservedZones.Clear();
            m_TwilightObserved = false;
            m_ObservationEnabled = false;
        }

        public MirraClimateSample Sample(Vector2 worldPosition)
        {
            ValidateOrThrow();
            var zone = zones[0];
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in zones)
            {
                if (candidate.Contains(worldPosition.x))
                {
                    zone = candidate;
                    bestDistance = 0f;
                    break;
                }

                var distance = worldPosition.x < candidate.MinimumX
                    ? candidate.MinimumX - worldPosition.x
                    : worldPosition.x - candidate.MaximumX;
                if (distance < bestDistance)
                {
                    zone = candidate;
                    bestDistance = distance;
                }
            }

            return new MirraClimateSample(
                zone.StableId,
                zone.TemperatureCelsius,
                zone.WindAcceleration);
        }

        public void EvaluateNow()
        {
            if (targetBody == null)
            {
                throw new InvalidOperationException(
                    "MirraClimateField requires the real surface body.");
            }

            ApplyCurrentSample();
            if (m_Events == null || !m_ObservationEnabled)
            {
                return;
            }

            var insideTwilight = targetBody.position.x >= twilightMinimumX &&
                targetBody.position.x <= twilightMaximumX;
            if (!m_TwilightObserved && insideTwilight)
            {
                m_TwilightObserved = true;
                m_Events.Publish(new TraversalMilestoneReached(
                    new ContentId(twilightMilestoneId)));
            }

            if (m_TwilightObserved &&
                !insideTwilight &&
                m_ObservedZones.Add(CurrentSample.ZoneId))
            {
                m_Events.Publish(new ClimateSampleObserved(
                    CurrentSample.ZoneId,
                    CurrentSample.TemperatureCelsius,
                    CurrentSample.WindAcceleration));
            }
        }

        private void ApplyCurrentSample()
        {
            CurrentSample = Sample(targetBody.position);
            var assistanceScale = m_Settings?.Current.ExplorationAssist switch
            {
                AssistLevel.Guided => 0.75f,
                AssistLevel.Balanced => 1f,
                AssistLevel.Ace => 1.1f,
                _ => 1f,
            };
            motor.SetExternalAcceleration(
                CurrentSample.WindAcceleration * assistanceScale);
            Present(CurrentSample);
        }

        public void ValidateOrThrow()
        {
            if (motor == null || targetBody == null || zones == null || zones.Length < 3)
            {
                throw new InvalidOperationException(
                    "MirraClimateField requires its motor, body, and hot/twilight/cold zones.");
            }

            _ = new ContentId(twilightMilestoneId);
            if (twilightMaximumX <= twilightMinimumX)
            {
                throw new InvalidOperationException(
                    "Mirra twilight interval must have positive width.");
            }

            var ids = new HashSet<ContentId>();
            foreach (var zone in zones)
            {
                zone.ValidateOrThrow();
                if (!ids.Add(zone.StableId))
                {
                    throw new InvalidOperationException(
                        $"Mirra repeats climate zone '{zone.StableId}'.");
                }
            }
        }

        private void FixedUpdate()
        {
            if (m_Events != null)
            {
                EvaluateNow();
            }
        }

        private void Present(MirraClimateSample sample)
        {
            if (readout == null)
            {
                return;
            }

            var direction = sample.WindAcceleration.x >= 0f ? "→" : "←";
            readout.text =
                $"{sample.ZoneId.Value.Replace("climate.mirra.", string.Empty).ToUpperInvariant()} " +
                $"// {sample.TemperatureCelsius:+0;-0;0}°C // WIND {direction}";
        }
    }
}
