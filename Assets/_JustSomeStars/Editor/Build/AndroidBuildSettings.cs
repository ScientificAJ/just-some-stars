using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace JustSomeStars.Editor.Build
{
    internal interface IAndroidBuildSettings
    {
        string ApplicationIdentifier { get; set; }

        int VersionCode { get; set; }

        bool BuildAppBundle { get; set; }

        bool UseCustomKeystore { get; set; }

        string KeystoreName { get; set; }

        string KeystorePassword { get; set; }

        string KeyAlias { get; set; }

        string KeyAliasPassword { get; set; }
    }

    internal sealed class AndroidBuildSettingsScope
    {
        private readonly IAndroidBuildSettings m_Settings;
        private readonly string m_ApplicationIdentifier;
        private readonly int m_VersionCode;
        private readonly bool m_BuildAppBundle;
        private bool m_Restored;

        private AndroidBuildSettingsScope(IAndroidBuildSettings settings)
        {
            m_Settings = settings;
            m_ApplicationIdentifier = settings.ApplicationIdentifier;
            m_VersionCode = settings.VersionCode;
            m_BuildAppBundle = settings.BuildAppBundle;
        }

        public static AndroidBuildSettingsScope Capture(IAndroidBuildSettings settings)
        {
            return new AndroidBuildSettingsScope(
                settings ?? throw new ArgumentNullException(nameof(settings)));
        }

        public void Apply(BuildConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            m_Settings.ApplicationIdentifier = configuration.PackageId;
            m_Settings.VersionCode = configuration.VersionCode;
            m_Settings.BuildAppBundle = configuration.BuildAppBundle;
        }

        public void RestoreAndVerify()
        {
            if (m_Restored)
            {
                return;
            }

            var failures = new List<Exception>();
            TryRestore(
                () => m_Settings.ApplicationIdentifier = m_ApplicationIdentifier,
                nameof(IAndroidBuildSettings.ApplicationIdentifier),
                failures);
            TryRestore(
                () => m_Settings.VersionCode = m_VersionCode,
                nameof(IAndroidBuildSettings.VersionCode),
                failures);
            TryRestore(
                () => m_Settings.BuildAppBundle = m_BuildAppBundle,
                nameof(IAndroidBuildSettings.BuildAppBundle),
                failures);
            TryVerify(
                () => string.Equals(
                    m_Settings.ApplicationIdentifier,
                    m_ApplicationIdentifier,
                    StringComparison.Ordinal),
                nameof(IAndroidBuildSettings.ApplicationIdentifier),
                failures);
            TryVerify(
                () => m_Settings.VersionCode == m_VersionCode,
                nameof(IAndroidBuildSettings.VersionCode),
                failures);
            TryVerify(
                () => m_Settings.BuildAppBundle == m_BuildAppBundle,
                nameof(IAndroidBuildSettings.BuildAppBundle),
                failures);

            if (failures.Count > 0)
            {
                throw new AggregateException(
                    "Temporary Android player settings could not be restored " +
                    "and verified.",
                    failures);
            }

            m_Restored = true;
        }

        private static void TryRestore(
            Action restore,
            string fieldName,
            ICollection<Exception> failures)
        {
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(
                    "Android player setting " + fieldName +
                    " could not be restored.",
                    exception));
            }
        }

        private static void TryVerify(
            Func<bool> verify,
            string fieldName,
            ICollection<Exception> failures)
        {
            try
            {
                if (!verify())
                {
                    failures.Add(new InvalidOperationException(
                        "Android player setting " + fieldName +
                        " did not match its captured value after restoration."));
                }
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(
                    "Android player setting " + fieldName +
                    " could not be verified after restoration.",
                    exception));
            }
        }
    }

    internal sealed class AndroidSigningScope
    {
        private readonly IAndroidBuildSettings m_Settings;
        private readonly bool m_UseCustomKeystore;
        private readonly string m_KeystoreName;
        private readonly string m_KeystorePassword;
        private readonly string m_KeyAlias;
        private readonly string m_KeyAliasPassword;
        private bool m_Restored;

        private AndroidSigningScope(IAndroidBuildSettings settings)
        {
            m_Settings = settings;
            m_UseCustomKeystore = settings.UseCustomKeystore;
            m_KeystoreName = settings.KeystoreName;
            m_KeystorePassword = settings.KeystorePassword;
            m_KeyAlias = settings.KeyAlias;
            m_KeyAliasPassword = settings.KeyAliasPassword;
        }

        public static AndroidSigningScope Capture(IAndroidBuildSettings settings)
        {
            return new AndroidSigningScope(
                settings ?? throw new ArgumentNullException(nameof(settings)));
        }

        public void ApplyRelease(ReleaseSigningCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            try
            {
                m_Settings.UseCustomKeystore = true;
                m_Settings.KeystoreName = credentials.KeystorePath;
                m_Settings.KeystorePassword = credentials.KeystorePassword;
                m_Settings.KeyAlias = credentials.KeyAlias;
                m_Settings.KeyAliasPassword = credentials.KeyAliasPassword;
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    "Temporary Android release signing settings could not be applied " +
                    "(credential values redacted).");
            }
        }

        public void ApplyDebug()
        {
            m_Settings.UseCustomKeystore = false;
            m_Settings.KeystoreName = string.Empty;
            m_Settings.KeystorePassword = string.Empty;
            m_Settings.KeyAlias = string.Empty;
            m_Settings.KeyAliasPassword = string.Empty;
        }

        public void RestoreAndVerify()
        {
            if (m_Restored)
            {
                return;
            }

            var failures = new List<Exception>();
            TryRestoreRedacted(
                () => m_Settings.UseCustomKeystore = m_UseCustomKeystore,
                nameof(IAndroidBuildSettings.UseCustomKeystore),
                failures);
            TryRestoreRedacted(
                () => m_Settings.KeystoreName = m_KeystoreName,
                nameof(IAndroidBuildSettings.KeystoreName),
                failures);
            TryRestoreRedacted(
                () => m_Settings.KeystorePassword = m_KeystorePassword,
                nameof(IAndroidBuildSettings.KeystorePassword),
                failures);
            TryRestoreRedacted(
                () => m_Settings.KeyAlias = m_KeyAlias,
                nameof(IAndroidBuildSettings.KeyAlias),
                failures);
            TryRestoreRedacted(
                () => m_Settings.KeyAliasPassword = m_KeyAliasPassword,
                nameof(IAndroidBuildSettings.KeyAliasPassword),
                failures);
            TryVerifyRedacted(
                () => m_Settings.UseCustomKeystore == m_UseCustomKeystore,
                nameof(IAndroidBuildSettings.UseCustomKeystore),
                failures);
            TryVerifyRedacted(
                () => string.Equals(
                    m_Settings.KeystoreName,
                    m_KeystoreName,
                    StringComparison.Ordinal),
                nameof(IAndroidBuildSettings.KeystoreName),
                failures);
            TryVerifyRedacted(
                () => string.Equals(
                    m_Settings.KeystorePassword,
                    m_KeystorePassword,
                    StringComparison.Ordinal),
                nameof(IAndroidBuildSettings.KeystorePassword),
                failures);
            TryVerifyRedacted(
                () => string.Equals(
                    m_Settings.KeyAlias,
                    m_KeyAlias,
                    StringComparison.Ordinal),
                nameof(IAndroidBuildSettings.KeyAlias),
                failures);
            TryVerifyRedacted(
                () => string.Equals(
                    m_Settings.KeyAliasPassword,
                    m_KeyAliasPassword,
                    StringComparison.Ordinal),
                nameof(IAndroidBuildSettings.KeyAliasPassword),
                failures);

            if (failures.Count > 0)
            {
                throw new AggregateException(
                    "Temporary Android signing settings could not be restored and " +
                    "verified (credential values redacted).",
                    failures);
            }

            m_Restored = true;
        }

        private static void TryRestoreRedacted(
            Action restore,
            string fieldName,
            ICollection<Exception> failures)
        {
            try
            {
                restore();
            }
            catch (Exception)
            {
                failures.Add(new InvalidOperationException(
                    "Android signing field " + fieldName +
                    " could not be restored (credential values redacted)."));
            }
        }

        private static void TryVerifyRedacted(
            Func<bool> verify,
            string fieldName,
            ICollection<Exception> failures)
        {
            try
            {
                if (!verify())
                {
                    failures.Add(new InvalidOperationException(
                        "Android signing field " + fieldName +
                        " did not match its captured value after restoration " +
                        "(credential values redacted)."));
                }
            }
            catch (Exception)
            {
                failures.Add(new InvalidOperationException(
                    "Android signing field " + fieldName +
                    " could not be verified after restoration " +
                    "(credential values redacted)."));
            }
        }
    }

    internal interface IAndroidBuildStateFactory
    {
        IAndroidBuildState Capture();
    }

    internal interface IAndroidBuildState
    {
        IReadOnlyList<string> PersistentDefineSymbols { get; }

        void ApplySettings(BuildConfiguration configuration);

        void ApplySigning(
            BuildConfiguration configuration,
            ReleaseSigningCredentials credentials);

        void RestoreSigningAndVerify();

        void RestoreSettingsAndVerify();
    }

    internal sealed class UnityAndroidBuildStateFactory : IAndroidBuildStateFactory
    {
        public IAndroidBuildState Capture()
        {
            var settings = new UnityAndroidBuildSettings();
            PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.Android,
                out var persistentDefineSymbols);
            return new UnityAndroidBuildState(
                AndroidBuildSettingsScope.Capture(settings),
                AndroidSigningScope.Capture(settings),
                persistentDefineSymbols);
        }
    }

    internal sealed class UnityAndroidBuildState : IAndroidBuildState
    {
        private readonly AndroidBuildSettingsScope m_BuildSettingsScope;
        private readonly AndroidSigningScope m_SigningScope;

        public UnityAndroidBuildState(
            AndroidBuildSettingsScope buildSettingsScope,
            AndroidSigningScope signingScope,
            IEnumerable<string> persistentDefineSymbols)
        {
            m_BuildSettingsScope = buildSettingsScope ??
                throw new ArgumentNullException(nameof(buildSettingsScope));
            m_SigningScope = signingScope ??
                throw new ArgumentNullException(nameof(signingScope));
            PersistentDefineSymbols = (persistentDefineSymbols ?? Array.Empty<string>())
                .ToArray();
        }

        public IReadOnlyList<string> PersistentDefineSymbols { get; }

        public void ApplySettings(BuildConfiguration configuration)
        {
            m_BuildSettingsScope.Apply(configuration);
        }

        public void ApplySigning(
            BuildConfiguration configuration,
            ReleaseSigningCredentials credentials)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.UseCustomKeystore)
            {
                m_SigningScope.ApplyRelease(credentials ??
                    throw new InvalidOperationException(
                        "Release signing credentials are required for this build variant."));
            }
            else
            {
                m_SigningScope.ApplyDebug();
            }
        }

        public void RestoreSigningAndVerify()
        {
            m_SigningScope.RestoreAndVerify();
        }

        public void RestoreSettingsAndVerify()
        {
            m_BuildSettingsScope.RestoreAndVerify();
        }
    }

    internal sealed class UnityAndroidBuildSettings : IAndroidBuildSettings
    {
        public string ApplicationIdentifier
        {
            get => PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            set => PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, value);
        }

        public int VersionCode
        {
            get => PlayerSettings.Android.bundleVersionCode;
            set => PlayerSettings.Android.bundleVersionCode = value;
        }

        public bool BuildAppBundle
        {
            get => EditorUserBuildSettings.buildAppBundle;
            set => EditorUserBuildSettings.buildAppBundle = value;
        }

        public bool UseCustomKeystore
        {
            get => PlayerSettings.Android.useCustomKeystore;
            set => PlayerSettings.Android.useCustomKeystore = value;
        }

        public string KeystoreName
        {
            get => PlayerSettings.Android.keystoreName;
            set => PlayerSettings.Android.keystoreName = value;
        }

        public string KeystorePassword
        {
            get => PlayerSettings.Android.keystorePass;
            set => PlayerSettings.Android.keystorePass = value;
        }

        public string KeyAlias
        {
            get => PlayerSettings.Android.keyaliasName;
            set => PlayerSettings.Android.keyaliasName = value;
        }

        public string KeyAliasPassword
        {
            get => PlayerSettings.Android.keyaliasPass;
            set => PlayerSettings.Android.keyaliasPass = value;
        }
    }
}
