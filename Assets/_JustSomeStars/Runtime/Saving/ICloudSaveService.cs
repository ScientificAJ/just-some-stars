using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Saving
{
    public enum CloudSaveSource
    {
        Server = 0,
        Cache = 1,
    }

    public readonly struct CloudSaveVersion : IEquatable<CloudSaveVersion>
    {
        public CloudSaveVersion(long revision, string updateToken)
        {
            Revision = revision;
            UpdateToken = updateToken ?? string.Empty;
            IsPresent = true;
        }

        public long Revision { get; }

        public string UpdateToken { get; }

        public bool IsPresent { get; }

        public bool Equals(CloudSaveVersion other) =>
            Revision == other.Revision &&
            IsPresent == other.IsPresent &&
            string.Equals(UpdateToken, other.UpdateToken, StringComparison.Ordinal);
    }

    public readonly struct CloudSaveSnapshot
    {
        private readonly GameSave m_Save;

        public CloudSaveSnapshot(
            GameSave save,
            int envelopeVersion = 1,
            CloudSaveVersion version = default,
            CloudSaveSource source = CloudSaveSource.Server)
        {
            m_Save = save?.Copy() ?? throw new ArgumentNullException(nameof(save));
            EnvelopeVersion = envelopeVersion;
            Version = version.IsPresent
                ? version
                : new CloudSaveVersion(save.Metadata.Revision, string.Empty);
            Source = source;
        }

        public GameSave Save => m_Save?.Copy();

        public int SchemaVersion => m_Save?.SchemaVersion ?? 0;

        public int EnvelopeVersion { get; }

        public CloudSaveVersion Version { get; }

        public CloudSaveSource Source { get; }
    }

    public readonly struct CloudCommitResult
    {
        public CloudCommitResult(bool committed, bool versionMismatch, CloudSaveVersion version)
        {
            Committed = committed;
            VersionMismatch = versionMismatch;
            Version = version;
        }

        public bool Committed { get; }

        public bool VersionMismatch { get; }

        public CloudSaveVersion Version { get; }
    }

    public interface ICloudSaveService : IGameService
    {
        ValueTask<CloudSaveSnapshot?> DownloadAsync(
            string userId,
            CancellationToken cancellationToken);

        ValueTask UploadAsync(
            string userId,
            GameSave save,
            CancellationToken cancellationToken);

        ValueTask<CloudCommitResult> UploadIfUnchangedAsync(
            string userId,
            GameSave save,
            CloudSaveVersion expectedRemote,
            CancellationToken cancellationToken);

        ValueTask DeleteAsync(
            string userId,
            CancellationToken cancellationToken);

        ValueTask<string> ExportAsync(
            string userId,
            CancellationToken cancellationToken);
    }
}
