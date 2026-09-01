using System;
using System.Linq;

namespace JustSomeStars.Runtime.Discovery
{
    public sealed class KoroSpectrumSample
    {
        public KoroSpectrumSample(
            string stableId,
            float[] wavelengths,
            float[] intensities,
            string unit)
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                string.IsNullOrWhiteSpace(unit) ||
                wavelengths == null || intensities == null ||
                wavelengths.Length < 3 || wavelengths.Length != intensities.Length ||
                wavelengths.Any(value => !IsFinite(value) || value <= 0f) ||
                intensities.Any(value => !IsFinite(value) || value < 0f) ||
                wavelengths.Zip(wavelengths.Skip(1), (first, second) =>
                    second > first).Any(increasing => !increasing))
            {
                throw new ArgumentException(
                    "A Koro spectrum requires ordered wavelengths and finite intensities.");
            }
            StableId = stableId.Trim();
            Wavelengths = wavelengths.ToArray();
            Intensities = intensities.ToArray();
            Unit = unit.Trim();
        }

        public string StableId { get; }
        public float[] Wavelengths { get; }
        public float[] Intensities { get; }
        public string Unit { get; }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct KoroSpectrumResult
    {
        public KoroSpectrumResult(
            bool waterRelated,
            bool repeatingDifference,
            string unit,
            float matchScore,
            string interpretation)
        {
            WaterRelatedSignaturePresent = waterRelated;
            RepeatingSignalDifferencePresent = repeatingDifference;
            Unit = unit;
            MatchScore = matchScore;
            Interpretation = interpretation;
        }

        public bool WaterRelatedSignaturePresent { get; }
        public bool RepeatingSignalDifferencePresent { get; }
        public string Unit { get; }
        public float MatchScore { get; }
        public string Interpretation { get; }
    }

    public static class KoroSpectrumComparison
    {
        public static KoroSpectrumResult Compare(
            KoroSpectrumSample natural,
            KoroSpectrumSample signal)
        {
            if (natural == null || signal == null)
            {
                throw new ArgumentNullException(
                    natural == null ? nameof(natural) : nameof(signal));
            }
            if (ReferenceEquals(natural, signal) ||
                string.Equals(natural.StableId, signal.StableId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Spectrum comparison requires two distinct plume observations.");
            }
            if (!string.Equals(natural.Unit, signal.Unit, StringComparison.Ordinal) ||
                !natural.Wavelengths.SequenceEqual(signal.Wavelengths))
            {
                throw new InvalidOperationException(
                    "Spectrum comparison requires the same wavelength basis and unit.");
            }

            var totalDifference = natural.Intensities
                .Zip(signal.Intensities, (first, second) => Math.Abs(first - second))
                .Sum();
            var totalScale = natural.Intensities
                .Zip(signal.Intensities, (first, second) => Math.Max(first, second))
                .Sum();
            var match = totalScale <= 0f
                ? 1f
                : Math.Clamp(1f - (totalDifference / totalScale), 0f, 1f);
            var waterRelated = natural.Intensities[0] > 0.2f &&
                signal.Intensities[0] > 0.2f;
            var repeatingDifference = totalDifference > 0.12f;
            const string interpretation =
                "The shared ultraviolet signature may indicate water-related " +
                "material. The repeating difference may track the Signal rhythm; " +
                "it does not prove life or a direct ocean source.";
            return new KoroSpectrumResult(
                waterRelated,
                repeatingDifference,
                natural.Unit,
                match,
                interpretation);
        }
    }
}
