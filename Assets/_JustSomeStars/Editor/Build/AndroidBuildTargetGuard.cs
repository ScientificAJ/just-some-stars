using System;
using UnityEditor;

namespace JustSomeStars.Editor.Build
{
    internal interface IBuildTargetGuard
    {
        void EnsureReady();
    }

    internal sealed class AndroidBuildTargetGuard : IBuildTargetGuard
    {
        public void EnsureReady()
        {
            Validate(
                BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Android,
                    BuildTarget.Android),
                EditorUserBuildSettings.activeBuildTarget);
        }

        public static void Validate(
            bool androidSupportInstalled,
            BuildTarget activeBuildTarget)
        {
            if (!androidSupportInstalled)
            {
                throw new InvalidOperationException(
                    "Android Build Support is not installed for this Unity editor.");
            }

            if (activeBuildTarget != BuildTarget.Android)
            {
                throw new InvalidOperationException(
                    "The active Unity build target is not Android. " +
                    "Start every Just Some Stars build with the exact CLI option " +
                    "'-buildTarget Android'; in-process target switching is not supported.");
            }
        }
    }
}
