using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using JustSomeStars.Editor.Build;
using NUnit.Framework;
using UnityEditor.Build;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class FirebasePrivacyBuildProcessorTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "JssTask21FirebasePrivacy",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }

        [Test]
        public void PatchUnityLibraryGradle_ExcludesAnalyticsExactlyOnce()
        {
            var path = Path.Combine(m_Root, "build.gradle");
            File.WriteAllText(path, "dependencies {\n    implementation 'x:y:1'\n}\n");

            FirebasePrivacyBuildProcessor.PatchUnityLibraryGradle(path);
            FirebasePrivacyBuildProcessor.PatchUnityLibraryGradle(path);

            var result = File.ReadAllText(path);
            Assert.That(
                result.Split(new[]
                {
                    FirebasePrivacyBuildProcessor.PolicyMarker,
                }, StringSplitOptions.None),
                Has.Length.EqualTo(2));
            Assert.That(
                result,
                Does.Contain("exclude group: 'com.google.firebase', " +
                             "module: 'firebase-analytics'"));
        }

        [Test]
        public void Postprocessor_PatchesUnityLibraryModulePath()
        {
            var path = Path.Combine(m_Root, "build.gradle");
            File.WriteAllText(path, "dependencies {\n}\n");

            new FirebasePrivacyBuildProcessor()
                .OnPostGenerateGradleAndroidProject(m_Root);

            Assert.That(
                File.ReadAllText(path),
                Does.Contain(FirebasePrivacyBuildProcessor.PolicyMarker));
        }

        [Test]
        public void PatchUnityLibraryGradle_FailsClosedForUnexpectedProjectShape()
        {
            var missing = Path.Combine(m_Root, "missing.gradle");
            Assert.Throws<BuildFailedException>(() =>
                FirebasePrivacyBuildProcessor.PatchUnityLibraryGradle(missing));

            var malformed = Path.Combine(m_Root, "malformed.gradle");
            File.WriteAllText(malformed, "plugins {}\n");
            Assert.Throws<BuildFailedException>(() =>
                FirebasePrivacyBuildProcessor.PatchUnityLibraryGradle(malformed));
        }

        [Test]
        public void AndroidBackupPolicy_ExcludesEveryPersistentStorageDomain()
        {
            var project = Directory.GetCurrentDirectory();
            var manifestPath = Path.Combine(
                project,
                "Assets/Plugins/Android/AndroidManifest.xml");
            var modernPath = Path.Combine(
                project,
                "Assets/Plugins/Android/JustSomeStarsPrivacy.androidlib/" +
                "res/xml/jss_data_extraction_rules.xml");
            var legacyPath = Path.Combine(
                project,
                "Assets/Plugins/Android/JustSomeStarsPrivacy.androidlib/" +
                "res/xml/jss_full_backup_content.xml");
            XNamespace android = "http://schemas.android.com/apk/res/android";

            var manifest = XDocument.Load(manifestPath);
            var application = manifest.Root?.Element("application");
            Assert.That(application, Is.Not.Null);
            Assert.That((string)application.Attribute(android + "allowBackup"),
                Is.EqualTo("false"));
            Assert.That((string)application.Attribute(android + "dataExtractionRules"),
                Is.EqualTo("@xml/jss_data_extraction_rules"));
            Assert.That((string)application.Attribute(android + "fullBackupContent"),
                Is.EqualTo("@xml/jss_full_backup_content"));

            Assert.That(File.Exists(modernPath), Is.True, modernPath);
            Assert.That(File.Exists(legacyPath), Is.True, legacyPath);
            var modern = XDocument.Load(modernPath);
            var legacy = XDocument.Load(legacyPath);
            var expectedDomains = new[]
            {
                "root",
                "file",
                "database",
                "sharedpref",
                "external",
                "device_root",
                "device_file",
                "device_database",
                "device_sharedpref",
            };
            var modernExclusions = modern.Descendants("exclude").ToArray();
            foreach (var section in new[] { "cloud-backup", "device-transfer" })
            {
                var exclusions = modern.Root?.Element(section)?
                    .Elements("exclude")
                    .ToArray();
                Assert.That(exclusions, Is.Not.Null, section);
                Assert.That(
                    exclusions.Select(element => (string)element.Attribute("domain")),
                    Is.EquivalentTo(expectedDomains),
                    section);
                Assert.That(
                    exclusions.Select(element => (string)element.Attribute("path")),
                    Is.All.EqualTo("."),
                    section);
            }

            Assert.That(modernExclusions, Has.Length.EqualTo(expectedDomains.Length * 2));
            Assert.That(
                legacy.Root?.Elements("exclude")
                    .Select(element => (string)element.Attribute("domain")),
                Is.EquivalentTo(expectedDomains.Take(5)));
        }

        [Test]
        public void VendoredFirebasePackages_MatchTheOfficialArchiveReceipt()
        {
            var project = Directory.GetCurrentDirectory();
            var packageRoot = Path.Combine(project, "Packages/FirebasePackages");
            var receiptPath = Path.Combine(packageRoot, "UPSTREAM_PROVENANCE.md");
            Assert.That(File.Exists(receiptPath), Is.True, receiptPath);
            var receipt = File.ReadAllText(receiptPath);
            var expected = new Dictionary<string, (long Bytes, string Hash, string Url)>
            {
                ["com.google.external-dependency-manager-1.2.186.tgz"] = (
                    420750L,
                    "46684b475c2a39844c44c07945b5aee02895c41a9bff97d5cd4b5d9e85e021d8",
                    "https://dl.google.com/games/registry/unity/" +
                    "com.google.external-dependency-manager/" +
                    "com.google.external-dependency-manager-1.2.186.tgz"),
                ["com.google.firebase.app-13.16.0.tgz"] = (
                    61721869L,
                    "691f7ef26d080de43a011ce7846567fa72ceede5bdf4917edc0dc7a715c38dd4",
                    "https://dl.google.com/games/registry/unity/" +
                    "com.google.firebase.app/com.google.firebase.app-13.16.0.tgz"),
                ["com.google.firebase.auth-13.16.0.tgz"] = (
                    2650552L,
                    "5718553c264ab8a971f7ee12628b19f4e767c7156fe7a80d0107c2e0859229e4",
                    "https://dl.google.com/games/registry/unity/" +
                    "com.google.firebase.auth/com.google.firebase.auth-13.16.0.tgz"),
                ["com.google.firebase.firestore-13.16.0.tgz"] = (
                    13137768L,
                    "d5613461ac91b1cd01a18de31e5647cca1c647e57e8de8eefd2a05d6fbf49db1",
                    "https://dl.google.com/games/registry/unity/" +
                    "com.google.firebase.firestore/" +
                    "com.google.firebase.firestore-13.16.0.tgz"),
            };

            foreach (var pair in expected)
            {
                var path = Path.Combine(packageRoot, pair.Key);
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(new FileInfo(path).Length, Is.EqualTo(pair.Value.Bytes), pair.Key);
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    var actual = string.Concat(
                        sha.ComputeHash(stream).Select(value => value.ToString("x2")));
                    Assert.That(actual, Is.EqualTo(pair.Value.Hash), pair.Key);
                }

                Assert.That(receipt, Does.Contain(pair.Key));
                Assert.That(receipt, Does.Contain(pair.Value.Hash));
                Assert.That(receipt, Does.Contain(pair.Value.Url));
            }
        }
    }
}
