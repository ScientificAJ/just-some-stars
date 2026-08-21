using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JustSomeStars.Editor.Build
{
    internal interface IPlayerBuilder
    {
        void Build(
            BuildConfiguration configuration,
            BuildPlayerOptions playerOptions);
    }

    internal sealed class UnityPlayerBuilder : IPlayerBuilder
    {
        public void Build(
            BuildConfiguration configuration,
            BuildPlayerOptions playerOptions)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var report = BuildPipeline.BuildPlayer(playerOptions);
            if (report != null)
            {
                var summary = report.summary;
                Debug.Log(
                    "[JSS Build] BuildReport: variant=" + configuration.Kind +
                    ", result=" + summary.result +
                    ", platform=" + summary.platform +
                    ", stagingOutput=" + Path.GetFileName(playerOptions.locationPathName) +
                    ", sizeBytes=" + summary.totalSize +
                    ", duration=" + summary.totalTime +
                    ", warnings=" + summary.totalWarnings +
                    ", errors=" + summary.totalErrors +
                    ", guid=" + summary.guid + ".");
            }

            BuildReportValidator.Validate(report);
        }
    }

    internal static class BuildReportValidator
    {
        public static void Validate(BuildReport report)
        {
            if (report == null)
            {
                throw new InvalidOperationException(
                    "BuildPipeline.BuildPlayer returned a null BuildReport.");
            }

            Validate(report.summary.result);
            if (report.summary.totalErrors > 0)
            {
                throw new InvalidOperationException(
                    "Unity reported a successful Android build with one or more errors.");
            }
        }

        public static void Validate(BuildResult result)
        {
            if (result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Android player build did not succeed. Result: " + result + ".");
            }
        }
    }

    internal static class BuildArtifactValidator
    {
        public static long Validate(string path, string expectedExtension)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("An artifact path is required.", nameof(path));
            }

            if (string.IsNullOrWhiteSpace(expectedExtension) ||
                expectedExtension[0] != '.')
            {
                throw new ArgumentException(
                    "An artifact extension beginning with '.' is required.",
                    nameof(expectedExtension));
            }

            if (!string.Equals(
                    Path.GetExtension(path),
                    expectedExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The staged Android artifact extension does not match the target output type.");
            }

            var artifact = new FileInfo(path);
            if (!artifact.Exists || artifact.Length <= 0)
            {
                throw new InvalidOperationException(
                    "Unity reported success but the staged Android artifact is missing or empty.");
            }

            return artifact.Length;
        }
    }
}
