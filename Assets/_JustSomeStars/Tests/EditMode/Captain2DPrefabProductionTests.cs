using System;
using System.IO;
using System.Linq;
using JustSomeStars.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class Captain2DPrefabProductionTests
    {
        private const string PrefabPath =
            "Assets/_JustSomeStars/Prefabs/Characters/Captain2D.prefab";
        private const string ScenePath =
            "Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity";

        [Test]
        public void CaptainPrefab_IsTheSingleProduction2DCharacterAuthority()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                Is.Not.Null, PrefabPath);
            Assert.That(File.Exists(PrefabPath + ".meta"), Is.True);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Assert.That(root.name, Is.EqualTo("Captain2D"));
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(root.GetComponentsInChildren<Rigidbody2D>(true).Length,
                    Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<CapsuleCollider2D>(true).Length,
                    Is.EqualTo(1));
                RequireExactlyOne(root, "JustSomeStars.Runtime.Player.SurfaceMotor2D");
                RequireExactlyOne(root,
                    "JustSomeStars.Runtime.Player.BodySpriteCalibration");
                RequireExactlyOne(root,
                    "JustSomeStars.Runtime.Player.SurfaceRecovery2D");
                RequireExactlyOne(root,
                    "JustSomeStars.Runtime.Animation2D.LayeredCharacterRenderer");
                RequireExactlyOne(root,
                    "JustSomeStars.Runtime.Animation2D.MirraCaptainMotionPresenter");

                var calibration = root.GetComponent<BodySpriteCalibration>();
                Assert.That(calibration, Is.Not.Null);
                Assert.That(calibration.Profiles.Count, Is.EqualTo(3));
                foreach (var profile in calibration.Profiles)
                {
                    Assert.That(profile.VisualScale.x,
                        Is.EqualTo(profile.VisualScale.y).Within(0.0001f),
                        profile.Family.ToString());
                    Assert.That(profile.VisualScale.y,
                        Is.GreaterThanOrEqualTo(1.90f),
                        profile.Family +
                        " must occupy a deliberate foreground share of the " +
                        "720p gameplay frame without changing its collider.");
                }

                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(renderers.Count(renderer =>
                    renderer.gameObject.name.StartsWith("Layer", StringComparison.Ordinal)),
                    Is.EqualTo(5));
                Assert.That(renderers.Count(renderer =>
                    renderer.gameObject.name == "ContactShadow"), Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Animator>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                    Is.Empty);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true),
                    Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void MirraBenchmark_UsesTheExactCaptainPrefabAndAuthoredCameraProfiles()
        {
            var priorSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var captain = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "Captain");
                var source = PrefabUtility.GetCorrespondingObjectFromSource(
                    captain.gameObject);
                Assert.That(source, Is.Not.Null,
                    "The playable Captain must be a production prefab instance.");
                Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(PrefabPath));
                Assert.That(captain.localScale, Is.EqualTo(Vector3.one));

                var camera = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Single(item => item.GetComponent(Type.GetType(
                        "JustSomeStars.Runtime.Player.CompositionCamera2D, " +
                        "JustSomeStars.Runtime")) != null);
                var controller = camera.GetComponent(Type.GetType(
                    "JustSomeStars.Runtime.Player.CompositionCamera2D, " +
                    "JustSomeStars.Runtime"));
                var serialized = new SerializedObject(controller);
                var profiles = serialized.FindProperty("profiles");
                Assert.That(profiles, Is.Not.Null);
                Assert.That(profiles.arraySize,
                    Is.EqualTo(Enum.GetValues(typeof(
                        JustSomeStars.Runtime.Core.GameCameraPolicy)).Length));
            }
            finally
            {
                if (priorSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(priorSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }

        private static void RequireExactlyOne(GameObject root, string fullName)
        {
            var type = Type.GetType(fullName + ", JustSomeStars.Runtime");
            Assert.That(type, Is.Not.Null, fullName);
            Assert.That(root.GetComponentsInChildren(type, true).Length,
                Is.EqualTo(1), fullName);
        }
    }
}
