using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class SettingsServiceTests
    {
        private string m_TestRoot;
        private string m_SettingsPath;

        [SetUp]
        public void SetUp()
        {
            m_TestRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask6SettingsTests",
                Guid.NewGuid().ToString("N"));
            m_SettingsPath = Path.Combine(m_TestRoot, "jss-settings-v1.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_TestRoot))
            {
                Directory.Delete(m_TestRoot, recursive: true);
            }
        }

        [Test]
        public void Defaults_AreIndependentBalancedAndAccessibleBeforeOpening()
        {
            var settings = GameSettings.CreateDefaults();

            Assert.That(settings.SchemaVersion, Is.EqualTo(1));
            Assert.That(settings.PilotingAssist, Is.EqualTo(AssistLevel.Balanced));
            Assert.That(settings.ExplorationAssist, Is.EqualTo(AssistLevel.Balanced));
            Assert.That(settings.ScienceDepth, Is.EqualTo(ScienceDepth.Balanced));
            Assert.That(settings.CaptionsEnabled, Is.True);
            Assert.That(settings.TextScale, Is.EqualTo(1f));
            Assert.That(settings.DyslexiaFriendlyFontEnabled, Is.False);
            Assert.That(settings.DialogueSpeed, Is.EqualTo(1f));
            Assert.That(settings.ColorVisionMode, Is.EqualTo(ColorVisionMode.Standard));
            Assert.That(settings.ReducedCameraShake, Is.False);
            Assert.That(settings.ReducedFlashing, Is.False);
            Assert.That(settings.ReducedMotion, Is.False);
            Assert.That(settings.MotionBlurEnabled, Is.False);
            Assert.That(settings.ParticleDensity, Is.EqualTo(1f));
            Assert.That(
                settings.PresentationQuality,
                Is.EqualTo(PresentationQuality.Balanced));
            Assert.That(settings.MusicVolume, Is.EqualTo(0.8f));
            Assert.That(settings.DialogueVolume, Is.EqualTo(1f));
            Assert.That(settings.EffectsVolume, Is.EqualTo(0.9f));
            Assert.That(settings.HapticsEnabled, Is.True);
            Assert.That(settings.LeftHandedControls, Is.False);
            Assert.That(settings.TouchSensitivity, Is.EqualTo(1f));
        }

        [Test]
        public async Task MissingDocument_LoadsDefaultsWithoutClaimingPersistence()
        {
            var service = new SettingsService(m_SettingsPath);

            var result = await service.InitializeAsync(CancellationToken.None);

            Assert.That(result.IsAvailable, Is.True);
            Assert.That(service.IsInitialized, Is.True);
            Assert.That(service.Current, Is.EqualTo(GameSettings.CreateDefaults()));
            Assert.That(File.Exists(m_SettingsPath), Is.False);
            await service.ShutdownAsync();
            Assert.That(service.IsInitialized, Is.False);
        }

        [Test]
        public async Task Apply_WritesAtomicallyRaisesOnceAndReopensExactSnapshot()
        {
            var service = new SettingsService(m_SettingsPath);
            await service.InitializeAsync(CancellationToken.None);
            var changes = 0;
            GameSettings observed = null;
            service.SettingsChanged += settings =>
            {
                changes++;
                observed = settings;
            };
            var updated = service.Current;
            updated.PilotingAssist = AssistLevel.Guided;
            updated.ExplorationAssist = AssistLevel.Ace;
            updated.ScienceDepth = ScienceDepth.Deep;
            updated.CaptionsEnabled = false;
            updated.TextScale = 1.35f;
            updated.DyslexiaFriendlyFontEnabled = true;
            updated.DialogueSpeed = 1.5f;
            updated.ColorVisionMode = ColorVisionMode.Deuteranopia;
            updated.ReducedCameraShake = true;
            updated.ReducedFlashing = true;
            updated.ReducedMotion = true;
            updated.MotionBlurEnabled = true;
            updated.ParticleDensity = 0.25f;
            updated.PresentationQuality = PresentationQuality.Performance;
            updated.MusicVolume = 0.25f;
            updated.DialogueVolume = 0.5f;
            updated.EffectsVolume = 0.75f;
            updated.HapticsEnabled = false;
            updated.LeftHandedControls = true;
            updated.TouchSensitivity = 2f;

            Assert.That(service.Apply(updated), Is.True);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(observed, Is.EqualTo(updated));
            Assert.That(observed, Is.Not.SameAs(updated));
            Assert.That(service.Current, Is.EqualTo(updated));
            Assert.That(File.Exists(m_SettingsPath), Is.True);
            Assert.That(File.Exists(m_SettingsPath + ".tmp"), Is.False);

            var reopened = new SettingsService(m_SettingsPath);
            var reopenResult = await reopened.InitializeAsync(CancellationToken.None);
            Assert.That(reopenResult.IsAvailable, Is.True);
            Assert.That(reopened.Current, Is.EqualTo(updated));

            await reopened.ShutdownAsync();
            await service.ShutdownAsync();
        }

        [Test]
        public async Task CurrentAndChangedSnapshots_CannotMutateServiceState()
        {
            var service = new SettingsService(m_SettingsPath);
            await service.InitializeAsync(CancellationToken.None);
            GameSettings observed = null;
            service.SettingsChanged += settings => observed = settings;
            var applied = service.Current;
            applied.MusicVolume = 0.4f;
            Assert.That(service.Apply(applied), Is.True);

            var current = service.Current;
            current.MusicVolume = 0f;
            observed.MusicVolume = 1f;

            Assert.That(service.Current.MusicVolume, Is.EqualTo(0.4f));
            await service.ShutdownAsync();
        }

        [Test]
        public async Task EqualSnapshot_PerformsNoWriteAndRaisesNoEvent()
        {
            var storage = new RecordingSettingsStorage();
            var service = new SettingsService(m_SettingsPath, storage);
            await service.InitializeAsync(CancellationToken.None);
            var changes = 0;
            service.SettingsChanged += _ => changes++;

            Assert.That(service.Apply(service.Current), Is.False);

            Assert.That(storage.WriteCount, Is.EqualTo(0));
            Assert.That(changes, Is.EqualTo(0));
            await service.ShutdownAsync();
        }

        [TestCase("not json")]
        [TestCase("{\"schemaVersion\":2}")]
        [TestCase("{\"schemaVersion\":1,\"pilotingAssist\":99}")]
        [TestCase("{\"schemaVersion\":1,\"textScale\":9}")]
        public async Task InvalidPersistedDocument_FailsClosedToCompleteDefaults(
            string document)
        {
            Directory.CreateDirectory(m_TestRoot);
            File.WriteAllText(m_SettingsPath, document);
            var service = new SettingsService(m_SettingsPath);

            var result = await service.InitializeAsync(CancellationToken.None);

            Assert.That(result.IsAvailable, Is.True);
            Assert.That(service.Current, Is.EqualTo(GameSettings.CreateDefaults()));
            Assert.That(File.ReadAllText(m_SettingsPath), Is.EqualTo(document));
            await service.ShutdownAsync();
        }

        [Test]
        public async Task Apply_WhenAtomicWriteFails_PreservesSnapshotAndRaisesNothing()
        {
            var storage = new ThrowingSettingsStorage();
            var service = new SettingsService(m_SettingsPath, storage);
            await service.InitializeAsync(CancellationToken.None);
            var before = service.Current;
            var updated = before.Copy();
            updated.TouchSensitivity = 1.5f;
            var changes = 0;
            service.SettingsChanged += _ => changes++;

            Assert.Throws<IOException>(() => service.Apply(updated));

            Assert.That(service.Current, Is.EqualTo(before));
            Assert.That(changes, Is.EqualTo(0));
            Assert.That(storage.WriteCount, Is.EqualTo(1));
            await service.ShutdownAsync();
        }

        [Test]
        public async Task Apply_RejectsInvalidCallerSnapshotBeforeWriting()
        {
            var storage = new RecordingSettingsStorage();
            var service = new SettingsService(m_SettingsPath, storage);
            await service.InitializeAsync(CancellationToken.None);
            var invalid = service.Current;
            invalid.TextScale = float.NaN;

            Assert.Throws<ArgumentOutOfRangeException>(() => service.Apply(invalid));

            Assert.That(storage.WriteCount, Is.EqualTo(0));
            Assert.That(service.Current, Is.EqualTo(GameSettings.CreateDefaults()));
            await service.ShutdownAsync();
        }

        private class RecordingSettingsStorage : ISettingsStorage
        {
            public int WriteCount { get; private set; }

            public virtual bool TryRead(string path, out string document)
            {
                document = null;
                return false;
            }

            public virtual void WriteAtomically(string path, string document)
            {
                WriteCount++;
            }
        }

        private sealed class ThrowingSettingsStorage : RecordingSettingsStorage
        {
            public override void WriteAtomically(string path, string document)
            {
                base.WriteAtomically(path, document);
                throw new IOException("Injected atomic settings write failure.");
            }
        }
    }
}
