using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Build;
using NUnit.Framework;
using UnityEditor;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BuildOrchestratorTests
    {
        [TestCase("target")]
        [TestCase("state.capture")]
        [TestCase("input")]
        [TestCase("scrub.pre")]
        [TestCase("settings.apply")]
        [TestCase("commerce.acquire")]
        [TestCase("scene.acquire")]
        [TestCase("addressables")]
        [TestCase("signing.apply")]
        [TestCase("player")]
        [TestCase("signing.restore")]
        [TestCase("scene.cleanup")]
        [TestCase("commerce.cleanup")]
        [TestCase("settings.restore")]
        [TestCase("scrub.post")]
        public void Run_AnyFailureLeavesNoCanonicalOrStagingArtifact(string failurePoint)
        {
            WithTemporaryProject(projectRoot =>
            {
                var dependencies = new FakeDependencies(projectRoot, failurePoint);
                dependencies.CreateStaleArtifacts();
                var orchestrator = CreateOrchestrator(projectRoot, dependencies);

                Assert.Catch<Exception>(() =>
                    orchestrator.Run(BuildTargetKind.AndroidInternal));

                Assert.That(File.Exists(dependencies.FinalPath), Is.False);
                Assert.That(File.Exists(dependencies.StagingPath), Is.False);
            });
        }

        [TestCase("settings.apply", 0, 0, 0, 1)]
        [TestCase("commerce.acquire", 0, 0, 0, 1)]
        [TestCase("scene.acquire", 0, 0, 1, 1)]
        [TestCase("addressables", 0, 1, 1, 1)]
        [TestCase("signing.apply", 1, 1, 1, 1)]
        [TestCase("player", 1, 1, 1, 1)]
        [TestCase("signing.restore", 1, 1, 1, 1)]
        [TestCase("scene.cleanup", 1, 1, 1, 1)]
        [TestCase("commerce.cleanup", 1, 1, 1, 1)]
        [TestCase("settings.restore", 1, 1, 1, 1)]
        [TestCase("scrub.post", 1, 1, 1, 1)]
        public void Run_FailureBoundary_RestoresEachMutatedScopeOnceAndContinuesCleanup(
            string failurePoint,
            int expectedSigningRestores,
            int expectedSceneCleanups,
            int expectedCommerceCleanups,
            int expectedSettingsRestores)
        {
            WithTemporaryProject(projectRoot =>
            {
                var dependencies = new FakeDependencies(projectRoot, failurePoint);
                var orchestrator = CreateOrchestrator(projectRoot, dependencies);

                Assert.Catch<Exception>(() =>
                    orchestrator.Run(BuildTargetKind.AndroidInternal));

                Assert.That(dependencies.SigningRestoreCount,
                    Is.EqualTo(expectedSigningRestores));
                Assert.That(dependencies.SceneCleanupCount,
                    Is.EqualTo(expectedSceneCleanups));
                Assert.That(dependencies.CommerceCleanupCount,
                    Is.EqualTo(expectedCommerceCleanups));
                Assert.That(dependencies.SettingsRestoreCount,
                    Is.EqualTo(expectedSettingsRestores));
                Assert.That(dependencies.ScrubCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void Run_InvalidatesArtifactsBeforeBuildNumberOrSigningInput()
        {
            WithTemporaryProject(projectRoot =>
            {
                var dependencies = new FakeDependencies(projectRoot, "input");
                dependencies.CreateStaleArtifacts();
                dependencies.AssertArtifactsAbsentWhenReadingInput = true;
                var orchestrator = CreateOrchestrator(projectRoot, dependencies);

                Assert.Throws<InvalidOperationException>(() =>
                    orchestrator.Run(BuildTargetKind.AndroidInternal));

                Assert.That(dependencies.InputObservedArtifactsAbsent, Is.True);
            });
        }

        [Test]
        public void Run_SuccessPublishesOnlyAfterAllCleanupAndSecretVerification()
        {
            WithTemporaryProject(projectRoot =>
            {
                var dependencies = new FakeDependencies(projectRoot);
                var orchestrator = CreateOrchestrator(projectRoot, dependencies);

                orchestrator.Run(BuildTargetKind.AndroidInternal);

                Assert.That(File.ReadAllBytes(dependencies.FinalPath),
                    Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(File.Exists(dependencies.StagingPath), Is.False);
                Assert.That(dependencies.FinalAbsentDuringPostScrub, Is.True);
                Assert.That(dependencies.Trace, Is.EqualTo(new[]
                {
                    "target",
                    "state.capture",
                    "input",
                    "scrub.pre",
                    "settings.apply",
                    "commerce.acquire",
                    "scene.acquire",
                    "addressables",
                    "signing.apply",
                    "player",
                    "signing.restore",
                    "scene.cleanup",
                    "commerce.cleanup",
                    "settings.restore",
                    "scrub.post",
                }));
            });
        }

        [TestCase(BuildTargetKind.GooglePlay, "google")]
        [TestCase(BuildTargetKind.Galaxy, "galaxy")]
        public void Run_ReleaseVariantPassesOnlySelectedCredentialsToSigningAndBothScrubs(
            BuildTargetKind kind,
            string storeName)
        {
            WithTemporaryProject(projectRoot =>
            {
                var fakeSecret = "JSS_TEST_SENTINEL_" + storeName.ToUpperInvariant();
                var credentials = new ReleaseSigningCredentials(
                    "/project/" + storeName + ".jks",
                    fakeSecret + "-store",
                    storeName + "-alias",
                    fakeSecret + "-alias");
                var dependencies = new FakeDependencies(projectRoot)
                {
                    SigningCredentials = credentials,
                };
                var orchestrator = CreateOrchestrator(projectRoot, dependencies);

                orchestrator.Run(kind);

                Assert.That(dependencies.AppliedSigningCredentials,
                    Is.SameAs(credentials));
                Assert.That(dependencies.ScrubbedCredentials,
                    Has.Count.EqualTo(2));
                Assert.That(
                    dependencies.ScrubbedCredentials.All(item =>
                        ReferenceEquals(item, credentials)),
                    Is.True);
                Assert.That(string.Join("|", dependencies.Trace),
                    Does.Not.Contain(fakeSecret));
                Assert.That(File.Exists(dependencies.FinalPathFor(kind)), Is.True);
                Assert.That(File.Exists(dependencies.StagingPathFor(kind)), Is.False);
            });
        }

        [Test]
        public void Run_PrimaryAndCleanupFailuresPreserveAllEvidenceAndDoNotPublish()
        {
            WithTemporaryProject(projectRoot =>
            {
                var dependencies = new FakeDependencies(
                    projectRoot,
                    "player",
                    "signing.restore",
                    "scene.cleanup",
                    "settings.restore",
                    "scrub.post");
                var orchestrator = CreateOrchestrator(projectRoot, dependencies);

                var exception = Assert.Throws<AggregateException>(() =>
                    orchestrator.Run(BuildTargetKind.AndroidInternal));
                var messages = exception.Flatten().InnerExceptions
                    .Select(item => item.Message)
                    .ToArray();

                Assert.That(messages, Does.Contain("failure:player"));
                Assert.That(messages, Does.Contain("failure:signing.restore"));
                Assert.That(messages, Does.Contain("failure:scene.cleanup"));
                Assert.That(messages, Does.Contain("failure:settings.restore"));
                Assert.That(messages, Does.Contain("failure:scrub.post"));
                Assert.That(File.Exists(dependencies.FinalPath), Is.False);
                Assert.That(File.Exists(dependencies.StagingPath), Is.False);
            });
        }

        private static BuildOrchestrator CreateOrchestrator(
            string projectRoot,
            FakeDependencies dependencies)
        {
            return new BuildOrchestrator(
                projectRoot,
                dependencies,
                dependencies,
                dependencies,
                dependencies,
                dependencies,
                dependencies,
                dependencies,
                dependencies);
        }

        private static void WithTemporaryProject(Action<string> action)
        {
            var projectRoot = Path.Combine(
                Path.GetTempPath(),
                "jss-build-orchestrator-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectRoot);
            try
            {
                action(projectRoot);
            }
            finally
            {
                if (Directory.Exists(projectRoot))
                {
                    Directory.Delete(projectRoot, true);
                }
            }
        }

        private sealed class FakeDependencies :
            IBuildTargetGuard,
            IBuildInputReader,
            IAndroidBuildStateFactory,
            IAndroidBuildState,
            ICommerceBuildConfigurationLeaseFactory,
            ICommerceBuildConfigurationLease,
            IAddressablesBuilder,
            IBuildSceneLeaseFactory,
            IBuildSceneLease,
            IPlayerBuilder,
            ISigningSecretScrubber
        {
            private readonly HashSet<string> m_FailurePoints;
            private int m_ScrubCount;

            public FakeDependencies(string projectRoot, params string[] failurePoints)
            {
                ProjectRoot = projectRoot;
                m_FailurePoints = new HashSet<string>(
                    failurePoints ?? Array.Empty<string>(),
                    StringComparer.Ordinal);
                Trace = new List<string>();
            }

            public string ProjectRoot { get; }

            public string FinalPath => Path.Combine(
                ProjectRoot,
                "Builds/AndroidInternal/JustSomeStars-internal.apk");

            public string StagingPath => Path.Combine(
                ProjectRoot,
                "Builds/AndroidInternal/JustSomeStars-internal.jss-staging.apk");

            public List<string> Trace { get; }

            public bool AssertArtifactsAbsentWhenReadingInput { get; set; }

            public bool InputObservedArtifactsAbsent { get; private set; }

            public bool FinalAbsentDuringPostScrub { get; private set; }

            public int SigningRestoreCount { get; private set; }

            public int SceneCleanupCount { get; private set; }

            public int CommerceCleanupCount { get; private set; }

            public int SettingsRestoreCount { get; private set; }

            public int ScrubCount => m_ScrubCount;

            public ReleaseSigningCredentials SigningCredentials { get; set; }

            public ReleaseSigningCredentials AppliedSigningCredentials { get; private set; }

            public List<ReleaseSigningCredentials> ScrubbedCredentials { get; } =
                new List<ReleaseSigningCredentials>();

            public IReadOnlyList<string> PersistentDefineSymbols =>
                new[] { "FEATURE_ALPHA" };

            public IReadOnlyList<string> ScenePaths =>
                new[] { "Assets/FakeBuildScene.unity" };

            public void CreateStaleArtifacts()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FinalPath));
                File.WriteAllBytes(FinalPath, new byte[] { 9 });
                File.WriteAllBytes(StagingPath, new byte[] { 8 });
            }

            public string FinalPathFor(BuildTargetKind kind)
            {
                switch (kind)
                {
                    case BuildTargetKind.AndroidInternal:
                        return FinalPath;
                    case BuildTargetKind.GooglePlay:
                        return Path.Combine(
                            ProjectRoot,
                            "Builds/GooglePlay/JustSomeStars-google-play.aab");
                    case BuildTargetKind.Galaxy:
                        return Path.Combine(
                            ProjectRoot,
                            "Builds/Galaxy/JustSomeStars-galaxy.aab");
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
                }
            }

            public string StagingPathFor(BuildTargetKind kind)
            {
                var finalPath = FinalPathFor(kind);
                return Path.Combine(
                    Path.GetDirectoryName(finalPath),
                    Path.GetFileNameWithoutExtension(finalPath) +
                    ".jss-staging" +
                    Path.GetExtension(finalPath));
            }

            public void EnsureReady()
            {
                Record("target");
            }

            public BuildInputs Read(BuildTargetKind kind)
            {
                Trace.Add("input");
                if (AssertArtifactsAbsentWhenReadingInput)
                {
                    InputObservedArtifactsAbsent =
                        !File.Exists(FinalPath) && !File.Exists(StagingPath);
                }

                ThrowIfRequested("input");
                return new BuildInputs(
                    buildNumber: 42,
                    signingCredentials: SigningCredentials);
            }

            public IAndroidBuildState Capture()
            {
                Record("state.capture");
                return this;
            }

            public void ApplySettings(BuildConfiguration configuration)
            {
                Record("settings.apply");
            }

            public void ApplySigning(
                BuildConfiguration configuration,
                ReleaseSigningCredentials credentials)
            {
                AppliedSigningCredentials = credentials;
                Record("signing.apply");
            }

            public void RestoreSigningAndVerify()
            {
                SigningRestoreCount++;
                Record("signing.restore");
            }

            public void RestoreSettingsAndVerify()
            {
                SettingsRestoreCount++;
                Record("settings.restore");
            }

            void IAddressablesBuilder.Build(
                BuildConfiguration configuration,
                BuildPlayerOptions playerOptions)
            {
                Record("addressables");
            }

            public IBuildSceneLease Acquire()
            {
                Record("scene.acquire");
                return this;
            }

            public ICommerceBuildConfigurationLease Acquire(
                BuildConfiguration configuration)
            {
                _ = configuration ?? throw new ArgumentNullException(
                    nameof(configuration));
                Record("commerce.acquire");
                return this;
            }

            public void CleanupAndVerify()
            {
                SceneCleanupCount++;
                Record("scene.cleanup");
            }

            void ICommerceBuildConfigurationLease.CleanupAndVerify()
            {
                CommerceCleanupCount++;
                Record("commerce.cleanup");
            }

            void IDisposable.Dispose()
            {
                ((ICommerceBuildConfigurationLease)this).CleanupAndVerify();
            }

            void IPlayerBuilder.Build(
                BuildConfiguration configuration,
                BuildPlayerOptions playerOptions)
            {
                Trace.Add("player");
                Directory.CreateDirectory(Path.GetDirectoryName(playerOptions.locationPathName));
                File.WriteAllBytes(playerOptions.locationPathName, new byte[] { 1, 2, 3 });
                ThrowIfRequested("player");
            }

            public void ScrubAndVerify(ReleaseSigningCredentials credentials)
            {
                m_ScrubCount++;
                ScrubbedCredentials.Add(credentials);
                var point = m_ScrubCount == 1 ? "scrub.pre" : "scrub.post";
                Trace.Add(point);
                if (point == "scrub.post")
                {
                    FinalAbsentDuringPostScrub = !File.Exists(FinalPath);
                }

                ThrowIfRequested(point);
            }

            private void Record(string point)
            {
                Trace.Add(point);
                ThrowIfRequested(point);
            }

            private void ThrowIfRequested(string point)
            {
                if (m_FailurePoints.Contains(point))
                {
                    throw new InvalidOperationException("failure:" + point);
                }
            }
        }
    }
}
