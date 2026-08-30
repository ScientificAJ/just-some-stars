using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class AtlasTests
    {
        private readonly List<UnityEngine.Object> m_Owned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in m_Owned)
            {
                if (item != null && !AssetDatabase.Contains(item))
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            m_Owned.Clear();
        }

        [Test]
        public void PhenomenonUnlock_IsExactlyOnceAndEachScienceDepthResolvesDistinctEnglishText()
        {
            var source = Source();
            var catalog = Catalog();
            var entry = Entry();
            var progression = new RecordingProgressionStore();
            var bus = new GameEventBus();
            using var atlas = new AtlasService(
                bus,
                progression,
                new[] { entry },
                new[] { source },
                catalog);

            bus.Publish(new PhenomenonObserved(entry.PhenomenonId));
            bus.Publish(new PhenomenonObserved(entry.PhenomenonId));

            Assert.That(progression.Unlocks, Is.EqualTo(1));
            Assert.That(progression.DiscoveryIds, Is.EqualTo(new[] { entry.PhenomenonId.Value }));
            Assert.That(progression.AtlasEntryIds, Is.EqualTo(new[] { entry.StableId.Value }));
            Assert.That(atlas.ResolveEnglish(entry.StableId, ScienceDepth.Guided), Is.EqualTo("Mirra has a hot day side and a cold night side."));
            Assert.That(atlas.ResolveEnglish(entry.StableId, ScienceDepth.Balanced), Does.Contain("atmosphere moves heat"));
            Assert.That(atlas.ResolveEnglish(entry.StableId, ScienceDepth.Deep), Does.Contain("tidally locked"));
            Assert.That(source.SourceUrl, Does.StartWith("https://science.nasa.gov/"));
        }

        [Test]
        public void RealTask18Assets_HaveValidCrossLinksAndResolvableEnglishValues()
        {
            var entry = AssetDatabase.LoadAssetAtPath<AtlasEntry>(
                "Assets/_JustSomeStars/Content/Atlas/MirraTemperatureAtlas.asset");
            var source = AssetDatabase.LoadAssetAtPath<ScienceSourceDefinition>(
                "Assets/_JustSomeStars/Content/ScienceSources/MirraTidalClimate.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<LocalizedEnglishCatalog>(
                "Assets/_JustSomeStars/Content/Localization/English/Task18English.asset");

            Assert.That(entry, Is.Not.Null);
            Assert.That(source, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            entry.ValidateOrThrow();
            source.ValidateOrThrow();
            Assert.That(entry.ScienceSourceId, Is.EqualTo(source.StableId));
            Assert.That(
                source.SourceUrl,
                Is.EqualTo("https://science.nasa.gov/asset/webb/diagram-of-an-exoplanet-phase-curve/"));
            Assert.That(source.UseNote, Does.Contain("tidally locked"));
            Assert.That(source.UseNote, Does.Contain("heat"));
            Assert.That(catalog.Resolve(entry.ShortTextKey), Is.Not.Empty);
            Assert.That(catalog.Resolve(entry.BalancedTextKey), Is.Not.Empty);
            Assert.That(catalog.Resolve(entry.DeepTextKey), Is.Not.Empty);
        }

        [Test]
        public void ProjectValidation_FindsBrokenGraphLocalizationAndScienceReferences()
        {
            const string missionPath =
                "Assets/_JustSomeStars/Content/__Task18BrokenMission.asset";
            const string dialoguePath =
                "Assets/_JustSomeStars/Content/__Task18BrokenDialogue.asset";
            const string atlasPath =
                "Assets/_JustSomeStars/Content/__Task18BrokenAtlas.asset";
            try
            {
                var mission = ScriptableObject.CreateInstance<MissionDefinition>();
                mission.Configure(
                    "mission.task18.broken",
                    "mission.task18.missing-entry",
                    new[]
                    {
                        new MissionNode(
                            "mission.task18.only-terminal",
                            MissionNodeKind.Terminal,
                            Array.Empty<MissionRequirement>(),
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            null,
                            0),
                        new MissionNode(
                            "mission.task18.dead-end",
                            MissionNodeKind.Objective,
                            new[]
                            {
                                new MissionRequirement(
                                    MissionEventKind.SignalFragmentRecovered,
                                    "signal.task18.missing"),
                            },
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            null,
                            0),
                    });
                AssetDatabase.CreateAsset(mission, missionPath);

                var dialogue = ScriptableObject.CreateInstance<DialogueEntry>();
                dialogue.Configure(
                    "dialogue.task18.missing-localization",
                    "dialogue.task18.not-authored",
                    "crew.mira",
                    "voice.fixture",
                    "calm",
                    "focus",
                    "gesture.none",
                    Array.Empty<string>(),
                    DialoguePriority.Ambient,
                    true,
                    0,
                    Array.Empty<string>());
                AssetDatabase.CreateAsset(dialogue, dialoguePath);

                var atlas = ScriptableObject.CreateInstance<AtlasEntry>();
                atlas.Configure(
                    "atlas.task18.missing-source",
                    "phenomenon.mirra.temperature-gradient",
                    "science-source.task18.missing",
                    "atlas.mirra.temperature.short",
                    "atlas.mirra.temperature.balanced",
                    "atlas.mirra.temperature.deep");
                AssetDatabase.CreateAsset(atlas, atlasPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var report = ProjectContentValidator.ValidateProject();

                Assert.That(
                    report.Issues,
                    Has.Some.Matches<ValidationIssue>(issue =>
                        issue.AssetPath == missionPath &&
                        issue.Code == ValidationIssueCode.InvalidContentAsset));
                Assert.That(
                    report.Issues,
                    Has.Some.Matches<ValidationIssue>(issue =>
                        issue.AssetPath == dialoguePath &&
                        issue.Code == ValidationIssueCode.MissingLocalization));
                Assert.That(
                    report.Issues,
                    Has.Some.Matches<ValidationIssue>(issue =>
                        issue.AssetPath == atlasPath &&
                        issue.Code == ValidationIssueCode.MissingScienceSource));
            }
            finally
            {
                AssetDatabase.DeleteAsset(missionPath);
                AssetDatabase.DeleteAsset(dialoguePath);
                AssetDatabase.DeleteAsset(atlasPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void ProjectValidation_RejectsMissionPredictionOutsideDeclaredCatalog()
        {
            const string missionPath =
                "Assets/_JustSomeStars/Content/__Task18UnknownPredictionMission.asset";
            try
            {
                var mission = ScriptableObject.CreateInstance<MissionDefinition>();
                mission.Configure(
                    "mission.task18.unknown-prediction",
                    "mission.task18.unknown.entry",
                    new[]
                    {
                        new MissionNode(
                            "mission.task18.unknown.entry",
                            MissionNodeKind.Entry,
                            Array.Empty<MissionRequirement>(),
                            new[] { "mission.task18.unknown.predict" },
                            Array.Empty<string>(),
                            null,
                            0),
                        new MissionNode(
                            "mission.task18.unknown.predict",
                            MissionNodeKind.Objective,
                            new[]
                            {
                                new MissionRequirement(
                                    MissionEventKind.PredictionRecorded,
                                    "prediction.task18.not-declared"),
                            },
                            new[] { "mission.task18.unknown.terminal" },
                            Array.Empty<string>(),
                            null,
                            0),
                        new MissionNode(
                            "mission.task18.unknown.terminal",
                            MissionNodeKind.Terminal,
                            Array.Empty<MissionRequirement>(),
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            null,
                            0),
                    });
                AssetDatabase.CreateAsset(mission, missionPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var report = ProjectContentValidator.ValidateProject();

                Assert.That(
                    report.Issues,
                    Has.Some.Matches<ValidationIssue>(issue =>
                        issue.AssetPath == missionPath &&
                        issue.Code == ValidationIssueCode.InvalidContentAsset &&
                        issue.Message.Contains(
                            "prediction.task18.not-declared",
                            StringComparison.Ordinal)));
            }
            finally
            {
                AssetDatabase.DeleteAsset(missionPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private AtlasEntry Entry()
        {
            var entry = ScriptableObject.CreateInstance<AtlasEntry>();
            entry.Configure(
                "atlas.mirra.temperature-gradient",
                "phenomenon.mirra.temperature-gradient",
                "science-source.mirra.tidal-climate",
                "atlas.mirra.short",
                "atlas.mirra.balanced",
                "atlas.mirra.deep");
            m_Owned.Add(entry);
            return entry;
        }

        private ScienceSourceDefinition Source()
        {
            var source = ScriptableObject.CreateInstance<ScienceSourceDefinition>();
            source.Configure(
                "science-source.mirra.tidal-climate",
                "NASA Exoplanet Exploration: Tidally Locked Worlds",
                "NASA",
                "https://science.nasa.gov/asset/webb/diagram-of-an-exoplanet-phase-curve/",
                "Task 18 scientific grounding for atmospheric heat transport.");
            m_Owned.Add(source);
            return source;
        }

        private LocalizedEnglishCatalog Catalog()
        {
            var catalog = ScriptableObject.CreateInstance<LocalizedEnglishCatalog>();
            catalog.Configure(new[]
            {
                new LocalizedEnglishText("atlas.mirra.short", "Mirra has a hot day side and a cold night side."),
                new LocalizedEnglishText("atlas.mirra.balanced", "Mirra's atmosphere moves heat from permanent day toward permanent night."),
                new LocalizedEnglishText("atlas.mirra.deep", "On a tidally locked world, atmospheric circulation can reduce the temperature contrast between the permanent day and night hemispheres."),
            });
            m_Owned.Add(catalog);
            return catalog;
        }

        private sealed class RecordingProgressionStore : IProgressionStore
        {
            private readonly HashSet<string> m_Discoveries = new(StringComparer.Ordinal);
            private readonly HashSet<string> m_Atlas = new(StringComparer.Ordinal);
            public int Unlocks { get; private set; }
            public IReadOnlyCollection<string> DiscoveryIds => m_Discoveries;
            public IReadOnlyCollection<string> AtlasEntryIds => m_Atlas;
            public bool TryUnlock(ContentId discoveryId, ContentId atlasEntryId)
            {
                if (!m_Discoveries.Add(discoveryId.Value) | !m_Atlas.Add(atlasEntryId.Value))
                {
                    return false;
                }

                Unlocks++;
                return true;
            }
        }
    }
}
