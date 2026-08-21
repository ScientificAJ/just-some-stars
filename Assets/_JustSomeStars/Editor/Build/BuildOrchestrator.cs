using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace JustSomeStars.Editor.Build
{
    internal sealed class BuildOrchestrator
    {
        private readonly string m_ProjectRoot;
        private readonly IBuildTargetGuard m_TargetGuard;
        private readonly IBuildInputReader m_InputReader;
        private readonly IAndroidBuildStateFactory m_StateFactory;
        private readonly IAddressablesBuilder m_AddressablesBuilder;
        private readonly IBuildSceneLeaseFactory m_SceneLeaseFactory;
        private readonly IPlayerBuilder m_PlayerBuilder;
        private readonly ISigningSecretScrubber m_SigningSecretScrubber;

        public BuildOrchestrator(
            string projectRoot,
            IBuildTargetGuard targetGuard,
            IBuildInputReader inputReader,
            IAndroidBuildStateFactory stateFactory,
            IAddressablesBuilder addressablesBuilder,
            IBuildSceneLeaseFactory sceneLeaseFactory,
            IPlayerBuilder playerBuilder,
            ISigningSecretScrubber signingSecretScrubber)
        {
            m_ProjectRoot = projectRoot ??
                throw new ArgumentNullException(nameof(projectRoot));
            m_TargetGuard = targetGuard ??
                throw new ArgumentNullException(nameof(targetGuard));
            m_InputReader = inputReader ??
                throw new ArgumentNullException(nameof(inputReader));
            m_StateFactory = stateFactory ??
                throw new ArgumentNullException(nameof(stateFactory));
            m_AddressablesBuilder = addressablesBuilder ??
                throw new ArgumentNullException(nameof(addressablesBuilder));
            m_SceneLeaseFactory = sceneLeaseFactory ??
                throw new ArgumentNullException(nameof(sceneLeaseFactory));
            m_PlayerBuilder = playerBuilder ??
                throw new ArgumentNullException(nameof(playerBuilder));
            m_SigningSecretScrubber = signingSecretScrubber ??
                throw new ArgumentNullException(nameof(signingSecretScrubber));
        }

        public void Run(BuildTargetKind kind)
        {
            var definition = BuildTargetDefinition.Resolve(kind);
            var artifactTransaction = BuildArtifactTransaction.Begin(
                m_ProjectRoot,
                definition.OutputPath);
            IAndroidBuildState state = null;
            BuildInputs inputs = null;
            IBuildSceneLease sceneLease = null;
            var settingsMutationAttempted = false;
            var signingMutationAttempted = false;
            var cleanupFailures = new List<Exception>();
            Exception primaryFailure = null;

            try
            {
                m_TargetGuard.EnsureReady();
                state = m_StateFactory.Capture();
                inputs = m_InputReader.Read(kind);
                m_SigningSecretScrubber.ScrubAndVerify(inputs.SigningCredentials);

                var configuration = BuildConfiguration.Resolve(kind, inputs.BuildNumber);
                BuildPlayerOptionsFactory.ValidatePersistentDefineSymbols(
                    state.PersistentDefineSymbols);

                settingsMutationAttempted = true;
                state.ApplySettings(configuration);

                sceneLease = m_SceneLeaseFactory.Acquire();
                var playerOptions = BuildPlayerOptionsFactory.Create(
                    configuration,
                    artifactTransaction.StagingPath,
                    sceneLease.ScenePaths,
                    state.PersistentDefineSymbols);
                Debug.Log(
                    "[JSS Build] Configured " + configuration.Kind +
                    ": package=" + configuration.PackageId +
                    ", versionCode=" + configuration.VersionCode +
                    ", canonicalOutput=" + configuration.OutputPath +
                    ", appBundle=" + configuration.BuildAppBundle +
                    ", development=" + configuration.IsDevelopmentBuild +
                    ", invocationDefines=" +
                    string.Join(";", configuration.DefineSymbols) + ".");

                m_AddressablesBuilder.Build(configuration, playerOptions);

                signingMutationAttempted = true;
                try
                {
                    state.ApplySigning(configuration, inputs.SigningCredentials);
                    m_PlayerBuilder.Build(configuration, playerOptions);
                }
                finally
                {
                    if (signingMutationAttempted)
                    {
                        TryCleanup(
                            state.RestoreSigningAndVerify,
                            cleanupFailures);
                        signingMutationAttempted = false;
                    }
                }

                var expectedExtension = Path.GetExtension(configuration.OutputPath);
                var artifactSize = BuildArtifactValidator.Validate(
                    artifactTransaction.StagingPath,
                    expectedExtension);
                Debug.Log(
                    "[JSS Build] Verified staged " + configuration.Kind +
                    " artifact (" + artifactSize + " bytes).");
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
            }
            finally
            {
                if (signingMutationAttempted && state != null)
                {
                    TryCleanup(state.RestoreSigningAndVerify, cleanupFailures);
                    signingMutationAttempted = false;
                }

                if (sceneLease != null)
                {
                    TryCleanup(sceneLease.CleanupAndVerify, cleanupFailures);
                }

                if (settingsMutationAttempted && state != null)
                {
                    TryCleanup(state.RestoreSettingsAndVerify, cleanupFailures);
                }

                if (inputs != null)
                {
                    TryCleanup(
                        () => m_SigningSecretScrubber.ScrubAndVerify(
                            inputs.SigningCredentials),
                        cleanupFailures);
                }
            }

            if (primaryFailure == null && cleanupFailures.Count == 0)
            {
                try
                {
                    artifactTransaction.Publish();
                }
                catch (Exception exception)
                {
                    primaryFailure = exception;
                }
            }

            try
            {
                artifactTransaction.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }

            ThrowIfFailed(primaryFailure, cleanupFailures);
        }

        private static void TryCleanup(
            Action cleanup,
            ICollection<Exception> cleanupFailures)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(exception);
            }
        }

        private static void ThrowIfFailed(
            Exception primaryFailure,
            IReadOnlyCollection<Exception> cleanupFailures)
        {
            if (primaryFailure == null && cleanupFailures.Count == 0)
            {
                return;
            }

            if (primaryFailure != null && cleanupFailures.Count == 0)
            {
                ExceptionDispatchInfo.Capture(primaryFailure).Throw();
                throw new InvalidOperationException("Unreachable build failure path.");
            }

            var failures = new List<Exception>();
            if (primaryFailure != null)
            {
                failures.Add(primaryFailure);
            }

            failures.AddRange(cleanupFailures);
            throw new AggregateException(
                "The Android build failed and one or more cleanup operations also failed.",
                failures);
        }
    }
}
