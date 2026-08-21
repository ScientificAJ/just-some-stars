using System;
using System.Collections.Generic;
using JustSomeStars.Editor.Build;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class AndroidBuildSettingsScopeTests
    {
        private const string FakeSecret = "JSS_TEST_SENTINEL_NOT_A_REAL_SECRET";

        [Test]
        public void BuildSettingsScope_RestoresCapturedState()
        {
            var settings = CreateSettings();
            var original = settings.Copy();
            var scope = AndroidBuildSettingsScope.Capture(settings);

            scope.Apply(BuildConfiguration.Resolve(BuildTargetKind.Galaxy, 99));

            Assert.That(settings.ApplicationIdentifier,
                Is.EqualTo("com.scientificaj.justsomestars.galaxy"));
            Assert.That(settings.VersionCode, Is.EqualTo(99));
            Assert.That(settings.BuildAppBundle, Is.True);

            scope.RestoreAndVerify();

            Assert.That(settings, Is.EqualTo(original));
        }

        [Test]
        public void BuildSettingsScope_RestoreCannotBeVerified_Throws()
        {
            var settings = CreateSettings();
            var scope = AndroidBuildSettingsScope.Capture(settings);
            scope.Apply(BuildConfiguration.Resolve(BuildTargetKind.Galaxy, 99));
            settings.IgnoreApplicationIdentifierWrites = true;

            var exception = Assert.Throws<AggregateException>(() =>
                scope.RestoreAndVerify());

            Assert.That(exception.Message, Does.Contain("restore"));
        }

        [TestCase(nameof(IAndroidBuildSettings.ApplicationIdentifier))]
        [TestCase(nameof(IAndroidBuildSettings.VersionCode))]
        [TestCase(nameof(IAndroidBuildSettings.BuildAppBundle))]
        public void BuildSettingsScope_OneRestoreSetterThrows_AttemptsEveryField(
            string failingProperty)
        {
            var settings = CreateSettings();
            var original = settings.Copy();
            var scope = AndroidBuildSettingsScope.Capture(settings);
            scope.Apply(BuildConfiguration.Resolve(BuildTargetKind.Galaxy, 99));
            settings.ThrowOnWriteProperty = failingProperty;
            settings.AttemptedWrites.Clear();

            Assert.Throws<AggregateException>(() => scope.RestoreAndVerify());

            Assert.That(settings.AttemptedWrites, Is.EquivalentTo(new[]
            {
                nameof(IAndroidBuildSettings.ApplicationIdentifier),
                nameof(IAndroidBuildSettings.VersionCode),
                nameof(IAndroidBuildSettings.BuildAppBundle),
            }));
            if (failingProperty != nameof(IAndroidBuildSettings.ApplicationIdentifier))
            {
                Assert.That(settings.ApplicationIdentifier,
                    Is.EqualTo(original.ApplicationIdentifier));
            }

            if (failingProperty != nameof(IAndroidBuildSettings.VersionCode))
            {
                Assert.That(settings.VersionCode, Is.EqualTo(original.VersionCode));
            }

            if (failingProperty != nameof(IAndroidBuildSettings.BuildAppBundle))
            {
                Assert.That(settings.BuildAppBundle, Is.EqualTo(original.BuildAppBundle));
            }
        }

        [Test]
        public void SigningScope_ReleaseCredentialsAreTemporaryAndRestoredImmediately()
        {
            var settings = CreateSettings();
            var original = settings.Copy();
            var credentials = new ReleaseSigningCredentials(
                "/project/fake-release.jks",
                FakeSecret + "-store",
                "jss-test-alias",
                FakeSecret + "-alias");
            var scope = AndroidSigningScope.Capture(settings);

            scope.ApplyRelease(credentials);

            Assert.That(settings.UseCustomKeystore, Is.True);
            Assert.That(settings.KeystoreName, Is.EqualTo("/project/fake-release.jks"));
            Assert.That(settings.KeystorePassword, Is.EqualTo(FakeSecret + "-store"));
            Assert.That(settings.KeyAlias, Is.EqualTo("jss-test-alias"));
            Assert.That(settings.KeyAliasPassword, Is.EqualTo(FakeSecret + "-alias"));

            scope.RestoreAndVerify();

            Assert.That(settings, Is.EqualTo(original));
        }

        [Test]
        public void SigningScope_InternalBuildUsesOnlyDebugSigningDuringScope()
        {
            var settings = CreateSettings();
            var original = settings.Copy();
            var scope = AndroidSigningScope.Capture(settings);

            scope.ApplyDebug();

            Assert.That(settings.UseCustomKeystore, Is.False);
            Assert.That(settings.KeystoreName, Is.Empty);
            Assert.That(settings.KeystorePassword, Is.Empty);
            Assert.That(settings.KeyAlias, Is.Empty);
            Assert.That(settings.KeyAliasPassword, Is.Empty);

            scope.RestoreAndVerify();

            Assert.That(settings, Is.EqualTo(original));
        }

        [Test]
        public void SigningScope_RestoreCannotBeVerified_ThrowsWithoutCredentialValue()
        {
            var settings = CreateSettings();
            var credentials = new ReleaseSigningCredentials(
                "/project/fake-release.jks",
                FakeSecret + "-store",
                "jss-test-alias",
                FakeSecret + "-alias");
            var scope = AndroidSigningScope.Capture(settings);
            scope.ApplyRelease(credentials);
            settings.IgnoreSigningWrites = true;

            var exception = Assert.Throws<AggregateException>(() =>
                scope.RestoreAndVerify());

            Assert.That(exception.ToString(), Does.Contain("restore"));
            Assert.That(exception.ToString(), Does.Not.Contain(FakeSecret));
        }

        [TestCase(nameof(IAndroidBuildSettings.UseCustomKeystore))]
        [TestCase(nameof(IAndroidBuildSettings.KeystoreName))]
        [TestCase(nameof(IAndroidBuildSettings.KeystorePassword))]
        [TestCase(nameof(IAndroidBuildSettings.KeyAlias))]
        [TestCase(nameof(IAndroidBuildSettings.KeyAliasPassword))]
        public void SigningScope_OneRestoreSetterThrows_AttemptsEveryFieldWithoutSecret(
            string failingProperty)
        {
            var settings = CreateSettings();
            var original = settings.Copy();
            var credentials = new ReleaseSigningCredentials(
                "/project/fake-release.jks",
                FakeSecret + "-store",
                "jss-test-alias",
                FakeSecret + "-alias");
            var scope = AndroidSigningScope.Capture(settings);
            scope.ApplyRelease(credentials);
            settings.ThrowOnWriteProperty = failingProperty;
            settings.AttemptedWrites.Clear();

            var exception = Assert.Throws<AggregateException>(() =>
                scope.RestoreAndVerify());

            Assert.That(settings.AttemptedWrites, Is.EquivalentTo(new[]
            {
                nameof(IAndroidBuildSettings.UseCustomKeystore),
                nameof(IAndroidBuildSettings.KeystoreName),
                nameof(IAndroidBuildSettings.KeystorePassword),
                nameof(IAndroidBuildSettings.KeyAlias),
                nameof(IAndroidBuildSettings.KeyAliasPassword),
            }));
            Assert.That(exception.ToString(), Does.Not.Contain(FakeSecret));
            if (failingProperty != nameof(IAndroidBuildSettings.UseCustomKeystore))
            {
                Assert.That(settings.UseCustomKeystore,
                    Is.EqualTo(original.UseCustomKeystore));
            }

            if (failingProperty != nameof(IAndroidBuildSettings.KeystoreName))
            {
                Assert.That(settings.KeystoreName, Is.EqualTo(original.KeystoreName));
            }

            if (failingProperty != nameof(IAndroidBuildSettings.KeystorePassword))
            {
                Assert.That(settings.KeystorePassword,
                    Is.EqualTo(original.KeystorePassword));
            }

            if (failingProperty != nameof(IAndroidBuildSettings.KeyAlias))
            {
                Assert.That(settings.KeyAlias, Is.EqualTo(original.KeyAlias));
            }

            if (failingProperty != nameof(IAndroidBuildSettings.KeyAliasPassword))
            {
                Assert.That(settings.KeyAliasPassword,
                    Is.EqualTo(original.KeyAliasPassword));
            }
        }

        private static FakeAndroidBuildSettings CreateSettings()
        {
            return new FakeAndroidBuildSettings
            {
                ApplicationIdentifier = "com.example.original",
                VersionCode = 7,
                BuildAppBundle = false,
                UseCustomKeystore = true,
                KeystoreName = "/original/debug.jks",
                KeystorePassword = "original-store-password",
                KeyAlias = "original-alias",
                KeyAliasPassword = "original-alias-password",
            };
        }

        private sealed class FakeAndroidBuildSettings : IAndroidBuildSettings,
            IEquatable<FakeAndroidBuildSettings>
        {
            private string m_ApplicationIdentifier;
            private int m_VersionCode;
            private bool m_BuildAppBundle;
            private bool m_UseCustomKeystore;
            private string m_KeystoreName;
            private string m_KeystorePassword;
            private string m_KeyAlias;
            private string m_KeyAliasPassword;

            public bool IgnoreApplicationIdentifierWrites { get; set; }

            public bool IgnoreSigningWrites { get; set; }

            public string ThrowOnWriteProperty { get; set; }

            public List<string> AttemptedWrites { get; } = new List<string>();

            public string ApplicationIdentifier
            {
                get => m_ApplicationIdentifier;
                set
                {
                    RecordWrite(nameof(ApplicationIdentifier));
                    if (!IgnoreApplicationIdentifierWrites)
                    {
                        m_ApplicationIdentifier = value;
                    }
                }
            }

            public int VersionCode
            {
                get => m_VersionCode;
                set
                {
                    RecordWrite(nameof(VersionCode));
                    m_VersionCode = value;
                }
            }

            public bool BuildAppBundle
            {
                get => m_BuildAppBundle;
                set
                {
                    RecordWrite(nameof(BuildAppBundle));
                    m_BuildAppBundle = value;
                }
            }

            public bool UseCustomKeystore
            {
                get => m_UseCustomKeystore;
                set
                {
                    RecordWrite(nameof(UseCustomKeystore));
                    if (!IgnoreSigningWrites)
                    {
                        m_UseCustomKeystore = value;
                    }
                }
            }

            public string KeystoreName
            {
                get => m_KeystoreName;
                set
                {
                    RecordWrite(nameof(KeystoreName));
                    if (!IgnoreSigningWrites)
                    {
                        m_KeystoreName = value;
                    }
                }
            }

            public string KeystorePassword
            {
                get => m_KeystorePassword;
                set
                {
                    RecordWrite(nameof(KeystorePassword));
                    if (!IgnoreSigningWrites)
                    {
                        m_KeystorePassword = value;
                    }
                }
            }

            public string KeyAlias
            {
                get => m_KeyAlias;
                set
                {
                    RecordWrite(nameof(KeyAlias));
                    if (!IgnoreSigningWrites)
                    {
                        m_KeyAlias = value;
                    }
                }
            }

            public string KeyAliasPassword
            {
                get => m_KeyAliasPassword;
                set
                {
                    RecordWrite(nameof(KeyAliasPassword));
                    if (!IgnoreSigningWrites)
                    {
                        m_KeyAliasPassword = value;
                    }
                }
            }

            public FakeAndroidBuildSettings Copy()
            {
                return new FakeAndroidBuildSettings
                {
                    ApplicationIdentifier = ApplicationIdentifier,
                    VersionCode = VersionCode,
                    BuildAppBundle = BuildAppBundle,
                    UseCustomKeystore = UseCustomKeystore,
                    KeystoreName = KeystoreName,
                    KeystorePassword = KeystorePassword,
                    KeyAlias = KeyAlias,
                    KeyAliasPassword = KeyAliasPassword,
                };
            }

            public bool Equals(FakeAndroidBuildSettings other)
            {
                return other != null &&
                       ApplicationIdentifier == other.ApplicationIdentifier &&
                       VersionCode == other.VersionCode &&
                       BuildAppBundle == other.BuildAppBundle &&
                       UseCustomKeystore == other.UseCustomKeystore &&
                       KeystoreName == other.KeystoreName &&
                       KeystorePassword == other.KeystorePassword &&
                       KeyAlias == other.KeyAlias &&
                       KeyAliasPassword == other.KeyAliasPassword;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as FakeAndroidBuildSettings);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(
                    ApplicationIdentifier,
                    VersionCode,
                    BuildAppBundle,
                    UseCustomKeystore,
                    KeystoreName,
                    KeystorePassword,
                    KeyAlias,
                    KeyAliasPassword);
            }

            private void RecordWrite(string propertyName)
            {
                AttemptedWrites.Add(propertyName);
                if (string.Equals(
                        ThrowOnWriteProperty,
                        propertyName,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "fake setter failure " + FakeSecret);
                }
            }
        }
    }
}
