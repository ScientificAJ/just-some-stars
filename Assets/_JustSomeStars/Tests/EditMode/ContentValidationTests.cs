using System;
using System.Linq;
using JustSomeStars.Editor.Validation;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class ContentValidationTests
    {
        public const string BrokenFixturePath =
            "Assets/_JustSomeStars/Tests/EditMode/__Task9BrokenSceneCatalog.asset";

        [Test]
        public void ContentId_RequiresCanonicalValueAndUsesOrdinalEquality()
        {
            Assert.Throws<ArgumentException>(() => new ContentId(null));
            Assert.Throws<ArgumentException>(() => new ContentId(string.Empty));
            Assert.Throws<ArgumentException>(() => new ContentId("   "));
            Assert.Throws<ArgumentException>(() => new ContentId(" mirra"));
            Assert.Throws<ArgumentException>(() => new ContentId("mirra "));

            var first = new ContentId("destination.mirra");
            var same = new ContentId("destination.mirra");
            var differentCase = new ContentId("destination.Mirra");

            Assert.That(first.IsValid, Is.True);
            Assert.That(default(ContentId).IsValid, Is.False);
            Assert.That(first.Value, Is.EqualTo("destination.mirra"));
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(differentCase));
            Assert.That(first.ToString(), Is.EqualTo("destination.mirra"));
        }

        [Test]
        public void TypedGameEvents_ExposeTheirExactContentIdentifiers()
        {
            var destination = new ContentId("destination.mirra");
            var phenomenon = new ContentId("phenomenon.mirra-terminator");
            var prediction = new ContentId("prediction.mirra-temperature");
            var instrument = new ContentId("instrument.thermal-camera");
            var fragment = new ContentId("signal.fragment.1");
            var conversation = new ContentId("conversation.mirra-arrival");

            Assert.That(
                new LandingCompleted(destination).DestinationId,
                Is.EqualTo(destination));
            Assert.That(
                new PhenomenonObserved(phenomenon).PhenomenonId,
                Is.EqualTo(phenomenon));
            Assert.That(
                new PredictionRecorded(prediction).PredictionId,
                Is.EqualTo(prediction));
            Assert.That(
                new InstrumentUsed(instrument).InstrumentId,
                Is.EqualTo(instrument));
            Assert.That(
                new SignalFragmentRecovered(fragment).FragmentId,
                Is.EqualTo(fragment));
            Assert.That(
                new ConversationCompleted(conversation).ConversationId,
                Is.EqualTo(conversation));
        }

        [Test]
        public void GameEventBus_PublishesOnlyToTheExactEventType()
        {
            var bus = new GameEventBus();
            var landingCount = 0;
            var phenomenonCount = 0;
            var landingId = default(ContentId);
            var landingSubscription = bus.Subscribe<LandingCompleted>(gameEvent =>
            {
                landingCount++;
                landingId = gameEvent.DestinationId;
            });
            var phenomenonSubscription =
                bus.Subscribe<PhenomenonObserved>(_ => phenomenonCount++);
            var destination = new ContentId("destination.mirra");

            bus.Publish(new LandingCompleted(destination));

            Assert.That(landingCount, Is.EqualTo(1));
            Assert.That(landingId, Is.EqualTo(destination));
            Assert.That(phenomenonCount, Is.Zero);
            landingSubscription.Dispose();
            phenomenonSubscription.Dispose();
        }

        [Test]
        public void GameEventBus_DisposedSubscriptionCannotFireAndCleanupIsIdempotent()
        {
            var bus = new GameEventBus();
            var invocationCount = 0;
            IDisposable subscription = null;
            subscription = bus.Subscribe<SignalFragmentRecovered>(_ =>
            {
                invocationCount++;
                subscription.Dispose();
            });
            var gameEvent = new SignalFragmentRecovered(
                new ContentId("signal.fragment.1"));

            bus.Publish(gameEvent);
            bus.Publish(gameEvent);
            subscription.Dispose();

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        [Test]
        public void ProjectValidator_RejectsDuplicateIdsAndMissingContentLinks()
        {
            var mission = new ContentId("mission.mirra");
            var missingMission = new ContentId("mission.missing");
            var missingDialogue = new ContentId("dialogue.missing");
            var builder = new ProjectContentIndexBuilder()
                .AddDefinition(
                    mission,
                    ContentKind.Mission,
                    "Assets/Content/Missions/mirra.asset")
                .AddDefinition(
                    mission,
                    ContentKind.Mission,
                    "Assets/Content/Missions/mirra-copy.asset")
                .AddReference(
                    mission,
                    missingMission,
                    ContentReferenceKind.MissionLink,
                    "Assets/Content/Missions/mirra.asset")
                .AddReference(
                    mission,
                    missingDialogue,
                    ContentReferenceKind.DialogueReference,
                    "Assets/Content/Missions/mirra.asset");

            var report = ProjectContentValidator.Validate(builder.Build());

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Is.EquivalentTo(new[]
                {
                    ValidationIssueCode.DuplicateContentId,
                    ValidationIssueCode.MissingMissionLink,
                    ValidationIssueCode.MissingDialogueReference,
                }));
        }

        [Test]
        public void ProjectValidator_RejectsBrokenCrossSystemContracts()
        {
            var phenomenon = new ContentId("phenomenon.mirra-terminator");
            var destination = new ContentId("destination.mirra");
            var cosmetic = new ContentId("cosmetic.launch-suit");
            var product = new ContentId("product.explorer-edition");
            var missingScience = new ContentId("science-source.missing");
            var missingEntitlement = new ContentId("entitlement.missing");
            var builder = new ProjectContentIndexBuilder()
                .AddDefinition(
                    phenomenon,
                    ContentKind.Phenomenon,
                    "Assets/Content/Phenomena/mirra.asset")
                .AddDefinition(
                    destination,
                    ContentKind.Destination,
                    "Assets/Content/Destinations/mirra.asset")
                .AddDefinition(
                    cosmetic,
                    ContentKind.Cosmetic,
                    "Assets/Content/Cosmetics/launch-suit.asset")
                .AddDefinition(
                    product,
                    ContentKind.StoreProduct,
                    "Assets/Content/Commerce/explorer.asset")
                .AddScienceSource(
                    phenomenon,
                    missingScience,
                    "Assets/Content/Phenomena/mirra.asset")
                .AddAddressable(
                    destination,
                    "scene.mirra",
                    "Assets/Content/Destinations/mirra.asset")
                .AddKnownAddressableKey("scene.clubhouse")
                .AddCosmeticFits(
                    cosmetic,
                    "Assets/Content/Cosmetics/launch-suit.asset",
                    ContentBodyFamily.Compact,
                    ContentBodyFamily.Average)
                .AddStoreEntitlement(
                    product,
                    missingEntitlement,
                    "Assets/Content/Commerce/explorer.asset");

            var report = ProjectContentValidator.Validate(builder.Build());

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Is.EquivalentTo(new[]
                {
                    ValidationIssueCode.MissingScienceSource,
                    ValidationIssueCode.MissingAddressableKey,
                    ValidationIssueCode.MissingCosmeticFit,
                    ValidationIssueCode.MissingStoreEntitlement,
                }));
        }

        [Test]
        public void ProjectValidator_RejectsOmittedRequiredBindings()
        {
            var phenomenon = new ContentId("phenomenon.without-source");
            var destination = new ContentId("destination.without-address");
            var cosmetic = new ContentId("cosmetic.without-fits");
            var product = new ContentId("product.without-entitlement");
            var builder = new ProjectContentIndexBuilder()
                .AddDefinition(phenomenon, ContentKind.Phenomenon, "phenomenon.asset")
                .AddDefinition(destination, ContentKind.Destination, "destination.asset")
                .AddDefinition(cosmetic, ContentKind.Cosmetic, "cosmetic.asset")
                .AddDefinition(product, ContentKind.StoreProduct, "product.asset");

            var report = ProjectContentValidator.Validate(builder.Build());

            Assert.That(report.IsValid, Is.False);
            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Is.EquivalentTo(new[]
                {
                    ValidationIssueCode.MissingScienceSource,
                    ValidationIssueCode.MissingAddressableKey,
                    ValidationIssueCode.MissingCosmeticFit,
                    ValidationIssueCode.MissingStoreEntitlement,
                }));
        }

        [Test]
        public void ProjectValidator_AcceptsCompleteCrossSystemIndex()
        {
            var mission = new ContentId("mission.mirra");
            var nextMission = new ContentId("mission.koro-vesper");
            var dialogue = new ContentId("dialogue.mirra-arrival");
            var phenomenon = new ContentId("phenomenon.mirra-terminator");
            var scienceSource = new ContentId("science-source.nasa-mirra-model");
            var destination = new ContentId("destination.mirra");
            var cosmetic = new ContentId("cosmetic.launch-suit");
            var product = new ContentId("product.explorer-edition");
            var entitlement = new ContentId("entitlement.explorer-edition");
            var builder = new ProjectContentIndexBuilder()
                .AddDefinition(mission, ContentKind.Mission, "mission.asset")
                .AddDefinition(nextMission, ContentKind.Mission, "next.asset")
                .AddDefinition(dialogue, ContentKind.Dialogue, "dialogue.asset")
                .AddDefinition(phenomenon, ContentKind.Phenomenon, "phenomenon.asset")
                .AddDefinition(scienceSource, ContentKind.ScienceSource, "source.asset")
                .AddDefinition(destination, ContentKind.Destination, "destination.asset")
                .AddDefinition(cosmetic, ContentKind.Cosmetic, "cosmetic.asset")
                .AddDefinition(product, ContentKind.StoreProduct, "product.asset")
                .AddDefinition(entitlement, ContentKind.Entitlement, "entitlement.asset")
                .AddReference(
                    mission,
                    nextMission,
                    ContentReferenceKind.MissionLink,
                    "mission.asset")
                .AddReference(
                    mission,
                    dialogue,
                    ContentReferenceKind.DialogueReference,
                    "mission.asset")
                .AddScienceSource(phenomenon, scienceSource, "phenomenon.asset")
                .AddAddressable(destination, "scene.mirra", "destination.asset")
                .AddKnownAddressableKey("scene.mirra")
                .AddCosmeticFits(
                    cosmetic,
                    "cosmetic.asset",
                    ContentBodyFamily.Compact,
                    ContentBodyFamily.Average,
                    ContentBodyFamily.TallBroad)
                .AddStoreEntitlement(product, entitlement, "product.asset");

            var report = ProjectContentValidator.Validate(builder.Build());

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void ProjectValidator_CurrentProjectContentIsValid()
        {
            var report = ProjectContentValidator.ValidateProject();

            Assert.That(
                report.IsValid,
                Is.True,
                string.Join("\n", report.Issues.Select(issue => issue.Message)));
            Assert.That(report.Issues, Is.Empty);
        }

        public static void CreateBrokenFixtureForCli()
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<SceneCatalog>(BrokenFixturePath) !=
                    null)
                {
                    throw new InvalidOperationException(
                        $"Broken fixture already exists at '{BrokenFixturePath}'.");
                }

                var catalog = SceneCatalog.CreateForTests(
                    SceneCatalog.CurrentSchemaVersion,
                    "Frontend",
                    GameMode.Frontend,
                    new SceneCatalogEntry(
                        "destination.duplicate",
                        "scene.one",
                        GameMode.Surface),
                    new SceneCatalogEntry(
                        "destination.duplicate",
                        "scene.two",
                        GameMode.Surface));
                AssetDatabase.CreateAsset(catalog, BrokenFixturePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<SceneCatalog>(BrokenFixturePath) ==
                    null)
                {
                    throw new InvalidOperationException(
                        "The intentionally broken validation fixture did not persist.");
                }

                Debug.Log("[JSS Task 9] Intentionally broken fixture created.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void RemoveBrokenFixtureForCli()
        {
            try
            {
                if (AssetDatabase.AssetPathExists(BrokenFixturePath) &&
                    !AssetDatabase.DeleteAsset(BrokenFixturePath))
                {
                    throw new InvalidOperationException(
                        $"Could not delete broken fixture '{BrokenFixturePath}'.");
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.AssetPathExists(BrokenFixturePath))
                {
                    throw new InvalidOperationException(
                        "The intentionally broken validation fixture still exists.");
                }

                Debug.Log("[JSS Task 9] Intentionally broken fixture removed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
