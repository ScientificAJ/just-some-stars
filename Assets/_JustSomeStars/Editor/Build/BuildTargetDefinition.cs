using System;

namespace JustSomeStars.Editor.Build
{
    internal sealed class BuildTargetDefinition
    {
        private BuildTargetDefinition(
            BuildTargetKind kind,
            string packageId,
            string variantSymbol,
            string outputPath,
            bool buildAppBundle,
            bool isDevelopmentBuild,
            bool allowDebugging,
            bool useCustomKeystore)
        {
            Kind = kind;
            PackageId = packageId;
            VariantSymbol = variantSymbol;
            OutputPath = outputPath;
            BuildAppBundle = buildAppBundle;
            IsDevelopmentBuild = isDevelopmentBuild;
            AllowDebugging = allowDebugging;
            UseCustomKeystore = useCustomKeystore;
        }

        public BuildTargetKind Kind { get; }

        public string PackageId { get; }

        public string VariantSymbol { get; }

        public string OutputPath { get; }

        public bool BuildAppBundle { get; }

        public bool IsDevelopmentBuild { get; }

        public bool AllowDebugging { get; }

        public bool UseCustomKeystore { get; }

        public static BuildTargetDefinition Resolve(BuildTargetKind kind)
        {
            switch (kind)
            {
                case BuildTargetKind.AndroidInternal:
                    return new BuildTargetDefinition(
                        kind,
                        BuildConfiguration.PrimaryAndroidPackageId,
                        BuildConfiguration.DevelopmentSymbol,
                        "Builds/AndroidInternal/JustSomeStars-internal.apk",
                        buildAppBundle: false,
                        isDevelopmentBuild: true,
                        allowDebugging: true,
                        useCustomKeystore: false);
                case BuildTargetKind.GooglePlay:
                    return new BuildTargetDefinition(
                        kind,
                        BuildConfiguration.PrimaryAndroidPackageId,
                        BuildConfiguration.GooglePlaySymbol,
                        "Builds/GooglePlay/JustSomeStars-google-play.aab",
                        buildAppBundle: true,
                        isDevelopmentBuild: false,
                        allowDebugging: false,
                        useCustomKeystore: true);
                case BuildTargetKind.Galaxy:
                    return new BuildTargetDefinition(
                        kind,
                        BuildConfiguration.GalaxyAndroidPackageId,
                        BuildConfiguration.GalaxySymbol,
                        "Builds/Galaxy/JustSomeStars-galaxy.aab",
                        buildAppBundle: true,
                        isDevelopmentBuild: false,
                        allowDebugging: false,
                        useCustomKeystore: true);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Unknown Just Some Stars build target kind.");
            }
        }
    }
}
