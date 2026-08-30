using System;
using System.IO;
using UnityEngine;

namespace JustSomeStars.Editor.Build
{
    public static class BuildCli
    {
        public static void BuildAndroidInternal()
        {
            CreateOrchestrator().Run(BuildTargetKind.AndroidInternal);
        }

        public static void BuildGooglePlayRelease()
        {
            CreateOrchestrator().Run(BuildTargetKind.GooglePlay);
        }

        public static void BuildGalaxyRelease()
        {
            CreateOrchestrator().Run(BuildTargetKind.Galaxy);
        }

        public static void BuildTask17FlightEvidence()
        {
            CreateOrchestrator().Run(
                BuildTargetKind.AndroidInternal,
                new BuildOrchestrator.BuildInvocationOverride(
                    "Builds/Task17Flight/JustSomeStars-task17-flight.apk",
                    new[] { "JSS_TASK17_FLIGHT_EVIDENCE" }));
        }

        private static BuildOrchestrator CreateOrchestrator()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException(
                    "Unity project root could not be resolved from Application.dataPath.");
            }

            return new BuildOrchestrator(
                projectRoot,
                new AndroidBuildTargetGuard(),
                new SystemBuildInputReader(projectRoot),
                new UnityAndroidBuildStateFactory(),
                new AddressablesBuilder(),
                new BuildSceneLeaseFactory(projectRoot),
                new UnityPlayerBuilder(),
                new SigningSecretScrubber(projectRoot));
        }
    }
}
