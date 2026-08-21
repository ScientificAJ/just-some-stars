using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JustSomeStars.Editor.Build
{
    public sealed class BuildConfiguration
    {
        public const int MinimumAndroidVersionCode = 1;
        public const int MaximumAndroidVersionCode = 2_100_000_000;
        public const string PrimaryAndroidPackageId = "com.scientificaj.justsomestars";
        public const string GalaxyAndroidPackageId =
            "com.scientificaj.justsomestars.galaxy";

        internal const string DevelopmentSymbol = "JSS_DEVELOPMENT";
        internal const string GooglePlaySymbol = "JSS_GOOGLE_PLAY";
        internal const string GalaxySymbol = "JSS_GALAXY";

        private static readonly HashSet<string> VariantSymbols = new HashSet<string>(
            new[]
            {
                DevelopmentSymbol,
                GooglePlaySymbol,
                GalaxySymbol,
            },
            StringComparer.Ordinal);

        private BuildConfiguration(
            BuildTargetKind kind,
            string packageId,
            string variantSymbol,
            string outputPath,
            bool buildAppBundle,
            bool isDevelopmentBuild,
            bool allowDebugging,
            bool useCustomKeystore,
            int versionCode,
            IReadOnlyList<string> defineSymbols)
        {
            Kind = kind;
            PackageId = packageId;
            VariantSymbol = variantSymbol;
            OutputPath = outputPath;
            BuildAppBundle = buildAppBundle;
            IsDevelopmentBuild = isDevelopmentBuild;
            AllowDebugging = allowDebugging;
            UseCustomKeystore = useCustomKeystore;
            VersionCode = versionCode;
            DefineSymbols = defineSymbols;
        }

        public BuildTargetKind Kind { get; }

        public string PackageId { get; }

        public string VariantSymbol { get; }

        public string OutputPath { get; }

        public bool BuildAppBundle { get; }

        public bool IsDevelopmentBuild { get; }

        public bool AllowDebugging { get; }

        public bool UseCustomKeystore { get; }

        public int VersionCode { get; }

        public IReadOnlyList<string> DefineSymbols { get; }

        public static BuildConfiguration Resolve(
            BuildTargetKind kind,
            int buildNumber)
        {
            var definition = BuildTargetDefinition.Resolve(kind);
            ValidateVersionCode(buildNumber);

            var defineSymbols = new ReadOnlyCollection<string>(
                new[] { definition.VariantSymbol });
            ValidateDefineSymbols(defineSymbols);

            return new BuildConfiguration(
                definition.Kind,
                definition.PackageId,
                definition.VariantSymbol,
                definition.OutputPath,
                definition.BuildAppBundle,
                definition.IsDevelopmentBuild,
                definition.AllowDebugging,
                definition.UseCustomKeystore,
                buildNumber,
                defineSymbols);
        }

        public static void ValidateDefineSymbols(IEnumerable<string> defineSymbols)
        {
            if (defineSymbols == null)
            {
                throw new ArgumentNullException(nameof(defineSymbols));
            }

            var normalizedSymbols = new HashSet<string>(StringComparer.Ordinal);
            var selectedVariants = new HashSet<string>(StringComparer.Ordinal);
            foreach (var symbol in defineSymbols)
            {
                var normalizedSymbol = NormalizeSymbol(symbol);
                if (normalizedSymbol == null)
                {
                    throw new InvalidOperationException(
                        "Define symbols must be nonempty individual tokens.");
                }

                if (normalizedSymbol.IndexOf(';') >= 0)
                {
                    throw new InvalidOperationException(
                        "Define symbols must not contain semicolon-packed tokens.");
                }

                if (!normalizedSymbols.Add(normalizedSymbol))
                {
                    throw new InvalidOperationException(
                        "Define symbols must be unique after trimming.");
                }

                if (VariantSymbols.Contains(normalizedSymbol))
                {
                    selectedVariants.Add(normalizedSymbol);
                }
            }

            if (selectedVariants.Count != 1)
            {
                throw new InvalidOperationException(
                    "Exactly one JSS build variant symbol must be defined; found " +
                    selectedVariants.Count + ".");
            }
        }

        internal static bool IsVariantSymbol(string symbol)
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            return normalizedSymbol != null && VariantSymbols.Contains(normalizedSymbol);
        }

        private static string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return null;
            }

            return symbol.Trim();
        }

        private static void ValidateVersionCode(int buildNumber)
        {
            if (buildNumber < MinimumAndroidVersionCode ||
                buildNumber > MaximumAndroidVersionCode)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(buildNumber),
                    buildNumber,
                    "Android version code must be between 1 and 2100000000 inclusive.");
            }
        }
    }
}
