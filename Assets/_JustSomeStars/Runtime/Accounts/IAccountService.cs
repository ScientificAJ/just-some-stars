using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Accounts
{
    public enum AccountConnection
    {
        OfflineGuest = 0,
        CloudUnavailable = 1,
        CloudAvailable = 2,
        Linked = 3,
        Pending = 4,
        Conflict = 5,
    }

    public enum AccountCapability
    {
        Checking = 0,
        Available = 1,
        Offline = 2,
        UnavailableConfiguration = 3,
        UnavailableDependency = 4,
        UnavailablePlatform = 5,
    }

    public enum AccountSyncState
    {
        LocalOnly = 0,
        NeedsRemoteRead = 1,
        Conflict = 2,
        PendingUpload = 3,
        Synced = 4,
        Failed = 5,
    }

    public enum AccountOperation
    {
        None = 0,
        Linking = 1,
        ResolvingConflict = 2,
        Syncing = 3,
        Exporting = 4,
        SigningOut = 5,
        Unlinking = 6,
        Deleting = 7,
    }

    public enum AccountLinkStatus
    {
        Linked = 0,
        Unavailable = 1,
        NeedsPlayerChoice = 2,
        Failed = 3,
    }

    public enum AccountConflictChoice
    {
        UseThisDevice = 0,
        UseCloudBackup = 1,
    }

    public enum AccountUnlinkStatus
    {
        Unlinked = 0,
        NotLinked = 1,
        WouldOrphanAccount = 2,
        Failed = 3,
    }

    public sealed class AccountState
    {
        public AccountState(
            AccountConnection connection,
            AccountCapability capability,
            AccountSyncState sync,
            AccountOperation operation,
            string guestId,
            string firebaseUserId,
            string statusMessage,
            SaveMergeConflictKind? conflictKind = null)
        {
            Connection = connection;
            Capability = capability;
            Sync = sync;
            Operation = operation;
            GuestId = guestId ?? string.Empty;
            FirebaseUserId = firebaseUserId ?? string.Empty;
            StatusMessage = statusMessage ?? string.Empty;
            ConflictKind = conflictKind;
        }

        public AccountConnection Connection { get; }

        public AccountCapability Capability { get; }

        public AccountSyncState Sync { get; }

        public AccountOperation Operation { get; }

        public string GuestId { get; }

        public string FirebaseUserId { get; }

        public string StatusMessage { get; }

        public SaveMergeConflictKind? ConflictKind { get; }
    }

    public sealed class AccountLinkResult
    {
        private readonly GameSave m_MergedSave;

        public AccountLinkResult(
            AccountLinkStatus status,
            GameSave mergedSave = null,
            SaveMergeConflictKind? conflictKind = null)
        {
            Status = status;
            m_MergedSave = mergedSave?.Copy();
            ConflictKind = conflictKind;
        }

        public AccountLinkStatus Status { get; }

        public GameSave MergedSave => m_MergedSave?.Copy();

        public SaveMergeConflictKind? ConflictKind { get; }
    }

    public sealed class CloudSyncResult
    {
        public CloudSyncResult(bool succeeded, bool isPending, string message)
        {
            Succeeded = succeeded;
            IsPending = isPending;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }

        public bool IsPending { get; }

        public string Message { get; }
    }

    public sealed class AccountExportResult
    {
        public AccountExportResult(bool succeeded, string document, string message)
        {
            Succeeded = succeeded;
            Document = document ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string Document { get; }

        public string Message { get; }
    }

    public sealed class AccountUnlinkResult
    {
        public AccountUnlinkResult(AccountUnlinkStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public AccountUnlinkStatus Status { get; }

        public string Message { get; }
    }

    public interface IAccountService : IGameService
    {
        AccountState Current { get; }

        event Action<AccountState> StateChanged;

        ValueTask<AccountLinkResult> LinkGoogleAsync(
            CancellationToken cancellationToken);

        ValueTask<AccountLinkResult> ResolveConflictAsync(
            AccountConflictChoice choice,
            CancellationToken cancellationToken);

        ValueTask<CloudSyncResult> SyncAsync(CancellationToken cancellationToken);

        ValueTask<AccountExportResult> ExportDataAsync(
            CancellationToken cancellationToken);

        ValueTask<AccountUnlinkResult> UnlinkGoogleAsync(
            CancellationToken cancellationToken);

        ValueTask SignOutAsync(CancellationToken cancellationToken);

        ValueTask DeleteAccountAsync(CancellationToken cancellationToken);
    }
}
