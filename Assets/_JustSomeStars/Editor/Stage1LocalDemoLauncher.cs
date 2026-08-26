using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JustSomeStars.Editor
{
    [InitializeOnLoad]
    internal static class Stage1LocalDemoLauncher
    {
        private const string DemoScenePath =
            "Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity";
        private const string PendingSessionKey =
            "JustSomeStars.Stage1LocalDemo.Pending";
        private static Stage1LocalDemoRunner s_Runner;
        private static bool s_AutomatedCaptureFailed;

        static Stage1LocalDemoLauncher()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Just Some Stars/Stage 1/Play Local Demo")]
        private static void PlayFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            LaunchFromCommandLine();
        }

        public static void LaunchFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Stop Play Mode before launching the Stage 1 local demo.");
            }

            OpenProofSceneForLocalPlay();
            s_AutomatedCaptureFailed = false;
            SessionState.SetBool(PendingSessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        internal static void OpenProofSceneForLocalPlay()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
            {
                throw new InvalidOperationException(
                    "The Stage 1 proof scene is missing: " + DemoScenePath);
            }

            var scene = EditorSceneManager.OpenScene(
                DemoScenePath,
                OpenSceneMode.Single);
            if (!scene.IsValid() || scene.path != DemoScenePath)
            {
                throw new InvalidOperationException(
                    "Unity did not open the Stage 1 proof scene.");
            }
        }

        private static async void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(PendingSessionKey, false))
            {
                SessionState.SetBool(PendingSessionKey, false);
                if (s_Runner != null)
                {
                    return;
                }

                s_Runner = new Stage1LocalDemoRunner();
                try
                {
                    await s_Runner.InitializeAsync();
                }
                catch (Exception exception)
                {
                    s_AutomatedCaptureFailed = true;
                    Debug.LogException(exception);
                    EditorApplication.ExitPlaymode();
                }

                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode && s_Runner != null)
            {
                var runner = s_Runner;
                s_Runner = null;
                try
                {
                    await runner.ShutdownAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PendingSessionKey, false);
                s_Runner = null;
                if (Stage1LocalDemoRunner.ShouldExitAfterCapture)
                {
                    EditorApplication.delayCall += () =>
                        EditorApplication.Exit(
                            s_AutomatedCaptureFailed ? 1 : 0);
                }
            }
        }
    }
}
