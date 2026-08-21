using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JustSomeStars.Editor.Build
{
    public sealed class BuildScenePlan
    {
        private BuildScenePlan(
            bool requiresTemporaryScene,
            IReadOnlyList<string> scenePaths)
        {
            RequiresTemporaryScene = requiresTemporaryScene;
            ScenePaths = scenePaths;
        }

        public bool RequiresTemporaryScene { get; }

        public IReadOnlyList<string> ScenePaths { get; }

        public static BuildScenePlan Resolve(
            IEnumerable<string> enabledScenePaths,
            string temporaryScenePath)
        {
            if (enabledScenePaths == null)
            {
                throw new ArgumentNullException(nameof(enabledScenePaths));
            }

            if (string.IsNullOrWhiteSpace(temporaryScenePath))
            {
                throw new ArgumentException(
                    "A temporary build-scene path is required.",
                    nameof(temporaryScenePath));
            }

            var scenePaths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scenePath in enabledScenePaths)
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                var normalizedPath = scenePath.Trim();
                if (seenPaths.Add(normalizedPath))
                {
                    scenePaths.Add(normalizedPath);
                }
            }

            if (scenePaths.Count > 0)
            {
                return new BuildScenePlan(
                    false,
                    new ReadOnlyCollection<string>(scenePaths));
            }

            var normalizedTemporaryPath = temporaryScenePath.Trim();
            return new BuildScenePlan(
                true,
                new ReadOnlyCollection<string>(new[] { normalizedTemporaryPath }));
        }
    }
}
