using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Accessibility
{
    public enum AssistLevel
    {
        Guided = 0,
        Balanced = 1,
        Ace = 2,
    }

    public enum ScienceDepth
    {
        Guided = 0,
        Balanced = 1,
        Deep = 2,
    }

    public enum ColorVisionMode
    {
        Standard = 0,
        Protanopia = 1,
        Deuteranopia = 2,
        Tritanopia = 3,
    }

    public enum PresentationQuality
    {
        Performance = 0,
        Balanced = 1,
        Cinematic = 2,
        HighFrameRate = 3,
    }

    [Serializable]
    public sealed class GameSettings : IEquatable<GameSettings>
    {
        public const int CurrentSchemaVersion = 1;

        private static readonly string[] RequiredJsonFields =
        {
            "schemaVersion",
            "pilotingAssist",
            "explorationAssist",
            "scienceDepth",
            "captionsEnabled",
            "textScale",
            "dyslexiaFriendlyFontEnabled",
            "dialogueSpeed",
            "colorVisionMode",
            "reducedCameraShake",
            "reducedFlashing",
            "reducedMotion",
            "motionBlurEnabled",
            "particleDensity",
            "presentationQuality",
            "musicVolume",
            "dialogueVolume",
            "effectsVolume",
            "hapticsEnabled",
            "leftHandedControls",
            "touchSensitivity",
        };

        [SerializeField] private int schemaVersion;
        [SerializeField] private AssistLevel pilotingAssist;
        [SerializeField] private AssistLevel explorationAssist;
        [SerializeField] private ScienceDepth scienceDepth;
        [SerializeField] private bool captionsEnabled;
        [SerializeField] private float textScale;
        [SerializeField] private bool dyslexiaFriendlyFontEnabled;
        [SerializeField] private float dialogueSpeed;
        [SerializeField] private ColorVisionMode colorVisionMode;
        [SerializeField] private bool reducedCameraShake;
        [SerializeField] private bool reducedFlashing;
        [SerializeField] private bool reducedMotion;
        [SerializeField] private bool motionBlurEnabled;
        [SerializeField] private float particleDensity;
        [SerializeField] private PresentationQuality presentationQuality;
        [SerializeField] private float musicVolume;
        [SerializeField] private float dialogueVolume;
        [SerializeField] private float effectsVolume;
        [SerializeField] private bool hapticsEnabled;
        [SerializeField] private bool leftHandedControls;
        [SerializeField] private float touchSensitivity;

        public int SchemaVersion => schemaVersion;

        public AssistLevel PilotingAssist
        {
            get => pilotingAssist;
            set => pilotingAssist = value;
        }

        public AssistLevel ExplorationAssist
        {
            get => explorationAssist;
            set => explorationAssist = value;
        }

        public ScienceDepth ScienceDepth
        {
            get => scienceDepth;
            set => scienceDepth = value;
        }

        public bool CaptionsEnabled
        {
            get => captionsEnabled;
            set => captionsEnabled = value;
        }

        public float TextScale
        {
            get => textScale;
            set => textScale = value;
        }

        public bool DyslexiaFriendlyFontEnabled
        {
            get => dyslexiaFriendlyFontEnabled;
            set => dyslexiaFriendlyFontEnabled = value;
        }

        public float DialogueSpeed
        {
            get => dialogueSpeed;
            set => dialogueSpeed = value;
        }

        public ColorVisionMode ColorVisionMode
        {
            get => colorVisionMode;
            set => colorVisionMode = value;
        }

        public bool ReducedCameraShake
        {
            get => reducedCameraShake;
            set => reducedCameraShake = value;
        }

        public bool ReducedFlashing
        {
            get => reducedFlashing;
            set => reducedFlashing = value;
        }

        public bool ReducedMotion
        {
            get => reducedMotion;
            set => reducedMotion = value;
        }

        public bool MotionBlurEnabled
        {
            get => motionBlurEnabled;
            set => motionBlurEnabled = value;
        }

        public float ParticleDensity
        {
            get => particleDensity;
            set => particleDensity = value;
        }

        public PresentationQuality PresentationQuality
        {
            get => presentationQuality;
            set => presentationQuality = value;
        }

        public float MusicVolume
        {
            get => musicVolume;
            set => musicVolume = value;
        }

        public float DialogueVolume
        {
            get => dialogueVolume;
            set => dialogueVolume = value;
        }

        public float EffectsVolume
        {
            get => effectsVolume;
            set => effectsVolume = value;
        }

        public bool HapticsEnabled
        {
            get => hapticsEnabled;
            set => hapticsEnabled = value;
        }

        public bool LeftHandedControls
        {
            get => leftHandedControls;
            set => leftHandedControls = value;
        }

        public float TouchSensitivity
        {
            get => touchSensitivity;
            set => touchSensitivity = value;
        }

        public static GameSettings CreateDefaults()
        {
            return new GameSettings
            {
                schemaVersion = CurrentSchemaVersion,
                pilotingAssist = AssistLevel.Balanced,
                explorationAssist = AssistLevel.Balanced,
                scienceDepth = global::JustSomeStars.Runtime.Accessibility.ScienceDepth.Balanced,
                captionsEnabled = true,
                textScale = 1f,
                dyslexiaFriendlyFontEnabled = false,
                dialogueSpeed = 1f,
                colorVisionMode = global::JustSomeStars.Runtime.Accessibility.ColorVisionMode.Standard,
                reducedCameraShake = false,
                reducedFlashing = false,
                reducedMotion = false,
                motionBlurEnabled = false,
                particleDensity = 1f,
                presentationQuality = global::JustSomeStars.Runtime.Accessibility.PresentationQuality.Balanced,
                musicVolume = 0.8f,
                dialogueVolume = 1f,
                effectsVolume = 0.9f,
                hapticsEnabled = true,
                leftHandedControls = false,
                touchSensitivity = 1f,
            };
        }

        public GameSettings Copy()
        {
            return (GameSettings)MemberwiseClone();
        }

        public bool Equals(GameSettings other)
        {
            return other != null &&
                schemaVersion == other.schemaVersion &&
                pilotingAssist == other.pilotingAssist &&
                explorationAssist == other.explorationAssist &&
                scienceDepth == other.scienceDepth &&
                captionsEnabled == other.captionsEnabled &&
                textScale.Equals(other.textScale) &&
                dyslexiaFriendlyFontEnabled == other.dyslexiaFriendlyFontEnabled &&
                dialogueSpeed.Equals(other.dialogueSpeed) &&
                colorVisionMode == other.colorVisionMode &&
                reducedCameraShake == other.reducedCameraShake &&
                reducedFlashing == other.reducedFlashing &&
                reducedMotion == other.reducedMotion &&
                motionBlurEnabled == other.motionBlurEnabled &&
                particleDensity.Equals(other.particleDensity) &&
                presentationQuality == other.presentationQuality &&
                musicVolume.Equals(other.musicVolume) &&
                dialogueVolume.Equals(other.dialogueVolume) &&
                effectsVolume.Equals(other.effectsVolume) &&
                hapticsEnabled == other.hapticsEnabled &&
                leftHandedControls == other.leftHandedControls &&
                touchSensitivity.Equals(other.touchSensitivity);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GameSettings);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = schemaVersion;
                hash = (hash * 397) ^ pilotingAssist.GetHashCode();
                hash = (hash * 397) ^ explorationAssist.GetHashCode();
                hash = (hash * 397) ^ scienceDepth.GetHashCode();
                hash = (hash * 397) ^ captionsEnabled.GetHashCode();
                hash = (hash * 397) ^ textScale.GetHashCode();
                hash = (hash * 397) ^ dyslexiaFriendlyFontEnabled.GetHashCode();
                hash = (hash * 397) ^ dialogueSpeed.GetHashCode();
                hash = (hash * 397) ^ colorVisionMode.GetHashCode();
                hash = (hash * 397) ^ reducedCameraShake.GetHashCode();
                hash = (hash * 397) ^ reducedFlashing.GetHashCode();
                hash = (hash * 397) ^ reducedMotion.GetHashCode();
                hash = (hash * 397) ^ motionBlurEnabled.GetHashCode();
                hash = (hash * 397) ^ particleDensity.GetHashCode();
                hash = (hash * 397) ^ presentationQuality.GetHashCode();
                hash = (hash * 397) ^ musicVolume.GetHashCode();
                hash = (hash * 397) ^ dialogueVolume.GetHashCode();
                hash = (hash * 397) ^ effectsVolume.GetHashCode();
                hash = (hash * 397) ^ hapticsEnabled.GetHashCode();
                hash = (hash * 397) ^ leftHandedControls.GetHashCode();
                return (hash * 397) ^ touchSensitivity.GetHashCode();
            }
        }

        internal string ToJson()
        {
            return JsonUtility.ToJson(this, prettyPrint: true);
        }

        internal static bool TryFromJson(string document, out GameSettings settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(document) || !ContainsAllFields(document))
            {
                return false;
            }

            try
            {
                var parsed = JsonUtility.FromJson<GameSettings>(document);
                if (parsed == null || !parsed.IsValid())
                {
                    return false;
                }

                settings = parsed;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal void ThrowIfInvalid(string parameterName)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentException(
                    $"Settings schema must be version {CurrentSchemaVersion}.",
                    parameterName);
            }

            RequireEnum(pilotingAssist, nameof(PilotingAssist), parameterName);
            RequireEnum(explorationAssist, nameof(ExplorationAssist), parameterName);
            RequireEnum(scienceDepth, nameof(ScienceDepth), parameterName);
            RequireRange(textScale, 0.85f, 1.35f, nameof(TextScale), parameterName);
            RequireRange(dialogueSpeed, 0.5f, 2f, nameof(DialogueSpeed), parameterName);
            RequireEnum(colorVisionMode, nameof(ColorVisionMode), parameterName);
            RequireRange(particleDensity, 0f, 1f, nameof(ParticleDensity), parameterName);
            RequireEnum(
                presentationQuality,
                nameof(PresentationQuality),
                parameterName);
            RequireRange(musicVolume, 0f, 1f, nameof(MusicVolume), parameterName);
            RequireRange(dialogueVolume, 0f, 1f, nameof(DialogueVolume), parameterName);
            RequireRange(effectsVolume, 0f, 1f, nameof(EffectsVolume), parameterName);
            RequireRange(touchSensitivity, 0.5f, 2f, nameof(TouchSensitivity), parameterName);
        }

        private bool IsValid()
        {
            try
            {
                ThrowIfInvalid(nameof(GameSettings));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool ContainsAllFields(string document)
        {
            foreach (var field in RequiredJsonFields)
            {
                if (document.IndexOf($"\"{field}\"", StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireEnum<T>(
            T value,
            string fieldName,
            string parameterName)
            where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"{fieldName} is not a supported value.");
            }
        }

        private static void RequireRange(
            float value,
            float minimum,
            float maximum,
            string fieldName,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) ||
                value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"{fieldName} must be finite and within {minimum}..{maximum}.");
            }
        }
    }
}
