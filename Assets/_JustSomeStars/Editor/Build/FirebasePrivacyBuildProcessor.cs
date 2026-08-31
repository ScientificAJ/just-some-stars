using System;
using System.IO;
using System.Text;
using UnityEditor.Android;
using UnityEditor.Build;

namespace JustSomeStars.Editor.Build
{
    internal sealed class FirebasePrivacyBuildProcessor :
        IPostGenerateGradleAndroidProject
    {
        internal const string PolicyMarker = "JSS_TASK21_NO_FIREBASE_ANALYTICS";
        private const string AnalyticsExclusion =
            "\n// " + PolicyMarker + "\n" +
            "configurations.configureEach {\n" +
            "    exclude group: 'com.google.firebase', " +
            "module: 'firebase-analytics'\n" +
            "}\n";

        public int callbackOrder => -2100;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new BuildFailedException(
                    "Android Gradle project path is required.");
            }

            PatchUnityLibraryGradle(Path.Combine(basePath, "build.gradle"));
        }

        internal static void PatchUnityLibraryGradle(string buildGradlePath)
        {
            if (string.IsNullOrWhiteSpace(buildGradlePath) ||
                !File.Exists(buildGradlePath))
            {
                throw new BuildFailedException(
                    "Generated unityLibrary/build.gradle is missing; " +
                    "Firebase Analytics exclusion cannot be proven.");
            }

            var source = File.ReadAllText(buildGradlePath);
            var markerCount = CountOccurrences(source, PolicyMarker);
            if (markerCount > 1)
            {
                throw new BuildFailedException(
                    "Generated Gradle policy contains duplicate Task 21 markers.");
            }

            if (markerCount == 0)
            {
                if (!source.Contains("dependencies {"))
                {
                    throw new BuildFailedException(
                        "Generated unityLibrary/build.gradle has no dependencies " +
                        "block; refusing an unverified Firebase build.");
                }

                source += AnalyticsExclusion;
                File.WriteAllText(
                    buildGradlePath,
                    source,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            var persisted = File.ReadAllText(buildGradlePath);
            if (CountOccurrences(persisted, PolicyMarker) != 1 ||
                !persisted.Contains(
                    "exclude group: 'com.google.firebase', " +
                    "module: 'firebase-analytics'"))
            {
                throw new BuildFailedException(
                    "Firebase Analytics exclusion did not persist exactly once.");
            }
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(
                       value,
                       index,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
