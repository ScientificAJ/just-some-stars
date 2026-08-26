using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Rendering2D
{
    [Serializable]
    public sealed class LayerBinding2D
    {
        [SerializeField] private LayerBand band;
        [SerializeField] private int minimumSortingOrder;
        [SerializeField] private int maximumSortingOrder;
        [SerializeField, Range(0f, 1f)] private float parallaxFactor;
        [SerializeField] private Bounds authoredBounds;
        [SerializeField] private int collisionMask;
        [SerializeField] private uint lightingMask;
        [SerializeField] private string addressablesGroup;
        [SerializeField] private string addressKey;

        public LayerBinding2D(
            LayerBand band,
            int minimumSortingOrder,
            int maximumSortingOrder,
            float parallaxFactor,
            Bounds authoredBounds,
            int collisionMask,
            uint lightingMask,
            string addressablesGroup,
            string addressKey)
        {
            this.band = band;
            this.minimumSortingOrder = minimumSortingOrder;
            this.maximumSortingOrder = maximumSortingOrder;
            this.parallaxFactor = parallaxFactor;
            this.authoredBounds = authoredBounds;
            this.collisionMask = collisionMask;
            this.lightingMask = lightingMask;
            this.addressablesGroup = addressablesGroup;
            this.addressKey = addressKey;
        }

        public LayerBand Band => band;
        public int MinimumSortingOrder => minimumSortingOrder;
        public int MaximumSortingOrder => maximumSortingOrder;
        public float ParallaxFactor => parallaxFactor;
        public Bounds AuthoredBounds => authoredBounds;
        public int CollisionMask => collisionMask;
        public uint LightingMask => lightingMask;
        public string AddressablesGroup => addressablesGroup;
        public string AddressKey => addressKey;
    }

    public sealed class LayeredSceneValidation
    {
        public LayeredSceneValidation(
            IEnumerable<LayerBand> missingBands,
            IEnumerable<LayerBand> duplicateBands,
            IEnumerable<string> errors)
        {
            MissingBands = missingBands.ToArray();
            DuplicateBands = duplicateBands.ToArray();
            Errors = errors.ToArray();
        }

        public IReadOnlyList<LayerBand> MissingBands { get; }
        public IReadOnlyList<LayerBand> DuplicateBands { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => MissingBands.Count == 0 &&
            DuplicateBands.Count == 0 &&
            Errors.Count == 0;
    }

    [DisallowMultipleComponent]
    public sealed class LayeredSceneDefinition : MonoBehaviour
    {
        [SerializeField] private LayerBinding2D[] bindings =
            Array.Empty<LayerBinding2D>();

        public IReadOnlyList<LayerBinding2D> Bindings => bindings;

        public void Configure(LayerBinding2D[] layerBindings)
        {
            bindings = layerBindings == null
                ? Array.Empty<LayerBinding2D>()
                : layerBindings.ToArray();
        }

        public LayeredSceneValidation Validate()
        {
            var validBindings = bindings.Where(binding => binding != null).ToArray();
            var groups = validBindings
                .GroupBy(binding => binding.Band)
                .ToDictionary(group => group.Key, group => group.Count());
            var missing = Enum.GetValues(typeof(LayerBand))
                .Cast<LayerBand>()
                .Where(band => !groups.ContainsKey(band));
            var duplicate = groups
                .Where(entry => entry.Value > 1)
                .Select(entry => entry.Key);
            var errors = new List<string>();

            if (bindings.Any(binding => binding == null))
            {
                errors.Add("Layer bindings cannot contain null entries.");
            }

            foreach (var binding in validBindings)
            {
                if (binding.MinimumSortingOrder > binding.MaximumSortingOrder)
                {
                    errors.Add($"{binding.Band} has an inverted sorting range.");
                }

                if (binding.ParallaxFactor < 0f || binding.ParallaxFactor > 1f)
                {
                    errors.Add($"{binding.Band} has an invalid parallax factor.");
                }

                if (binding.AuthoredBounds.size.x <= 0f ||
                    binding.AuthoredBounds.size.y <= 0f)
                {
                    errors.Add($"{binding.Band} has empty authored bounds.");
                }

                if (string.IsNullOrWhiteSpace(binding.AddressKey))
                {
                    errors.Add($"{binding.Band} is missing its address key.");
                }

                if (binding.LightingMask == 0u)
                {
                    errors.Add($"{binding.Band} is missing its lighting mask.");
                }

                if (string.IsNullOrWhiteSpace(binding.AddressablesGroup))
                {
                    errors.Add(
                        $"{binding.Band} is missing its Addressables group.");
                }
            }

            var sorted = validBindings
                .OrderBy(binding => binding.MinimumSortingOrder)
                .ThenBy(binding => binding.MaximumSortingOrder)
                .ToArray();
            for (var index = 1; index < sorted.Length; index++)
            {
                if (sorted[index].MinimumSortingOrder <=
                    sorted[index - 1].MaximumSortingOrder)
                {
                    errors.Add(
                        $"Layer sorting ranges overlap: " +
                        $"{sorted[index - 1].Band} and {sorted[index].Band}.");
                }
            }

            return new LayeredSceneValidation(missing, duplicate, errors);
        }
    }
}
