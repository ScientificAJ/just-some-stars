using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace JustSomeStars.Editor.Build
{
    internal static class BuildPlayerOptionsFactory
    {
        public static BuildPlayerOptions Create(
            BuildConfiguration configuration,
            string stagingOutputPath,
            IEnumerable<string> scenePaths,
            IEnumerable<string> persistentDefineSymbols)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (string.IsNullOrWhiteSpace(stagingOutputPath))
            {
                throw new ArgumentException(
                    "A staging output path is required.",
                    nameof(stagingOutputPath));
            }

            if (scenePaths == null)
            {
                throw new ArgumentNullException(nameof(scenePaths));
            }

            ValidatePersistentDefineSymbols(persistentDefineSymbols);
            BuildConfiguration.ValidateDefineSymbols(configuration.DefineSymbols);

            var options = BuildOptions.DetailedBuildReport;
            if (configuration.IsDevelopmentBuild)
            {
                options |= BuildOptions.Development;
            }

            if (configuration.AllowDebugging)
            {
                options |= BuildOptions.AllowDebugging;
            }

            return new BuildPlayerOptions
            {
                scenes = scenePaths.ToArray(),
                locationPathName = stagingOutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options,
                extraScriptingDefines = configuration.DefineSymbols.ToArray(),
            };
        }

        internal static void ValidatePersistentDefineSymbols(
            IEnumerable<string> persistentDefineSymbols)
        {
            if (persistentDefineSymbols == null)
            {
                return;
            }

            var persistentVariants = persistentDefineSymbols
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .SelectMany(symbol => symbol.Split(';'))
                .Select(symbol => symbol.Trim())
                .Where(BuildConfiguration.IsVariantSymbol)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (persistentVariants.Length > 0)
            {
                throw new InvalidOperationException(
                    "JSS build variant symbols must be invocation-local rather than " +
                    "persisted in Android PlayerSettings. Remove: " +
                    string.Join(", ", persistentVariants) + ".");
            }
        }
    }
}
