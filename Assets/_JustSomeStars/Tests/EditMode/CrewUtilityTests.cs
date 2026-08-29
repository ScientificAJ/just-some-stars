using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using JustSomeStars.Runtime.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CrewUtilityTests
    {
        private static readonly string[] PersonalityPaths =
        {
            "Assets/_JustSomeStars/Content/Crew/Personalities/Mira.asset",
            "Assets/_JustSomeStars/Content/Crew/Personalities/Juno.asset",
            "Assets/_JustSomeStars/Content/Crew/Personalities/Kai.asset",
            "Assets/_JustSomeStars/Content/Crew/Personalities/Bea.asset",
            "Assets/_JustSomeStars/Content/Crew/Personalities/Ori.asset",
        };

        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var ownedObject in m_OwnedObjects)
            {
                if (ownedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObject);
                }
            }

            m_OwnedObjects.Clear();
        }

        [Test]
        public void MandatoryStoryAction_OutranksPersonalityObservation()
        {
            var personality = CreatePersonality(
                "crew.mira",
                CrewRole.Mira,
                CrewAttention.AtmosphereAndEvidence);
            var personalityNotice = Candidate(
                "action.notice-clouds",
                CrewActionState.Investigate,
                CrewActionPriority.Personality,
                CrewAttention.AtmosphereAndEvidence,
                1000f);
            var mandatoryRepair = Candidate(
                "action.repair-signal",
                CrewActionState.Interact,
                CrewActionPriority.MandatoryStory,
                CrewAttention.MachineryAndTools,
                0f);

            var choice = CrewUtility.Select(
                new[] { personalityNotice, mandatoryRepair },
                personality);

            Assert.That(choice.Id, Is.EqualTo(mandatoryRepair.Id));
        }

        [Test]
        public void SafetyRecovery_OutranksPersonalityAndAmbientActions()
        {
            var personality = CreatePersonality(
                "crew.kai",
                CrewRole.Kai,
                CrewAttention.TraversalAndDanger);
            var choice = CrewUtility.Select(
                new[]
                {
                    Candidate(
                        "action.race",
                        CrewActionState.Traverse,
                        CrewActionPriority.Personality,
                        CrewAttention.TraversalAndDanger,
                        500f),
                    Candidate(
                        "action.recover",
                        CrewActionState.Recover,
                        CrewActionPriority.SafetyRecovery,
                        CrewAttention.HazardsAndSignal,
                        -100f),
                    Candidate(
                        "action.wait",
                        CrewActionState.Wait,
                        CrewActionPriority.Ambient,
                        CrewAttention.None,
                        1000f),
                },
                personality);

            Assert.That(choice.State, Is.EqualTo(CrewActionState.Recover));
        }

        [Test]
        public void PersonalityAttention_BreaksEqualPriorityUtilityTies()
        {
            var mira = CreatePersonality(
                "crew.mira",
                CrewRole.Mira,
                CrewAttention.AtmosphereAndEvidence);
            var weather = Candidate(
                "action.weather",
                CrewActionState.Investigate,
                CrewActionPriority.Personality,
                CrewAttention.AtmosphereAndEvidence,
                0.2f);
            var machine = Candidate(
                "action.machine",
                CrewActionState.Investigate,
                CrewActionPriority.Personality,
                CrewAttention.MachineryAndTools,
                0.2f);

            Assert.That(
                CrewUtility.Select(new[] { machine, weather }, mira).Id,
                Is.EqualTo(weather.Id));
        }

        [Test]
        public void ApproximateUtilityTie_IsStableAcrossCandidateOrder()
        {
            var personality = CreatePersonality(
                "crew.bea",
                CrewRole.Bea,
                CrewAttention.MemoryAndWellbeing);
            var z = Candidate("action.z", CrewActionState.React,
                CrewActionPriority.Personality,
                CrewAttention.MemoryAndWellbeing, 0f);
            var a = Candidate("action.a", CrewActionState.React,
                CrewActionPriority.Personality,
                CrewAttention.MemoryAndWellbeing, -0.0000001f);

            Assert.That(CrewUtility.Select(new[] { z, a }, personality).Id,
                Is.EqualTo(a.Id));
            Assert.That(CrewUtility.Select(new[] { a, z }, personality).Id,
                Is.EqualTo(a.Id));
        }

        [Test]
        public void NearTieTotalOrdering_IsStableAcrossAllThreeCandidatePermutations()
        {
            var personality = CreatePersonality(
                "crew.bea",
                CrewRole.Bea,
                CrewAttention.MemoryAndWellbeing);
            var a = Candidate("action.a", CrewActionState.React,
                CrewActionPriority.Personality, CrewAttention.None, 0f);
            var b = Candidate("action.b", CrewActionState.React,
                CrewActionPriority.Personality, CrewAttention.None, 0.00000075f);
            var c = Candidate("action.c", CrewActionState.React,
                CrewActionPriority.Personality, CrewAttention.None, 0.0000015f);
            var permutations = new[]
            {
                new[] { a, b, c }, new[] { a, c, b },
                new[] { b, a, c }, new[] { b, c, a },
                new[] { c, a, b }, new[] { c, b, a },
            };

            foreach (var permutation in permutations)
            {
                Assert.That(CrewUtility.Select(permutation, personality).Id,
                    Is.EqualTo(c.Id));
            }
        }

        [Test]
        public void Director_SelectsExactlyTwoHumansPlusOriDeterministically()
        {
            var brains = new[]
            {
                CreateBrain("crew.mira", CrewRole.Mira,
                    CrewAttention.AtmosphereAndEvidence),
                CreateBrain("crew.juno", CrewRole.Juno,
                    CrewAttention.MachineryAndTools),
                CreateBrain("crew.kai", CrewRole.Kai,
                    CrewAttention.TraversalAndDanger),
                CreateBrain("crew.bea", CrewRole.Bea,
                    CrewAttention.MemoryAndWellbeing),
                CreateBrain("crew.ori", CrewRole.Ori,
                    CrewAttention.HazardsAndSignal),
            };
            var director = new CrewDirector(
                new DialogueTokenArbiter(),
                new InteractionReservationService(),
                decisionIntervalSeconds: 0.2f);
            var destinationFit = new Dictionary<ContentId, float>
            {
                [new ContentId("crew.mira")] = 1f,
                [new ContentId("crew.juno")] = 0.5f,
                [new ContentId("crew.kai")] = 0.5f,
                [new ContentId("crew.bea")] = 0.2f,
                [new ContentId("crew.ori")] = -100f,
            };

            var team = director.SelectExpeditionTeam(brains, destinationFit);

            Assert.That(team.Count, Is.EqualTo(3));
            Assert.That(team.Count(member => member.Role == CrewRole.Ori), Is.EqualTo(1));
            Assert.That(team.Count(member => member.Role != CrewRole.Ori), Is.EqualTo(2));
            Assert.That(team.Select(member => member.ActorId.Value), Is.EqualTo(
                new[] { "crew.mira", "crew.juno", "crew.ori" }));
        }

        [Test]
        public void TraversalGraph_UsesOnlyDeclared2DDepthTransitions()
        {
            var graph = ScriptableObject.CreateInstance<TraversalGraph2D>();
            m_OwnedObjects.Add(graph);
            graph.Configure(
                new[]
                {
                    Node("node.start", new Vector2(0f, 0f),
                        InteractionDepthBand.Gameplay, "node.ramp"),
                    Node("node.ramp", new Vector2(1f, 0.5f),
                        InteractionDepthBand.Gameplay, "node.overlook"),
                    Node("node.overlook", new Vector2(2f, 1f),
                        InteractionDepthBand.Midground),
                },
                new[]
                {
                    new TraversalDepthTransition(
                        InteractionDepthBand.Gameplay,
                        InteractionDepthBand.Midground),
                });

            var path = graph.FindPath(
                new ContentId("node.start"),
                new ContentId("node.overlook"));

            Assert.That(path.Select(node => node.Id.Value), Is.EqualTo(
                new[] { "node.start", "node.ramp", "node.overlook" }));
            var invalid = ScriptableObject.CreateInstance<TraversalGraph2D>();
            m_OwnedObjects.Add(invalid);
            Assert.Throws<InvalidOperationException>(() => invalid.Configure(
                new[]
                {
                    Node("node.a", Vector2.zero,
                        InteractionDepthBand.Gameplay, "node.b"),
                    Node("node.b", Vector2.right,
                        InteractionDepthBand.Foreground),
                },
                Array.Empty<TraversalDepthTransition>()));
        }

        [Test]
        public void Director_CinematicControlSuspendsEveryDecisionTick()
        {
            var brains = new[]
            {
                CreateBrain("crew.mira", CrewRole.Mira,
                    CrewAttention.AtmosphereAndEvidence),
                CreateBrain("crew.juno", CrewRole.Juno,
                    CrewAttention.MachineryAndTools),
                CreateBrain("crew.ori", CrewRole.Ori,
                    CrewAttention.HazardsAndSignal),
            };
            var candidates = brains.ToDictionary(
                brain => brain.ActorId,
                brain => (IReadOnlyList<CrewActionCandidate>)new[]
                {
                    Candidate(
                        $"action.{brain.Role.ToString().ToLowerInvariant()}.follow",
                        CrewActionState.Follow,
                        CrewActionPriority.Ambient,
                        CrewAttention.None,
                        0f),
                });
            var director = new CrewDirector(
                new DialogueTokenArbiter(),
                new InteractionReservationService(),
                decisionIntervalSeconds: 0.2f);
            director.SetCinematicControl(true);

            var choices = director.Tick(brains, candidates, nowSeconds: 0d);

            Assert.That(choices, Is.Empty);
            Assert.That(brains.All(brain =>
                brain.CurrentState == CrewActionState.Cinematic), Is.True);
            Assert.That(brains.All(brain => brain.CurrentAction == null), Is.True);
        }

        [Test]
        public void Director_EnforcesActiveTeamShapeAndDecisionCadence()
        {
            var brains = new[]
            {
                CreateBrain("crew.mira", CrewRole.Mira,
                    CrewAttention.AtmosphereAndEvidence),
                CreateBrain("crew.juno", CrewRole.Juno,
                    CrewAttention.MachineryAndTools),
                CreateBrain("crew.ori", CrewRole.Ori,
                    CrewAttention.HazardsAndSignal),
            };
            var candidates = brains.ToDictionary(
                brain => brain.ActorId,
                brain => (IReadOnlyList<CrewActionCandidate>)new[]
                {
                    Candidate($"action.{brain.Role}.follow",
                        CrewActionState.Follow,
                        CrewActionPriority.Ambient,
                        CrewAttention.None,
                        0f),
                });
            var director = new CrewDirector(
                new DialogueTokenArbiter(),
                new InteractionReservationService(),
                decisionIntervalSeconds: 0.2f);

            using var first = new DecisionBatch(
                director.Tick(brains, candidates, nowSeconds: 0d));
            using var throttled = new DecisionBatch(
                director.Tick(brains, candidates, nowSeconds: 0.1d));
            using var second = new DecisionBatch(
                director.Tick(brains, candidates, nowSeconds: 0.2d));

            Assert.That(first.Decisions, Has.Count.EqualTo(3));
            Assert.That(throttled.Decisions, Is.Empty);
            Assert.That(second.Decisions, Has.Count.EqualTo(3));
            Assert.Throws<InvalidOperationException>(() => director.Tick(
                brains.Take(2).ToArray(), candidates, nowSeconds: 0.4d));
        }

        [Test]
        public void AuthoredPersonalities_HaveExactRolesAndPrimaryAttention()
        {
            var expected = new[]
            {
                ("crew.mira", CrewRole.Mira,
                    CrewAttention.AtmosphereAndEvidence),
                ("crew.juno", CrewRole.Juno,
                    CrewAttention.MachineryAndTools),
                ("crew.kai", CrewRole.Kai,
                    CrewAttention.TraversalAndDanger),
                ("crew.bea", CrewRole.Bea,
                    CrewAttention.MemoryAndWellbeing),
                ("crew.ori", CrewRole.Ori,
                    CrewAttention.HazardsAndSignal),
            };

            for (var index = 0; index < PersonalityPaths.Length; index++)
            {
                var personality = AssetDatabase.LoadAssetAtPath<CrewPersonality>(
                    PersonalityPaths[index]);
                Assert.That(personality, Is.Not.Null, PersonalityPaths[index]);
                personality.ValidateOrThrow();
                Assert.That(personality.StableId.Value, Is.EqualTo(expected[index].Item1));
                Assert.That(personality.Role, Is.EqualTo(expected[index].Item2));
                Assert.That(personality.PrimaryAttention,
                    Is.EqualTo(expected[index].Item3));
                Assert.That(personality.GetAttentionWeight(
                    personality.PrimaryAttention), Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void DecisionTicks_TwoCompanionsPlusOriStayWithinAuthoredBudget()
        {
            var brains = new[]
            {
                CreateBrain("crew.mira", CrewRole.Mira,
                    CrewAttention.AtmosphereAndEvidence),
                CreateBrain("crew.juno", CrewRole.Juno,
                    CrewAttention.MachineryAndTools),
                CreateBrain("crew.ori", CrewRole.Ori,
                    CrewAttention.HazardsAndSignal),
            };
            var candidates = new[]
            {
                Candidate("action.follow", CrewActionState.Follow,
                    CrewActionPriority.Ambient, CrewAttention.None, 0.1f),
                Candidate("action.observe", CrewActionState.Investigate,
                    CrewActionPriority.Personality,
                    CrewAttention.AtmosphereAndEvidence, 0.5f),
                Candidate("action.repair", CrewActionState.Interact,
                    CrewActionPriority.Personality,
                    CrewAttention.MachineryAndTools, 0.4f),
                Candidate("action.scan", CrewActionState.Investigate,
                    CrewActionPriority.Personality,
                    CrewAttention.HazardsAndSignal, 0.3f),
            };
            var stopwatch = Stopwatch.StartNew();
            for (var tick = 0; tick < 10000; tick++)
            {
                foreach (var brain in brains)
                {
                    brain.Decide(candidates, cinematicControl: false);
                }
            }

            stopwatch.Stop();
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000),
                "Thirty thousand authored utility decisions exceeded 1,000 ms.");
        }

        private CrewBrain CreateBrain(
            string id,
            CrewRole role,
            CrewAttention primary)
        {
            return new CrewBrain(CreatePersonality(id, role, primary));
        }

        private CrewPersonality CreatePersonality(
            string id,
            CrewRole role,
            CrewAttention primary)
        {
            var personality = ScriptableObject.CreateInstance<CrewPersonality>();
            m_OwnedObjects.Add(personality);
            personality.Configure(
                id,
                role.ToString(),
                role,
                primary,
                Enum.GetValues(typeof(CrewAttention))
                    .Cast<CrewAttention>()
                    .Where(attention => attention != CrewAttention.None)
                    .Select(attention => new CrewAttentionWeight(
                        attention,
                        attention == primary ? 1f : 0.2f))
                    .ToArray());
            return personality;
        }

        private static CrewActionCandidate Candidate(
            string id,
            CrewActionState state,
            CrewActionPriority priority,
            CrewAttention attention,
            float utility)
        {
            return new CrewActionCandidate(
                id,
                state,
                priority,
                attention,
                utility,
                Vector2.zero,
                InteractionDepthBand.Gameplay,
                requiresDialogueToken: state == CrewActionState.Speak ||
                    state == CrewActionState.Conversation);
        }

        private static TraversalNode2D Node(
            string id,
            Vector2 position,
            InteractionDepthBand depth,
            params string[] neighbors)
        {
            return new TraversalNode2D(id, position, depth, neighbors);
        }
    }
}
