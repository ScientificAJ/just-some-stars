using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace JustSomeStars.Editor.Build
{
    internal interface IBuildInputReader
    {
        BuildInputs Read(BuildTargetKind kind);
    }

    internal sealed class BuildInputs
    {
        public BuildInputs(
            int buildNumber,
            ReleaseSigningCredentials signingCredentials)
        {
            BuildNumber = buildNumber;
            SigningCredentials = signingCredentials;
        }

        public int BuildNumber { get; }

        public ReleaseSigningCredentials SigningCredentials { get; }
    }

    internal sealed class ReleaseSigningCredentials
    {
        public ReleaseSigningCredentials(
            string keystorePath,
            string keystorePassword,
            string keyAlias,
            string keyAliasPassword)
        {
            KeystorePath = keystorePath;
            KeystorePassword = keystorePassword;
            KeyAlias = keyAlias;
            KeyAliasPassword = keyAliasPassword;
        }

        public string KeystorePath { get; }

        public string KeystorePassword { get; }

        public string KeyAlias { get; }

        public string KeyAliasPassword { get; }

        internal IReadOnlyList<string> SensitiveValues => new[]
        {
            KeystorePassword,
            KeyAliasPassword,
        };

        public override string ToString()
        {
            return "Release signing credentials (values redacted)";
        }
    }

    internal static class BuildEnvironment
    {
        public const string BuildNumberVariable = "JSS_BUILD_NUMBER";
        public const int MinimumSigningPasswordLength = 12;

        private const int DefaultInternalBuildNumber = 1;

        private static readonly string[] GooglePlaySigningVariables =
        {
            "JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PATH",
            "JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PASSWORD",
            "JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS",
            "JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS_PASSWORD",
        };

        private static readonly string[] GalaxySigningVariables =
        {
            "JSS_GALAXY_ANDROID_KEYSTORE_PATH",
            "JSS_GALAXY_ANDROID_KEYSTORE_PASSWORD",
            "JSS_GALAXY_ANDROID_KEY_ALIAS",
            "JSS_GALAXY_ANDROID_KEY_ALIAS_PASSWORD",
        };

        public static int ReadBuildNumber(
            BuildTargetKind kind,
            Func<string, string> readVariable)
        {
            if (readVariable == null)
            {
                throw new ArgumentNullException(nameof(readVariable));
            }

            var rawValue = readVariable(BuildNumberVariable);
            if (rawValue == null)
            {
                if (kind == BuildTargetKind.AndroidInternal)
                {
                    return DefaultInternalBuildNumber;
                }

                if (kind != BuildTargetKind.GooglePlay && kind != BuildTargetKind.Galaxy)
                {
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
                }

                throw new InvalidOperationException(
                    BuildNumberVariable + " is required for release builds.");
            }

            if (!int.TryParse(
                    rawValue,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var buildNumber) ||
                buildNumber < BuildConfiguration.MinimumAndroidVersionCode ||
                buildNumber > BuildConfiguration.MaximumAndroidVersionCode)
            {
                throw new InvalidOperationException(
                    BuildNumberVariable +
                    " must be an unsigned base-10 Android version code from 1 through " +
                    BuildConfiguration.MaximumAndroidVersionCode + ".");
            }

            return buildNumber;
        }

        public static ReleaseSigningCredentials ReadSigning(
            BuildTargetKind kind,
            Func<string, string> readVariable,
            Func<string, bool> fileExists,
            Func<string, string> resolveFullPath)
        {
            if (kind == BuildTargetKind.AndroidInternal)
            {
                return null;
            }

            if (readVariable == null)
            {
                throw new ArgumentNullException(nameof(readVariable));
            }

            if (fileExists == null)
            {
                throw new ArgumentNullException(nameof(fileExists));
            }

            if (resolveFullPath == null)
            {
                throw new ArgumentNullException(nameof(resolveFullPath));
            }

            string[] selectedVariables;
            string[] forbiddenVariables;
            switch (kind)
            {
                case BuildTargetKind.GooglePlay:
                    selectedVariables = GooglePlaySigningVariables;
                    forbiddenVariables = GalaxySigningVariables;
                    break;
                case BuildTargetKind.Galaxy:
                    selectedVariables = GalaxySigningVariables;
                    forbiddenVariables = GooglePlaySigningVariables;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            var populatedForbiddenVariables = forbiddenVariables
                .Where(name => !string.IsNullOrWhiteSpace(readVariable(name)))
                .ToArray();
            if (populatedForbiddenVariables.Length > 0)
            {
                throw new InvalidOperationException(
                    "Signing variables for the other Android store must be unset: " +
                    string.Join(", ", populatedForbiddenVariables) + ".");
            }

            var values = selectedVariables.ToDictionary(
                name => name,
                readVariable,
                StringComparer.Ordinal);
            var missingVariables = selectedVariables
                .Where(name => string.IsNullOrWhiteSpace(values[name]))
                .ToArray();
            if (missingVariables.Length > 0)
            {
                throw new InvalidOperationException(
                    "Release signing is incomplete. Missing environment variables: " +
                    string.Join(", ", missingVariables) + ".");
            }

            var shortPasswordVariables = new[]
                {
                    selectedVariables[1],
                    selectedVariables[3],
                }
                .Where(name =>
                    values[name].Length < MinimumSigningPasswordLength)
                .ToArray();
            if (shortPasswordVariables.Length > 0)
            {
                throw new InvalidOperationException(
                    "Release signing passwords must contain at least " +
                    MinimumSigningPasswordLength +
                    " characters. Invalid environment variables: " +
                    string.Join(", ", shortPasswordVariables) + ".");
            }

            string keystorePath;
            try
            {
                keystorePath = resolveFullPath(values[selectedVariables[0]]);
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    selectedVariables[0] + " is not a valid keystore path.");
            }

            if (string.IsNullOrWhiteSpace(keystorePath) || !fileExists(keystorePath))
            {
                throw new InvalidOperationException(
                    selectedVariables[0] +
                    " does not reference an existing keystore file.");
            }

            return new ReleaseSigningCredentials(
                keystorePath,
                values[selectedVariables[1]],
                values[selectedVariables[2]],
                values[selectedVariables[3]]);
        }
    }

    internal sealed class SystemBuildInputReader : IBuildInputReader
    {
        private readonly string m_ProjectRoot;

        public SystemBuildInputReader(string projectRoot)
        {
            m_ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        }

        public BuildInputs Read(BuildTargetKind kind)
        {
            var buildNumber = BuildEnvironment.ReadBuildNumber(
                kind,
                Environment.GetEnvironmentVariable);
            var signing = BuildEnvironment.ReadSigning(
                kind,
                Environment.GetEnvironmentVariable,
                File.Exists,
                ResolvePath);
            return new BuildInputs(buildNumber, signing);
        }

        private string ResolvePath(string path)
        {
            return Path.GetFullPath(
                Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(m_ProjectRoot, path));
        }
    }
}
