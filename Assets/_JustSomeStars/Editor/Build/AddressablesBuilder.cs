using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace JustSomeStars.Editor.Build
{
    internal interface IAddressablesBuilder
    {
        void Build(
            BuildConfiguration configuration,
            BuildPlayerOptions playerOptions);
    }

    internal sealed class AddressablesBuilder : IAddressablesBuilder
    {
        private const string AddDefinesOptIn = "ADDRESSABLES_ADD_DEFINES";

        private readonly Func<AddressableAssetSettings> m_GetSettings;

        public AddressablesBuilder()
            : this(() => AddressableAssetSettingsDefaultObject.GetSettings(false))
        {
        }

        internal AddressablesBuilder(Func<AddressableAssetSettings> getSettings)
        {
            m_GetSettings = getSettings ??
                throw new ArgumentNullException(nameof(getSettings));
        }

        public void Build(
            BuildConfiguration configuration,
            BuildPlayerOptions playerOptions)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var settings = m_GetSettings();
            ValidatePlayerBuildMode(settings);
            var activeBuilder = settings.ActivePlayerDataBuilder;
            if (activeBuilder == null)
            {
                throw new InvalidOperationException(
                    "Committed Addressables settings have no active player data builder.");
            }

            if (!activeBuilder.CanBuildData<AddressablesPlayerBuildResult>())
            {
                throw new InvalidOperationException(
                    "The active Addressables player data builder cannot produce a player build result.");
            }

            var input = CreateInput(settings, playerOptions);
            var result = activeBuilder.BuildData<AddressablesPlayerBuildResult>(input);
            AddressablesBuildResultValidator.Validate(result, configuration.Kind);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log(
                "[JSS Build] Addressables succeeded for " + configuration.Kind +
                ": locations=" + result.LocationCount +
                ", durationSeconds=" +
                result.Duration.ToString("0.###", CultureInfo.InvariantCulture) +
                ", output=" + result.OutputPath + ".");
        }

        internal static AddressablesDataBuilderInput CreateInput(
            AddressableAssetSettings settings,
            BuildPlayerOptions playerOptions)
        {
            ValidatePlayerBuildMode(settings);
            var addressablesOptions = playerOptions;
            var playerDefines = playerOptions.extraScriptingDefines ?? Array.Empty<string>();
            addressablesOptions.extraScriptingDefines = playerDefines
                .Where(define => !string.Equals(
                    define,
                    AddDefinesOptIn,
                    StringComparison.Ordinal))
                .Concat(new[] { AddDefinesOptIn })
                .ToArray();
            return new AddressablesDataBuilderInput(settings, addressablesOptions);
        }

        internal static void ValidatePlayerBuildMode(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                throw new InvalidOperationException(
                    "Committed Addressables settings are missing.");
            }

            if (settings.BuildAddressablesWithPlayerBuild !=
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer)
            {
                throw new InvalidOperationException(
                    "Addressables must use DoNotBuildWithPlayer because the CLI " +
                    "performs one explicit variant-aware content build.");
            }
        }
    }

    internal static class AddressablesBuildResultValidator
    {
        public static void Validate(
            AddressablesPlayerBuildResult result,
            BuildTargetKind kind)
        {
            if (result == null)
            {
                throw new InvalidOperationException(
                    "Addressables returned no build result for " + kind + ".");
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException(
                    "Addressables content build failed for " + kind + ": " + result.Error);
            }
        }
    }
}
