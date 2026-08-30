using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Flight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class FlightModel2DTests
    {
        private static readonly FlightLaneTransition[] Transitions =
        {
            new FlightLaneTransition(0, 1),
            new FlightLaneTransition(1, 0),
            new FlightLaneTransition(1, 2),
            new FlightLaneTransition(2, 1),
        };

        [Test]
        public void FocusedFlightEvidenceBuild_RoutesThroughTheProductionFlightMode()
        {
            var policyType = typeof(GameBootstrap).Assembly.GetType(
                "JustSomeStars.Runtime.Core.InitialExperiencePolicy");
            Assert.That(policyType, Is.Not.Null);
            var resolveDestination = policyType.GetMethod(
                "ResolveDestinationForInvocation",
                BindingFlags.Static | BindingFlags.NonPublic);
            var resolveMode = policyType.GetMethod(
                "ResolveModeForInvocation",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveDestination, Is.Not.Null);
            Assert.That(resolveMode, Is.Not.Null);

            Assert.That(
                resolveDestination.Invoke(null, new object[] { true, true }),
                Is.EqualTo("Task17FlightGraybox"));
            Assert.That(
                resolveMode.Invoke(null, new object[] { true, true }),
                Is.EqualTo(GameMode.Flight));
            Assert.That(
                resolveDestination.Invoke(null, new object[] { false, false }),
                Is.EqualTo("Frontend"));
        }

        [Test]
        public void FixedStepTrajectory_IsRepeatableAndPredictionMatchesLiveState()
        {
            var model = CreateModel(AssistLevel.Balanced);
            var input = new FlightInputFrame(new Vector2(0.6f, 0.2f), true, false);
            var initial = new FlightState(new Vector2(-8f, 0f), Vector2.right, 1);

            var first = initial;
            var second = initial;
            for (var index = 0; index < 120; index++)
            {
                first = model.Step(first, input, 1f / 60f);
                second = model.Step(second, input, 1f / 60f);
            }

            Assert.That(first.Position, Is.EqualTo(second.Position));
            Assert.That(first.Velocity, Is.EqualTo(second.Velocity));
            var prediction = model.Predict(initial, input, 1f / 60f, 120);
            Assert.That(prediction.Count, Is.EqualTo(120));
            Assert.That(prediction[^1].Position, Is.EqualTo(first.Position));
            Assert.That(prediction[^1].Velocity, Is.EqualTo(first.Velocity));
            Assert.That(initial.Position, Is.EqualTo(new Vector2(-8f, 0f)));
        }

        [Test]
        public void BoostCoastBrakeAndDrift_PreserveReadableMomentumContracts()
        {
            var model = CreateModel(AssistLevel.Balanced);
            var state = new FlightState(Vector2.zero, Vector2.zero, 1);
            state = model.Step(
                state,
                new FlightInputFrame(Vector2.right, true, false),
                0.25f);
            var boostedSpeed = state.Velocity.magnitude;
            Assert.That(boostedSpeed, Is.GreaterThan(1f));

            var coasted = model.Step(
                state,
                new FlightInputFrame(Vector2.zero, false, false),
                0.25f);
            Assert.That(coasted.Velocity.magnitude, Is.GreaterThan(boostedSpeed * 0.9f));

            var braked = model.Step(
                coasted,
                new FlightInputFrame(Vector2.zero, false, true),
                0.25f);
            Assert.That(braked.Velocity.magnitude, Is.LessThan(coasted.Velocity.magnitude));

            var drifted = model.Step(
                coasted,
                new FlightInputFrame(Vector2.up, false, true),
                0.25f);
            Assert.That(drifted.Velocity.magnitude, Is.GreaterThan(braked.Velocity.magnitude));
            Assert.That(drifted.Velocity.y, Is.GreaterThan(coasted.Velocity.y));
        }

        [Test]
        public void InvalidConfiguration_FailsClosedAndBoundaryMathRemainsFinite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlightModel2D(
                new FlightSimulationConfig(
                    new Rect(-1f, -1f, 2f, 2f),
                    float.NaN,
                    1f,
                    0f,
                    1f,
                    1f,
                    1f,
                    1f,
                    1f),
                AssistLevel.Balanced,
                Transitions,
                Array.Empty<GravityAssistSample>()));

            var model = new FlightModel2D(
                FlightSimulationConfig.Default,
                AssistLevel.Guided,
                Transitions,
                new[] { new GravityAssistSample(Vector2.zero, 3f, 4f, 3f, 1) });
            var center = model.Step(
                new FlightState(Vector2.zero, Vector2.zero, 1),
                new FlightInputFrame(Vector2.zero, false, false),
                1f / 60f);
            Assert.That(float.IsFinite(center.Position.x), Is.True);
            Assert.That(float.IsFinite(center.Position.y), Is.True);
            Assert.That(float.IsFinite(center.Velocity.x), Is.True);
            Assert.That(float.IsFinite(center.Velocity.y), Is.True);
        }

        [Test]
        public void AssistProfiles_AreMonotonicWithoutChangingStoryAccess()
        {
            var guided = FlightAssist.For(AssistLevel.Guided);
            var balanced = FlightAssist.For(AssistLevel.Balanced);
            var ace = FlightAssist.For(AssistLevel.Ace);

            Assert.That(guided.SteeringCorrection, Is.GreaterThan(balanced.SteeringCorrection));
            Assert.That(balanced.SteeringCorrection, Is.GreaterThan(ace.SteeringCorrection));
            Assert.That(guided.RouteCorrection, Is.GreaterThan(balanced.RouteCorrection));
            Assert.That(balanced.RouteCorrection, Is.GreaterThan(ace.RouteCorrection));
            Assert.That(guided.SafeMargin, Is.GreaterThan(balanced.SafeMargin));
            Assert.That(balanced.SafeMargin, Is.GreaterThan(ace.SafeMargin));
            Assert.That(guided.StoryAccessMask, Is.EqualTo(balanced.StoryAccessMask));
            Assert.That(balanced.StoryAccessMask, Is.EqualTo(ace.StoryAccessMask));
        }

        [Test]
        public void RouteEnvelope_IsBoundedAndGuidedCorrectionIsStrongest()
        {
            var initial = new FlightState(
                new Vector2(10.5f, 3.8f),
                new Vector2(1f, 1f),
                1);
            var idle = new FlightInputFrame(Vector2.zero, false, false);
            var guided = CreateModel(AssistLevel.Guided).Step(initial, idle, 0.1f);
            var ace = CreateModel(AssistLevel.Ace).Step(initial, idle, 0.1f);

            AssertInsideInclusive(
                FlightSimulationConfig.Default.RouteEnvelope,
                guided.Position);
            AssertInsideInclusive(
                FlightSimulationConfig.Default.RouteEnvelope,
                ace.Position);
            Assert.That(guided.Velocity.x, Is.LessThan(ace.Velocity.x));
            Assert.That(guided.Velocity.y, Is.LessThan(ace.Velocity.y));
        }

        [Test]
        public void DepthLanes_AllowOnlyDeclaredTransitionsAndOwnHazardsDeterministically()
        {
            var model = CreateModel(AssistLevel.Balanced);
            Assert.That(model.IsLaneTransitionDeclared(0, 1), Is.True);
            Assert.That(model.IsLaneTransitionDeclared(1, 2), Is.True);
            Assert.That(model.IsLaneTransitionDeclared(0, 2), Is.False);
            Assert.That(FlightModel2D.IsHazardActiveForLane(2, 2), Is.True);
            Assert.That(FlightModel2D.IsHazardActiveForLane(1, 2), Is.False);

            var state = new FlightState(Vector2.zero, Vector2.right, 0);
            var illegal = model.Step(
                state,
                new FlightInputFrame(Vector2.zero, false, false, 2),
                1f / 60f);
            var legal = model.Step(
                state,
                new FlightInputFrame(Vector2.zero, false, false, 1),
                1f / 60f);
            Assert.That(illegal.Lane, Is.EqualTo(0));
            Assert.That(legal.Lane, Is.EqualTo(1));
        }

        [Test]
        public void GravityAssist_DeflectsTrajectoryAndPredictionUsesTheSameRules()
        {
            var gravity = new GravityAssistSample(Vector2.zero, 4f, 3f, 2f, 1);
            var model = new FlightModel2D(
                FlightSimulationConfig.Default,
                AssistLevel.Balanced,
                Transitions,
                new[] { gravity });
            var state = new FlightState(new Vector2(-3f, 1f), Vector2.right * 2f, 1);
            var noInput = new FlightInputFrame(Vector2.zero, false, false);
            var live = model.Step(state, noInput, 0.25f);
            var predicted = model.Predict(state, noInput, 0.25f, 1).Single();

            Assert.That(live.Velocity.y, Is.Not.EqualTo(state.Velocity.y).Within(0.0001f));
            Assert.That(predicted.Position, Is.EqualTo(live.Position));
            Assert.That(predicted.Velocity, Is.EqualTo(live.Velocity));
        }

        [Test]
        public void PlayerShipAssets_AreLayeredOriginal2DAndContainNoShipping3DDependency()
        {
            const string root = "Assets/_JustSomeStars/Art/2D/Ship/PlayerShip";
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(
                root + "/PlayerShipMaster.png"), Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(root, "PlayerShipLayers.json")), Is.True);
            Assert.That(Directory.GetFiles(root, "*.fbx", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetFiles(root, "*.blend", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetFiles(root, "*.obj", SearchOption.AllDirectories), Is.Empty);

            var manifest = File.ReadAllText(Path.Combine(root, "PlayerShipLayers.json"));
            foreach (var token in new[]
            {
                "engine", "landing", "door", "cockpitSeat", "damage", "cosmetic"
            })
            {
                Assert.That(manifest, Does.Contain(token));
            }

            foreach (var contract in new[]
            {
                (Path: root + "/PlayerShipEngineAtlas.png", Frames: 4),
                (Path: root + "/PlayerShipLandingAtlas.png", Frames: 3),
                (Path: root + "/PlayerShipDoorAtlas.png", Frames: 3),
            })
            {
                var importer = AssetImporter.GetAtPath(contract.Path) as TextureImporter;
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
                Assert.That(
                    AssetDatabase.LoadAllAssetsAtPath(contract.Path).OfType<Sprite>().ToArray(),
                    Has.Length.EqualTo(contract.Frames),
                    contract.Path + " must expose every authored frame to the player.");
            }
        }

        [Test]
        public void ProductionFlightAssets_ContainTheRealPrefabAndNinetySecondRoute()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_JustSomeStars/Prefabs/Ship/PlayerShip2D.prefab");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/_JustSomeStars/Scenes/Benchmarks/Task17FlightGraybox.unity");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(scene, Is.Not.Null);

            var presentation = prefab.GetComponent<PlayerShipPresentation2D>();
            Assert.That(presentation, Is.Not.Null);
            var landing = prefab.GetComponent<LandingSequence>();
            Assert.That(landing, Is.Not.Null);
            var serializedLanding = new SerializedObject(landing);
            Assert.That(
                serializedLanding.FindProperty("presentation").objectReferenceValue,
                Is.SameAs(presentation),
                "The production landing route must own the authored ship sequence.");
            Assert.That(
                serializedLanding.FindProperty("motor").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<FlightMotor2D>()),
                "The production landing route must lock and restore its motor.");
            var serializedPresentation = new SerializedObject(presentation);
            Assert.That(
                serializedPresentation.FindProperty("engineFrames").arraySize,
                Is.EqualTo(4));
            Assert.That(
                serializedPresentation.FindProperty("landingFrames").arraySize,
                Is.EqualTo(3));
            Assert.That(
                serializedPresentation.FindProperty("doorFrames").arraySize,
                Is.EqualTo(3));
        }

        private static FlightModel2D CreateModel(AssistLevel level)
        {
            return new FlightModel2D(
                FlightSimulationConfig.Default,
                level,
                Transitions,
                Array.Empty<GravityAssistSample>());
        }

        private static void AssertInsideInclusive(Rect bounds, Vector2 position)
        {
            Assert.That(position.x, Is.InRange(bounds.xMin, bounds.xMax));
            Assert.That(position.y, Is.InRange(bounds.yMin, bounds.yMax));
        }
    }
}
