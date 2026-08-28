using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Cosmetics;
using UnityEngine;

namespace JustSomeStars.Runtime.Rendering2D
{
    [Serializable]
    public sealed class MirraLayerAsset2D
    {
        [SerializeField] private LayerBand band;
        [SerializeField] private Sprite artwork;
        [SerializeField] private Texture2D normalMap;
        [SerializeField] private Texture2D emissionMask;
        [SerializeField] private string addressKey;

        public MirraLayerAsset2D(
            LayerBand layerBand,
            Sprite layerArtwork,
            Texture2D layerNormalMap,
            Texture2D layerEmissionMask,
            string stableAddressKey)
        {
            band = layerBand;
            artwork = layerArtwork;
            normalMap = layerNormalMap;
            emissionMask = layerEmissionMask;
            addressKey = stableAddressKey;
        }

        public LayerBand Band => band;
        public Sprite Artwork => artwork;
        public Texture2D NormalMap => normalMap;
        public Texture2D EmissionMask => emissionMask;
        public string AddressKey => addressKey;
    }

    public sealed class Mirra2DProofValidation
    {
        public Mirra2DProofValidation(IEnumerable<string> errors)
        {
            Errors = errors.ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    [CreateAssetMenu(
        fileName = "Mirra2DProof",
        menuName = "Just Some Stars/Rendering 2D/Mirra Proof Definition")]
    public sealed class Mirra2DProofDefinition : ScriptableObject
    {
        private static readonly LayerBand[] RequiredArtworkBands =
        {
            LayerBand.Sky,
            LayerBand.FarWorld,
            LayerBand.Atmosphere,
            LayerBand.Midground,
            LayerBand.Gameplay,
            LayerBand.Foreground,
        };

        [SerializeField] private MirraLayerAsset2D[] bands =
            Array.Empty<MirraLayerAsset2D>();
        [SerializeField] private CaptainSpriteSet captainSpriteSet;
        [SerializeField] private CharacterSpriteSet companionSpriteSet;
        [SerializeField] private CharacterSpriteSet oriSpriteSet;
        [SerializeField] private UnityEngine.Object[] characterModelReferences =
            Array.Empty<UnityEngine.Object>();
        [SerializeField] private string interactionId;
        [SerializeField] private string lensTargetId;
        [SerializeField] private Bounds cameraBounds;
        [SerializeField] private Vector2 recoveryAnchor;
        [SerializeField] private float recoveryThreshold;

        public IReadOnlyList<MirraLayerAsset2D> Bands => bands;
        public CaptainSpriteSet CaptainSpriteSet => captainSpriteSet;
        public CharacterSpriteSet CompanionSpriteSet => companionSpriteSet;
        public CharacterSpriteSet OriSpriteSet => oriSpriteSet;
        public IReadOnlyList<UnityEngine.Object> CharacterModelReferences =>
            characterModelReferences;
        public string InteractionId => interactionId;
        public string LensTargetId => lensTargetId;
        public Bounds CameraBounds => cameraBounds;
        public Vector2 RecoveryAnchor => recoveryAnchor;
        public float RecoveryThreshold => recoveryThreshold;

        public void Configure(
            MirraLayerAsset2D[] layerAssets,
            CaptainSpriteSet captain,
            CharacterSpriteSet companion,
            CharacterSpriteSet ori,
            string interactionStableId,
            string lensStableId,
            Bounds authoredCameraBounds,
            Vector2 safeRecoveryAnchor,
            float fallThreshold)
        {
            bands = layerAssets == null
                ? Array.Empty<MirraLayerAsset2D>()
                : (MirraLayerAsset2D[])layerAssets.Clone();
            captainSpriteSet = captain;
            companionSpriteSet = companion;
            oriSpriteSet = ori;
            characterModelReferences = Array.Empty<UnityEngine.Object>();
            interactionId = interactionStableId;
            lensTargetId = lensStableId;
            cameraBounds = authoredCameraBounds;
            recoveryAnchor = safeRecoveryAnchor;
            recoveryThreshold = fallThreshold;
        }

        public Mirra2DProofValidation Validate()
        {
            var errors = new List<string>();
            var validBands = bands?.Where(item => item != null).ToArray() ??
                Array.Empty<MirraLayerAsset2D>();
            foreach (LayerBand required in Enum.GetValues(typeof(LayerBand)))
            {
                if (validBands.Count(item => item.Band == required) != 1)
                {
                    errors.Add($"Mirra requires exactly one {required} band.");
                }
            }
            foreach (var required in RequiredArtworkBands)
            {
                var binding = validBands.SingleOrDefault(item => item.Band == required);
                if (binding?.Artwork == null)
                {
                    errors.Add($"Mirra {required} is missing final artwork.");
                }
            }
            if (validBands.Any(item => string.IsNullOrWhiteSpace(item.AddressKey)))
            {
                errors.Add("Every Mirra band requires a stable address key.");
            }
            if (validBands.Select(item => item.AddressKey)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).Count() != validBands.Length)
            {
                errors.Add("Mirra band address keys must be unique.");
            }
            var gameplay = validBands.SingleOrDefault(
                item => item.Band == LayerBand.Gameplay);
            if (gameplay?.NormalMap == null || gameplay.EmissionMask == null)
            {
                errors.Add("Mirra gameplay needs normal and emission textures.");
            }
            if (captainSpriteSet == null || companionSpriteSet == null ||
                oriSpriteSet == null)
            {
                errors.Add("Mirra requires Captain, companion and Ori sprite sets.");
            }
            if (characterModelReferences == null ||
                characterModelReferences.Any(item => item != null))
            {
                errors.Add("Mirra cannot depend on 3D character models.");
            }
            if (string.IsNullOrWhiteSpace(interactionId) ||
                string.IsNullOrWhiteSpace(lensTargetId))
            {
                errors.Add("Mirra requires stable interaction and Lens target ids.");
            }
            if (cameraBounds.size.x <= 0f || cameraBounds.size.y <= 0f)
            {
                errors.Add("Mirra requires non-empty camera bounds.");
            }
            if (recoveryThreshold >= recoveryAnchor.y)
            {
                errors.Add("Mirra recovery threshold must be below its safe anchor.");
            }
            return new Mirra2DProofValidation(errors);
        }
    }
}
