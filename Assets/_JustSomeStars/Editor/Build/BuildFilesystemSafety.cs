using System;
using System.IO;

namespace JustSomeStars.Editor.Build
{
    internal static class BuildFilesystemSafety
    {
        public static void EnsureNoFilesystemLinks(
            string projectRoot,
            string targetPath,
            string description)
        {
            var absoluteProjectRoot = Path.GetFullPath(projectRoot);
            var absoluteTargetPath = Path.GetFullPath(targetPath);
            var projectPrefix = absoluteProjectRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!string.Equals(
                    absoluteTargetPath,
                    absoluteProjectRoot,
                    StringComparison.Ordinal) &&
                !absoluteTargetPath.StartsWith(projectPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to access " + description +
                    " outside the Unity project root.");
            }

            var currentPath = absoluteTargetPath;
            while (true)
            {
                if ((File.Exists(currentPath) || Directory.Exists(currentPath)) &&
                    (File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(
                        "Refusing to access " + description +
                        " through a filesystem link.");
                }

                if (string.Equals(
                        currentPath,
                        absoluteProjectRoot,
                        StringComparison.Ordinal))
                {
                    return;
                }

                var parentPath = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrEmpty(parentPath) ||
                    string.Equals(parentPath, currentPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The " + description +
                        " path could not be bounded to the Unity project root.");
                }

                currentPath = parentPath;
            }
        }
    }
}
