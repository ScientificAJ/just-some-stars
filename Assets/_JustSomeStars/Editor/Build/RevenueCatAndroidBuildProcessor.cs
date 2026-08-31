using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace JustSomeStars.Editor.Build
{
    internal sealed class RevenueCatAndroidBuildProcessor :
        IPostGenerateGradleAndroidProject
    {
        internal const string GalaxyPolicyMarker =
            "JSS_TASK24_GALAXY_SAMSUNG_IAP_ISOLATION";
        private const string GalaxyModuleName = "jssGalaxyBilling";
        private const string GalaxyProjectDependency =
            "    implementation project(':" + GalaxyModuleName + "')";
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
                "AndroidManifest.xml"),
                galaxy);
            var generatedRoot = Directory.GetParent(unityLibraryPath)?.FullName;
            if (string.IsNullOrEmpty(generatedRoot))
            {
                throw new BuildFailedException(
                    "Generated Android project root could not be resolved.");
            }

            PatchGradle(
                Path.Combine(unityLibraryPath, "build.gradle"),
                galaxy);
            PatchSettings(
                Path.Combine(generatedRoot, "settings.gradle"),
                galaxy);
            PatchGalaxyModule(generatedRoot, galaxy);
        }

        private static void PatchManifest(string manifestPath, bool galaxy)
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
            var root = document.Root ?? throw new BuildFailedException(
                "Generated Android manifest has no root element.");
            var permissions = root.Elements("uses-permission").ToArray();
            var googleBilling = permissions.Where(element => string.Equals(
                (string)element.Attribute(AndroidNamespace + "name"),
                "com.android.vending.BILLING",
                StringComparison.Ordinal)).ToArray();
            var samsungBilling = permissions.Where(element => string.Equals(
                (string)element.Attribute(AndroidNamespace + "name"),
                "com.samsung.android.iap.permission.BILLING",
                StringComparison.Ordinal)).ToArray();
            if (galaxy)
            {
                foreach (var permission in googleBilling)
                {
                    permission.Remove();
                }

                if (samsungBilling.Length == 0)
                {
                    root.AddFirst(new XElement(
                        "uses-permission",
                        new XAttribute(
                            AndroidNamespace + "name",
                            "com.samsung.android.iap.permission.BILLING")));
                }
                else if (samsungBilling.Length > 1)
                {
                    throw new BuildFailedException(
                        "Generated manifest contains duplicate Samsung IAP permissions.");
                }
            }
            else
            {
                foreach (var permission in samsungBilling)
                {
                    permission.Remove();
                }
            }
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

            var persistedPermissions = persisted
                .Root?
                .Elements("uses-permission")
                .Select(element =>
                    (string)element.Attribute(AndroidNamespace + "name"))
                .ToArray() ?? Array.Empty<string>();
            if (galaxy &&
                (persistedPermissions.Count(value => value ==
                    "com.samsung.android.iap.permission.BILLING") != 1 ||
                 persistedPermissions.Contains("com.android.vending.BILLING")))
            {
                throw new BuildFailedException(
                    "Galaxy billing permission isolation did not persist.");
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
                var lines = source
                    .Replace("\r\n", "\n")
                    .Split('\n')
                    .Where(line =>
                        line.IndexOf(
                            "purchases-hybrid-common",
                            StringComparison.Ordinal) < 0 &&
                        line.IndexOf(
                            "com.android.billingclient",
                            StringComparison.Ordinal) < 0)
                    .ToList();
                source = InsertIntoGradleBlock(
                    string.Join("\n", lines),
                    "dependencies",
                    GalaxyProjectDependency);
                source += "\n// " + GalaxyPolicyMarker;
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
                !persisted.Contains(GalaxyProjectDependency) ||
                persisted.Contains("com.revenuecat") ||
                persisted.Contains("purchases-hybrid-common") ||
                persisted.Contains("com.android.billingclient"))
            {
                throw new BuildFailedException(
                    "Galaxy Samsung/Google billing dependency isolation did not persist.");
            }
        }

        private static string InsertIntoGradleBlock(
            string source,
            string blockName,
            string line)
        {
            var name = source.IndexOf(blockName, StringComparison.Ordinal);
            while (name >= 0)
            {
                var before = name == 0 ? '\0' : source[name - 1];
                var afterName = name + blockName.Length;
                var after = afterName >= source.Length
                    ? '\0'
                    : source[afterName];
                if ((name == 0 || !IsGradleIdentifier(before)) &&
                    (afterName == source.Length || !IsGradleIdentifier(after)))
                {
                    var open = source.IndexOf('{', afterName);
                    if (open >= 0)
                    {
                        var between = source.Substring(
                            afterName,
                            open - afterName);
                        if (string.IsNullOrWhiteSpace(between))
                        {
                            var close = FindMatchingGradleBrace(source, open);
                            var prefix = source.Substring(0, close)
                                .TrimEnd('\r', '\n');
                            var suffix = source.Substring(close);
                            return prefix + "\n" + line + "\n" + suffix;
                        }
                    }
                }

                name = source.IndexOf(
                    blockName,
                    name + blockName.Length,
                    StringComparison.Ordinal);
            }

            throw new BuildFailedException(
                "Generated Gradle " + blockName + " block could not be located.");
        }

        private static int FindMatchingGradleBrace(string source, int open)
        {
            var depth = 0;
            var quote = '\0';
            var escaped = false;
            var lineComment = false;
            var blockComment = false;
            for (var index = open; index < source.Length; index++)
            {
                var value = source[index];
                var next = index + 1 < source.Length
                    ? source[index + 1]
                    : '\0';
                if (lineComment)
                {
                    if (value == '\n')
                    {
                        lineComment = false;
                    }
                    continue;
                }

                if (blockComment)
                {
                    if (value == '*' && next == '/')
                    {
                        blockComment = false;
                        index++;
                    }
                    continue;
                }

                if (quote != '\0')
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (value == '\\')
                    {
                        escaped = true;
                    }
                    else if (value == quote)
                    {
                        quote = '\0';
                    }
                    continue;
                }

                if (value == '/' && next == '/')
                {
                    lineComment = true;
                    index++;
                    continue;
                }
                if (value == '/' && next == '*')
                {
                    blockComment = true;
                    index++;
                    continue;
                }
                if (value == '\'' || value == '"')
                {
                    quote = value;
                    continue;
                }
                if (value == '{')
                {
                    depth++;
                }
                else if (value == '}' && --depth == 0)
                {
                    return index;
                }
            }

            throw new BuildFailedException(
                "Generated Gradle block has unbalanced braces.");
        }

        private static bool IsGradleIdentifier(char value) =>
            char.IsLetterOrDigit(value) || value == '_' || value == '-';

        private static void PatchSettings(string settingsPath, bool galaxy)
        {
            if (!File.Exists(settingsPath))
            {
                throw new BuildFailedException(
                    "Generated Android settings.gradle is missing.");
            }

            var source = File.ReadAllText(settingsPath);
            var declaration = "include ':" + GalaxyModuleName + "'";
            var count = CountOccurrences(source, declaration);
            if (galaxy && count == 0)
            {
                File.AppendAllText(
                    settingsPath,
                    "\n" + declaration + "\n",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            else if ((!galaxy && count != 0) || count > 1)
            {
                throw new BuildFailedException(
                    "Generated Android settings contain the wrong Galaxy module state.");
            }
        }

        private static void PatchGalaxyModule(string generatedRoot, bool galaxy)
        {
            var destination = Path.Combine(generatedRoot, GalaxyModuleName);
            if (!galaxy)
            {
                if (Directory.Exists(destination))
                {
                    throw new BuildFailedException(
                        "A Google build retained the Galaxy billing module.");
                }

                return;
            }

            var source = Path.Combine(
                Application.dataPath,
                "Plugins",
                "Android",
                "jss-galaxy-billing");
            if (!Directory.Exists(source))
            {
                throw new BuildFailedException(
                    "The canonical Galaxy billing module is missing.");
            }

            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            CopyDirectory(source, destination);
            if (!File.Exists(Path.Combine(destination, "build.gradle")))
            {
                throw new BuildFailedException(
                    "The Galaxy billing module did not stage completely.");
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(".meta", StringComparison.Ordinal))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destination, name), overwrite: true);
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(destination, Path.GetFileName(directory)));
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
