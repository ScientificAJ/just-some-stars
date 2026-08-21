using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace JustSomeStars.Editor.Build
{
    internal interface ISigningSecretScrubber
    {
        void ScrubAndVerify(ReleaseSigningCredentials credentials);
    }

    internal interface ISigningSecretFileAccess
    {
        IEnumerable<string> EnumerateFiles(string rootPath);

        bool ContainsAny(
            string path,
            IReadOnlyList<byte[]> patterns);

        void DeleteFile(string path);
    }

    internal sealed class SigningSecretScrubber : ISigningSecretScrubber
    {
        private static readonly string[] VolatileCacheRoots =
        {
            Path.Combine("Library", "Bee"),
            Path.Combine("Library", "BuildPlayerData"),
            Path.Combine("Library", "Il2cppBuildCache"),
            Path.Combine("Library", "PlayerDataCache"),
            "Temp",
        };

        private readonly string m_ProjectRoot;
        private readonly ISigningSecretFileAccess m_FileAccess;

        public SigningSecretScrubber()
            : this(projectRoot: null, new SystemSigningSecretFileAccess())
        {
        }

        internal SigningSecretScrubber(ISigningSecretFileAccess fileAccess)
            : this(projectRoot: null, fileAccess)
        {
        }

        internal SigningSecretScrubber(string projectRoot)
            : this(projectRoot, new SystemSigningSecretFileAccess())
        {
        }

        private SigningSecretScrubber(
            string projectRoot,
            ISigningSecretFileAccess fileAccess)
        {
            m_ProjectRoot = projectRoot;
            m_FileAccess = fileAccess ??
                throw new ArgumentNullException(nameof(fileAccess));
        }

        public void ScrubAndVerify(ReleaseSigningCredentials credentials)
        {
            if (string.IsNullOrWhiteSpace(m_ProjectRoot))
            {
                throw new InvalidOperationException(
                    "The signing scrubber has no configured Unity project root.");
            }

            ScrubAndVerify(m_ProjectRoot, credentials);
        }

        internal void ScrubAndVerify(
            string projectRoot,
            ReleaseSigningCredentials credentials)
        {
            if (credentials == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "A Unity project root is required.",
                    nameof(projectRoot));
            }

            var patterns = CreatePatterns(credentials);
            var firstPass = FindResidueFiles(projectRoot, patterns);
            if (firstPass.Count == 0)
            {
                return;
            }

            foreach (var residuePath in firstPass)
            {
                try
                {
                    m_FileAccess.DeleteFile(residuePath);
                }
                catch (Exception)
                {
                    throw new InvalidOperationException(
                        "A volatile Unity cache file containing signing residue " +
                        "could not be removed.");
                }
            }

            var remainingResidue = FindResidueFiles(projectRoot, patterns);
            if (remainingResidue.Count > 0)
            {
                throw new InvalidOperationException(
                    "Signing residue remains in bounded volatile Unity caches; " +
                    "artifact publication is forbidden.");
            }
        }

        private IReadOnlyList<string> FindResidueFiles(
            string projectRoot,
            IReadOnlyList<byte[]> patterns)
        {
            var result = new List<string>();
            foreach (var relativeRoot in VolatileCacheRoots)
            {
                var root = Path.GetFullPath(Path.Combine(projectRoot, relativeRoot));
                BuildFilesystemSafety.EnsureNoFilesystemLinks(
                    projectRoot,
                    root,
                    "a bounded volatile Unity signing-cache root");
                IEnumerable<string> files;
                try
                {
                    files = m_FileAccess.EnumerateFiles(root).ToArray();
                }
                catch (Exception)
                {
                    throw new InvalidOperationException(
                        "A bounded volatile Unity cache could not be enumerated " +
                        "to prove signing cleanup.");
                }

                foreach (var file in files)
                {
                    bool containsResidue;
                    try
                    {
                        containsResidue = m_FileAccess.ContainsAny(file, patterns);
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException(
                            "A volatile Unity cache file could not be inspected " +
                            "to prove signing cleanup.");
                    }

                    if (containsResidue)
                    {
                        result.Add(file);
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<byte[]> CreatePatterns(
            ReleaseSigningCredentials credentials)
        {
            return credentials.SensitiveValues
                .Where(value => !string.IsNullOrEmpty(value))
                .SelectMany(CreateSerializedForms)
                .Distinct(StringComparer.Ordinal)
                .SelectMany(value => new[]
                {
                    Encoding.UTF8.GetBytes(value),
                    Encoding.Unicode.GetBytes(value),
                })
                .Where(value => value.Length > 0)
                .GroupBy(value => Convert.ToBase64String(value), StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
        }

        private static IEnumerable<string> CreateSerializedForms(string value)
        {
            yield return value;
            yield return EscapeJson(
                value,
                escapeNonAsciiAndSlash: false,
                uppercaseUnicode: false);
            yield return EscapeJson(
                value,
                escapeNonAsciiAndSlash: true,
                uppercaseUnicode: false);
            yield return EscapeJson(
                value,
                escapeNonAsciiAndSlash: true,
                uppercaseUnicode: true);
            yield return EscapeJavaPropertiesValue(
                value,
                uppercaseUnicode: false);
            yield return EscapeJavaPropertiesValue(
                value,
                uppercaseUnicode: true);
        }

        private static string EscapeJson(
            string value,
            bool escapeNonAsciiAndSlash,
            bool uppercaseUnicode)
        {
            var result = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\"':
                        result.Append("\\\"");
                        break;
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '/':
                        result.Append(escapeNonAsciiAndSlash ? "\\/" : "/");
                        break;
                    case '\b':
                        result.Append("\\b");
                        break;
                    case '\f':
                        result.Append("\\f");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    default:
                        if (character < ' ' ||
                            (escapeNonAsciiAndSlash && character > '~'))
                        {
                            AppendUnicodeEscape(
                                result,
                                character,
                                uppercaseUnicode);
                        }
                        else
                        {
                            result.Append(character);
                        }

                        break;
                }
            }

            return result.ToString();
        }

        private static string EscapeJavaPropertiesValue(
            string value,
            bool uppercaseUnicode)
        {
            var result = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '\f':
                        result.Append("\\f");
                        break;
                    case ' ':
                        if (index == 0)
                        {
                            result.Append('\\');
                        }

                        result.Append(character);
                        break;
                    case '=':
                    case ':':
                    case '#':
                    case '!':
                        result.Append('\\');
                        result.Append(character);
                        break;
                    default:
                        if (character < ' ' || character > '~')
                        {
                            AppendUnicodeEscape(
                                result,
                                character,
                                uppercaseUnicode);
                        }
                        else
                        {
                            result.Append(character);
                        }

                        break;
                }
            }

            return result.ToString();
        }

        private static void AppendUnicodeEscape(
            StringBuilder result,
            char character,
            bool uppercase)
        {
            result.Append("\\u");
            result.Append(
                ((int)character).ToString(
                    uppercase ? "X4" : "x4",
                    CultureInfo.InvariantCulture));
        }
    }

    internal sealed class SystemSigningSecretFileAccess : ISigningSecretFileAccess
    {
        public IEnumerable<string> EnumerateFiles(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            if ((File.GetAttributes(rootPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "A bounded volatile cache root is a filesystem link.");
            }

            var files = new List<string>();
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);
            while (pendingDirectories.Count > 0)
            {
                var directory = pendingDirectories.Pop();
                foreach (var file in Directory.GetFiles(directory))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            "A bounded volatile cache file is a filesystem link.");
                    }

                    files.Add(file);
                }

                foreach (var childDirectory in Directory.GetDirectories(directory))
                {
                    if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            "A bounded volatile cache directory is a filesystem link.");
                    }

                    pendingDirectories.Push(childDirectory);
                }
            }

            return files;
        }

        public bool ContainsAny(
            string path,
            IReadOnlyList<byte[]> patterns)
        {
            if (patterns == null)
            {
                throw new ArgumentNullException(nameof(patterns));
            }

            var nonEmptyPatterns = patterns
                .Where(pattern => pattern != null && pattern.Length > 0)
                .ToArray();
            if (nonEmptyPatterns.Length == 0)
            {
                return false;
            }

            const int readBufferSize = 64 * 1024;
            var overlapSize = nonEmptyPatterns.Max(pattern => pattern.Length) - 1;
            var buffer = new byte[readBufferSize + overlapSize];
            var retainedBytes = 0;
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete,
                       readBufferSize,
                       FileOptions.SequentialScan))
            {
                while (true)
                {
                    var bytesRead = stream.Read(
                        buffer,
                        retainedBytes,
                        readBufferSize);
                    var availableBytes = retainedBytes + bytesRead;
                    if (nonEmptyPatterns.Any(pattern =>
                            Contains(buffer, availableBytes, pattern)))
                    {
                        return true;
                    }

                    if (bytesRead == 0)
                    {
                        return false;
                    }

                    retainedBytes = Math.Min(overlapSize, availableBytes);
                    if (retainedBytes > 0)
                    {
                        Buffer.BlockCopy(
                            buffer,
                            availableBytes - retainedBytes,
                            buffer,
                            0,
                            retainedBytes);
                    }
                }
            }
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        private static bool Contains(
            byte[] haystack,
            int haystackLength,
            byte[] needle)
        {
            if (haystackLength < needle.Length)
            {
                return false;
            }

            for (var offset = 0; offset <= haystackLength - needle.Length; offset++)
            {
                var matches = true;
                for (var index = 0; index < needle.Length; index++)
                {
                    if (haystack[offset + index] != needle[index])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
