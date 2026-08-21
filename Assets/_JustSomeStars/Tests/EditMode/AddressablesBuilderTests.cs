using System;
using JustSomeStars.Editor.Build;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class AddressablesBuilderTests
    {
        [Test]
        public void CreateInput_PropagatesPlayerDefinesAndAddsOnlyAddressablesOptIn()
        {
            var settings = ScriptableObject.CreateInstance<AddressableAssetSettings>();
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            var playerOptions = new BuildPlayerOptions
            {
                target = BuildTarget.Android,
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "JSS_DEVELOPMENT" },
            };

            try
            {
                var input = AddressablesBuilder.CreateInput(settings, playerOptions);

                Assert.That(input.Target, Is.EqualTo(BuildTarget.Android));
                Assert.That(input.TargetGroup, Is.EqualTo(BuildTargetGroup.Android));
                Assert.That(input.DevelopmentBuild, Is.True);
                Assert.That(input.ExtraScriptingDefines, Is.EqualTo(new[]
                {
                    "JSS_DEVELOPMENT",
                    "ADDRESSABLES_ADD_DEFINES",
                }));
                Assert.That(playerOptions.extraScriptingDefines,
                    Is.EqualTo(new[] { "JSS_DEVELOPMENT" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [TestCase(AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer)]
        [TestCase(AddressableAssetSettings.PlayerBuildOption.PreferencesValue)]
        public void ValidatePlayerBuildMode_ImplicitBuildMode_Throws(
            AddressableAssetSettings.PlayerBuildOption option)
        {
            var settings = ScriptableObject.CreateInstance<AddressableAssetSettings>();
            settings.BuildAddressablesWithPlayerBuild = option;

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    AddressablesBuilder.ValidatePlayerBuildMode(settings));

                Assert.That(exception.Message, Does.Contain("DoNotBuildWithPlayer"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ValidatePlayerBuildMode_DoNotBuildWithPlayer_IsAccepted()
        {
            var settings = ScriptableObject.CreateInstance<AddressableAssetSettings>();
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            try
            {
                Assert.DoesNotThrow(() =>
                    AddressablesBuilder.ValidatePlayerBuildMode(settings));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ValidateResult_NullAddressablesResult_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                AddressablesBuildResultValidator.Validate(
                    null,
                    BuildTargetKind.AndroidInternal));
        }

        [Test]
        public void ValidateResult_AddressablesError_ThrowsWithVariantAndError()
        {
            var result = new AddressablesPlayerBuildResult
            {
                Error = "fake addressables failure",
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                AddressablesBuildResultValidator.Validate(
                    result,
                    BuildTargetKind.Galaxy));

            Assert.That(exception.Message, Does.Contain("Galaxy"));
            Assert.That(exception.Message, Does.Contain("fake addressables failure"));
        }

        [Test]
        public void ValidateResult_EmptyError_IsAccepted()
        {
            var result = new AddressablesPlayerBuildResult
            {
                Error = string.Empty,
            };

            Assert.DoesNotThrow(() =>
                AddressablesBuildResultValidator.Validate(
                    result,
                    BuildTargetKind.GooglePlay));
        }

        [Test]
        public void ConcreteBuilder_NullSettings_ThrowsBeforeBuildDispatch()
        {
            var builder = new AddressablesBuilder(() => null);
            var configuration = BuildConfiguration.Resolve(
                BuildTargetKind.AndroidInternal,
                42);

            Assert.Throws<InvalidOperationException>(() =>
                builder.Build(configuration, CreatePlayerOptions()));
        }

        [Test]
        public void ConcreteBuilder_MissingActivePlayerBuilder_Throws()
        {
            var settings = ScriptableObject.CreateInstance<AddressableAssetSettings>();
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            var builder = new AddressablesBuilder(() => settings);
            var configuration = BuildConfiguration.Resolve(
                BuildTargetKind.AndroidInternal,
                42);

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    builder.Build(configuration, CreatePlayerOptions()));

                Assert.That(exception.Message, Does.Contain("active player data builder"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ConcreteBuilder_CallsActivePlayerBuildDataWithConstructedInput()
        {
            var settings = ScriptableObject.CreateInstance<AddressableAssetSettings>();
            var dataBuilder = ScriptableObject.CreateInstance<RecordingDataBuilder>();
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            Assert.That(settings.AddDataBuilder(dataBuilder, postEvent: false), Is.True);
            settings.ActivePlayerDataBuilderIndex = 0;
            var builder = new AddressablesBuilder(() => settings);
            var configuration = BuildConfiguration.Resolve(
                BuildTargetKind.GooglePlay,
                42);

            try
            {
                builder.Build(configuration, CreatePlayerOptions("JSS_GOOGLE_PLAY"));

                Assert.That(dataBuilder.BuildCount, Is.EqualTo(1));
                Assert.That(dataBuilder.LastRequestedResultType,
                    Is.EqualTo(typeof(AddressablesPlayerBuildResult)));
                Assert.That(dataBuilder.LastInput, Is.Not.Null);
                Assert.That(dataBuilder.LastInput.ExtraScriptingDefines, Is.EqualTo(new[]
                {
                    "JSS_GOOGLE_PLAY",
                    "ADDRESSABLES_ADD_DEFINES",
                }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dataBuilder);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static BuildPlayerOptions CreatePlayerOptions(
            string variant = "JSS_DEVELOPMENT")
        {
            return new BuildPlayerOptions
            {
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                extraScriptingDefines = new[] { variant },
            };
        }

        private sealed class RecordingDataBuilder : ScriptableObject, IDataBuilder
        {
            public string Name => "Recording test data builder";

            public int BuildCount { get; private set; }

            public Type LastRequestedResultType { get; private set; }

            public AddressablesDataBuilderInput LastInput { get; private set; }

            public bool CanBuildData<T>() where T : IDataBuilderResult
            {
                return typeof(T) == typeof(AddressablesPlayerBuildResult);
            }

            public TResult BuildData<TResult>(AddressablesDataBuilderInput builderInput)
                where TResult : IDataBuilderResult
            {
                BuildCount++;
                LastRequestedResultType = typeof(TResult);
                LastInput = builderInput;
                if (typeof(TResult) != typeof(AddressablesPlayerBuildResult))
                {
                    throw new InvalidOperationException("Unexpected result type in test.");
                }

                return (TResult)(object)new AddressablesPlayerBuildResult
                {
                    Error = string.Empty,
                };
            }

            public void ClearCachedData()
            {
            }
        }
    }
}
