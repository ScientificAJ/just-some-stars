using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;

namespace JustSomeStars.Editor.Build
{
    internal sealed class RevenueCatAndroidBuildProcessor :
        IPostGenerateGradleAndroidProject
    {
        internal const string GalaxyPolicyMarker =
            "JSS_TASK23_GALAXY_REVENUECAT_EXCLUSION";
        private const string GalaxyExclusion =
            "\n// " + GalaxyPolicyMarker + "\n" +
            "configurations.configureEach {\n" +
            "    exclude group: 'com.revenuecat.purchases', " +
            "module: 'purchases-hybrid-common'\n" +
            "    exclude group: 'com.android.billingclient'\n" +
            "}\n";
        private static readonly XNamespace AndroidNamespace =
            "http://schemas.android.com/apk/res/android";

        public int callbackOrder => -2000;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new BuildFailedException(
                    "Android unityLibrary path is required.");
            }

            var applicationIdentifier = PlayerSettings.GetApplicationIdentifier(
                NamedBuildTarget.Android);
            PatchGeneratedAndroidProject(
                basePath,
                ResolveGalaxyVariant(applicationIdentifier));
        }

        internal static bool ResolveGalaxyVariant(string applicationIdentifier)
        {
            if (string.Equals(
                    applicationIdentifier,
                    BuildConfiguration.GalaxyAndroidPackageId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(
                    applicationIdentifier,
                    BuildConfiguration.PrimaryAndroidPackageId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            throw new BuildFailedException(
                "The generated Android application ID does not identify a " +
                "supported JSS store variant: " +
                (applicationIdentifier ?? "<null>") + ".");
        }

        internal static void PatchGeneratedAndroidProject(
            string unityLibraryPath,
            bool galaxy)
        {
            if (string.IsNullOrWhiteSpace(unityLibraryPath))
            {
                throw new BuildFailedException(
                    "Generated unityLibrary path is required.");
            }

            PatchManifest(Path.Combine(
                unityLibraryPath,
                "src",
                "main",
                "AndroidManifest.xml"));
            PatchGradle(Path.Combine(unityLibraryPath, "build.gradle"), galaxy);
        }

        private static void PatchManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new BuildFailedException(
                    "Generated unityLibrary AndroidManifest.xml is missing.");
            }

            XDocument document;
            try
            {
                document = XDocument.Load(
                    manifestPath,
                    LoadOptions.PreserveWhitespace);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is XmlException)
            {
                throw new BuildFailedException(
                    "Generated Android manifest could not be parsed: " +
                    exception.Message);
            }

            var activities = document
                .Descendants("activity")
                .Where(element => string.Equals(
                    (string)element.Attribute(AndroidNamespace + "name"),
                    "com.unity3d.player.UnityPlayerGameActivity",
                    StringComparison.Ordinal))
                .ToArray();
            if (activities.Length != 1)
            {
                throw new BuildFailedException(
                    "Generated Android manifest must contain exactly one " +
                    "UnityPlayerGameActivity.");
            }

            activities[0].SetAttributeValue(
                AndroidNamespace + "launchMode",
                "singleTop");
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                Indent = true,
            };
            using (var writer = XmlWriter.Create(manifestPath, settings))
            {
                document.Save(writer);
            }

            var persisted = XDocument.Load(manifestPath);
            var launchModes = persisted
                .Descendants("activity")
                .Where(element => string.Equals(
                    (string)element.Attribute(AndroidNamespace + "name"),
                    "com.unity3d.player.UnityPlayerGameActivity",
                    StringComparison.Ordinal))
                .Select(element =>
                    (string)element.Attribute(AndroidNamespace + "launchMode"))
                .ToArray();
            if (launchModes.Length != 1 || launchModes[0] != "singleTop")
            {
                throw new BuildFailedException(
                    "Safe singleTop launch mode did not persist.");
            }
        }

        private static void PatchGradle(string gradlePath, bool galaxy)
        {
            if (!File.Exists(gradlePath))
            {
                throw new BuildFailedException(
                    "Generated unityLibrary/build.gradle is missing.");
            }

            var source = File.ReadAllText(gradlePath);
            var markerCount = CountOccurrences(source, GalaxyPolicyMarker);
            if (markerCount > 1)
            {
                throw new BuildFailedException(
                    "Generated Gradle file contains duplicate Task 23 markers.");
            }

            if (galaxy && markerCount == 0)
            {
                source += GalaxyExclusion;
                File.WriteAllText(
                    gradlePath,
                    source,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            else if (!galaxy && markerCount != 0)
            {
                throw new BuildFailedException(
                    "A non-Galaxy build retained Galaxy RevenueCat exclusions.");
            }

            if (!galaxy)
            {
                return;
            }

            var persisted = File.ReadAllText(gradlePath);
            if (CountOccurrences(persisted, GalaxyPolicyMarker) != 1 ||
                !persisted.Contains(
                    "exclude group: 'com.revenuecat.purchases', " +
                    "module: 'purchases-hybrid-common'") ||
                !persisted.Contains(
                    "exclude group: 'com.android.billingclient'"))
            {
                throw new BuildFailedException(
                    "Galaxy RevenueCat/billing exclusions did not persist.");
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
