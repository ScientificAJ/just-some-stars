using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Accounts
{
    public interface IFirebaseAuthGateway : IGameService
    {
        bool IsConfigured { get; }

        string CurrentUserId { get; }

        ValueTask<string> LinkGoogleAsync(CancellationToken cancellationToken);

        ValueTask SignOutAsync(CancellationToken cancellationToken);

        ValueTask UnlinkGoogleAsync(CancellationToken cancellationToken);

        ValueTask DeleteAccountAsync(CancellationToken cancellationToken);
    }

    public sealed class UnavailableFirebaseAuthGateway : IFirebaseAuthGateway
    {
        public bool IsConfigured => false;

        public string CurrentUserId => string.Empty;

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<StartupResult>(StartupResult.Unavailable(
                "Google backup is not configured for this build."));
        }

        public ValueTask ShutdownAsync() => default;

        public ValueTask<string> LinkGoogleAsync(CancellationToken cancellationToken) =>
            throw Unavailable();

        public ValueTask SignOutAsync(CancellationToken cancellationToken) =>
            throw Unavailable();

        public ValueTask UnlinkGoogleAsync(CancellationToken cancellationToken) =>
            throw Unavailable();

        public ValueTask DeleteAccountAsync(CancellationToken cancellationToken) =>
            throw Unavailable();

        private static InvalidOperationException Unavailable() =>
            new InvalidOperationException(
                "Google backup is not configured for this build.");
    }

    public sealed class FirebaseAccountService : IAccountService
    {
        private readonly GuestAccountService m_Guest;
        private readonly ISaveService m_LocalSave;
        private readonly ICloudSaveService m_Cloud;
        private readonly IFirebaseAuthGateway m_Auth;
        private readonly SemaphoreSlim m_Operation = new SemaphoreSlim(1, 1);

        private StartupResult m_CloudStartup;
        private GameSave m_ConflictLocal;
        private GameSave m_ConflictCloud;
        private CloudSaveVersion m_ConflictRemoteVersion;
        private string m_ConflictUserId;

        public FirebaseAccountService(
            GuestAccountService guest,
            ISaveService localSave,
            ICloudSaveService cloud,
            IFirebaseAuthGateway auth)
        {
            m_Guest = guest ?? throw new ArgumentNullException(nameof(guest));
            m_LocalSave = localSave ?? throw new ArgumentNullException(nameof(localSave));
            m_Cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            m_Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            Current = GuestState(string.Empty);
        }

        public event Action<AccountState> StateChanged;

        public AccountState Current { get; private set; }

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await m_Guest.InitializeAsync(cancellationToken);
            Current = GuestState(m_Guest.Current.GuestId);

            var authStartup = await m_Auth.InitializeAsync(cancellationToken);
            m_CloudStartup = await m_Cloud.InitializeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!m_Auth.IsConfigured || !authStartup.IsAvailable ||
                !m_CloudStartup.IsAvailable)
            {
                SetState(new AccountState(
                    AccountConnection.CloudUnavailable,
                    AccountCapability.UnavailableConfiguration,
                    AccountSyncState.LocalOnly,
                    AccountOperation.None,
                    m_Guest.Current.GuestId,
                    string.Empty,
                    "Google backup is unavailable in this build. " +
                    "Offline progress still works."));
            }
            else if (!string.IsNullOrWhiteSpace(m_Auth.CurrentUserId))
            {
                var userId = m_Auth.CurrentUserId;
                SetPending(userId, "Restoring Google backup…");
                try
                {
                    await SynchronizeAuthenticatedUserAsync(
                        userId,
                        signOutOnLocalFailure: false,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    SetPending(userId, "Signed in. Cloud backup will retry.");
                    throw;
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    SetPending(userId,
                        "Signed in. Device save is safe; cloud backup will retry.");
                }
            }
            else
            {
                SetCloudAvailable();
            }

            return StartupResult.Available();
        }

        public async ValueTask ShutdownAsync()
        {
            await m_Auth.ShutdownAsync();
            await m_Cloud.ShutdownAsync();
            await m_Guest.ShutdownAsync();
            ClearConflict();
        }

        public async ValueTask<AccountLinkResult> LinkGoogleAsync(
            CancellationToken cancellationToken)
        {
            await m_Operation.WaitAsync(cancellationToken);
            try
            {
                if (!CanUseCloud())
                {
                    return new AccountLinkResult(AccountLinkStatus.Unavailable);
                }

                SetOperation(AccountOperation.Linking, "Connecting Google backup…");
                var userId = await m_Auth.LinkGoogleAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(userId))
                {
                    SetCloudAvailable();
                    return new AccountLinkResult(AccountLinkStatus.Failed);
                }

                return await SynchronizeAuthenticatedUserAsync(
                    userId,
                    signOutOnLocalFailure: true,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                RestoreTruthfulIdleState();
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RestoreTruthfulIdleState();
                return new AccountLinkResult(AccountLinkStatus.Failed);
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask<AccountLinkResult> ResolveConflictAsync(
            AccountConflictChoice choice,
            CancellationToken cancellationToken)
        {
            await m_Operation.WaitAsync(cancellationToken);
            var idle = Current;
            try
            {
                if (m_ConflictLocal == null || m_ConflictCloud == null ||
                    string.IsNullOrEmpty(m_ConflictUserId))
                {
                    return new AccountLinkResult(AccountLinkStatus.Failed);
                }

                SetOperation(AccountOperation.ResolvingConflict,
                    "Resolving the backup choice…");
                var selected = SaveMerge.ResolveConflict(
                    m_ConflictLocal,
                    m_ConflictCloud,
                    preferLocal: choice == AccountConflictChoice.UseThisDevice);

                await m_LocalSave.SaveCheckpointAsync(selected, cancellationToken);
                var commit = await m_Cloud.UploadIfUnchangedAsync(
                    m_ConflictUserId,
                    selected,
                    m_ConflictRemoteVersion,
                    cancellationToken);
                if (!commit.Committed)
                {
                    SetPending(m_ConflictUserId,
                        "Choice saved on this device. Cloud backup is waiting to retry.");
                    return new AccountLinkResult(AccountLinkStatus.Failed, selected);
                }

                var userId = m_ConflictUserId;
                ClearConflict();
                SetLinked(userId, AccountSyncState.Synced,
                    "Progress is backed up with Google.");
                return new AccountLinkResult(AccountLinkStatus.Linked, selected);
            }
            catch (OperationCanceledException)
            {
                RestoreIdleAfterFailure(idle, "Backup choice was cancelled.");
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RestoreIdleAfterFailure(
                    idle,
                    "Backup choice is still waiting. Device progress is safe.");
                return new AccountLinkResult(AccountLinkStatus.Failed);
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask<CloudSyncResult> SyncAsync(
            CancellationToken cancellationToken)
        {
            await m_Operation.WaitAsync(cancellationToken);
            var idle = Current;
            try
            {
                var userId = AuthenticatedUserId();
                if (!CanUseCloud() || string.IsNullOrEmpty(userId))
                {
                    return new CloudSyncResult(
                        false,
                        false,
                        "Connect to Google before syncing cloud progress.");
                }

                SetOperation(AccountOperation.Syncing, "Syncing cloud progress…");
                var result = await SynchronizeAuthenticatedUserAsync(
                    userId,
                    signOutOnLocalFailure: false,
                    cancellationToken: cancellationToken);
                return new CloudSyncResult(
                    result.Status == AccountLinkStatus.Linked,
                    result.Status == AccountLinkStatus.Failed,
                    Current.StatusMessage);
            }
            catch (OperationCanceledException)
            {
                RestoreIdleAfterFailure(idle, "Cloud sync was cancelled. Device save is safe.");
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RestoreIdleAfterFailure(
                    idle,
                    "Device save is safe. Cloud backup needs another try.");
                return new CloudSyncResult(false, true, Current.StatusMessage);
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask<AccountExportResult> ExportDataAsync(
            CancellationToken cancellationToken)
        {
            await m_Operation.WaitAsync(cancellationToken);
            var idle = Current;
            try
            {
                if (Current.Connection != AccountConnection.Linked ||
                    string.IsNullOrEmpty(Current.FirebaseUserId))
                {
                    return new AccountExportResult(
                        false,
                        string.Empty,
                        "Connect to Google before exporting cloud data.");
                }

                var userId = Current.FirebaseUserId;
                SetOperation(AccountOperation.Exporting, "Preparing cloud data export…");
                var document = await m_Cloud.ExportAsync(userId, cancellationToken);
                SetLinked(userId, Current.Sync, "Cloud data export is ready.");
                return new AccountExportResult(true, document, Current.StatusMessage);
            }
            catch (OperationCanceledException)
            {
                RestoreIdleAfterFailure(idle, "Cloud export was cancelled.");
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RestoreIdleAfterFailure(idle, "Cloud export could not be prepared.");
                return new AccountExportResult(false, string.Empty, Current.StatusMessage);
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask<string> ExportCloudDataAsync(
            CancellationToken cancellationToken)
        {
            var result = await ExportDataAsync(cancellationToken);
            return result.Document;
        }

        public async ValueTask<AccountUnlinkResult> UnlinkGoogleAsync(
            CancellationToken cancellationToken)
        {
            await m_Operation.WaitAsync(cancellationToken);
            var idle = Current;
            try
            {
                if (string.IsNullOrEmpty(AuthenticatedUserId()))
                {
                    return new AccountUnlinkResult(
                        AccountUnlinkStatus.NotLinked,
                        "No Google account is linked.");
                }

                SetOperation(AccountOperation.Unlinking, "Disconnecting Google backup…");
                await m_Auth.UnlinkGoogleAsync(cancellationToken);
                ClearConflict();
                SetCloudAvailable();
                return new AccountUnlinkResult(
                    AccountUnlinkStatus.Unlinked,
                    "Google backup is disconnected. Device progress is unchanged.");
            }
            catch (OperationCanceledException)
            {
                RestoreIdleAfterFailure(idle, "Disconnect was cancelled.");
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RestoreIdleAfterFailure(idle, "Google backup could not be disconnected.");
                return new AccountUnlinkResult(
                    AccountUnlinkStatus.Failed,
                    Current.StatusMessage);
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask SignOutAsync(CancellationToken cancellationToken)
        {
            await m_Operation.WaitAsync(cancellationToken);
            var idle = Current;
            try
            {
                SetOperation(AccountOperation.SigningOut, "Signing out…");
                await m_Auth.SignOutAsync(cancellationToken);
                ClearConflict();
                SetCloudAvailable();
            }
            catch (OperationCanceledException)
            {
                RestoreIdleAfterFailure(idle, "Sign-out was cancelled.");
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RestoreIdleAfterFailure(idle, "Google sign-out did not complete.");
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask DeleteAccountAsync(
            CancellationToken cancellationToken)
        {
            await m_Operation.WaitAsync(cancellationToken);
            var idle = Current;
            try
            {
                var userId = AuthenticatedUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return;
                }

                SetOperation(AccountOperation.Deleting,
                    "Deleting cloud backup and Google account…");
                await m_Cloud.DeleteAsync(userId, cancellationToken);
                await m_Auth.DeleteAccountAsync(cancellationToken);
                ClearConflict();
                SetCloudAvailable();
            }
            catch (OperationCanceledException)
            {
                RestoreIdleAfterFailure(
                    idle,
                    "Account deletion was cancelled. Verify cloud status before retrying.");
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                RestoreIdleAfterFailure(
                    idle,
                    "Account deletion did not fully complete. Device progress is safe.");
            }
            finally
            {
                m_Operation.Release();
            }
        }

        private async ValueTask<AccountLinkResult> SynchronizeAuthenticatedUserAsync(
            string userId,
            bool signOutOnLocalFailure,
            CancellationToken cancellationToken)
        {
            var localResult = await m_LocalSave.LoadAsync(cancellationToken);
            if (localResult.Status == LoadSaveStatus.Unreadable ||
                localResult.Status == LoadSaveStatus.StorageUnavailable)
            {
                if (signOutOnLocalFailure)
                {
                    await m_Auth.SignOutAsync(CancellationToken.None);
                    SetCloudAvailable();
                }
                else
                {
                    SetPending(userId,
                        "Signed in. Local progress needs recovery before cloud sync.");
                }

                return new AccountLinkResult(AccountLinkStatus.Failed);
            }

            var remote = await m_Cloud.DownloadAsync(userId, cancellationToken);
            if (remote.HasValue && remote.Value.Source != CloudSaveSource.Server)
            {
                SetPending(userId, "Signed in. Connect to verify the cloud backup.");
                return new AccountLinkResult(AccountLinkStatus.Failed);
            }

            var local = localResult.HasSave ? localResult.Save : null;
            var cloud = remote?.Save;
            if (local == null && cloud == null)
            {
                SetLinked(userId, AccountSyncState.LocalOnly,
                    "Google backup is ready. Sync after a checkpoint to back up progress.");
                return new AccountLinkResult(AccountLinkStatus.Linked);
            }

            GameSave merged;
            try
            {
                merged = local == null
                    ? cloud.Copy()
                    : cloud == null
                        ? local.Copy()
                        : m_LocalSave.Merge(local, cloud);
            }
            catch (SaveMergeConflictException conflict)
            {
                m_ConflictLocal = local?.Copy();
                m_ConflictCloud = cloud?.Copy();
                m_ConflictRemoteVersion = remote.Value.Version;
                m_ConflictUserId = userId;
                SetState(new AccountState(
                    AccountConnection.Conflict,
                    AccountCapability.Available,
                    AccountSyncState.Conflict,
                    AccountOperation.None,
                    m_Guest.Current.GuestId,
                    userId,
                    ConflictMessage(conflict.Kind),
                    conflict.Kind));
                return new AccountLinkResult(
                    AccountLinkStatus.NeedsPlayerChoice,
                    conflictKind: conflict.Kind);
            }

            if (local == null || !merged.Equals(local))
            {
                await m_LocalSave.SaveCheckpointAsync(merged, cancellationToken);
            }

            var commit = await m_Cloud.UploadIfUnchangedAsync(
                userId,
                merged,
                remote?.Version ?? default,
                cancellationToken);
            if (!commit.Committed)
            {
                SetPending(userId,
                    "Device save is safe. Cloud backup is waiting to retry.");
                return new AccountLinkResult(AccountLinkStatus.Failed, merged);
            }

            ClearConflict();
            SetLinked(userId, AccountSyncState.Synced,
                "Progress is backed up with Google.");
            return new AccountLinkResult(AccountLinkStatus.Linked, merged);
        }

        private string AuthenticatedUserId()
        {
            if (!string.IsNullOrWhiteSpace(Current.FirebaseUserId))
            {
                return Current.FirebaseUserId;
            }

            return m_Auth.CurrentUserId ?? string.Empty;
        }

        private bool CanUseCloud() =>
            m_Auth.IsConfigured && m_CloudStartup.IsAvailable;

        private static bool IsRecoverable(Exception exception) =>
            !(exception is OutOfMemoryException) &&
            !(exception is StackOverflowException) &&
            !(exception is AccessViolationException);

        private void RestoreIdleAfterFailure(AccountState idle, string message)
        {
            if (idle == null)
            {
                RestoreTruthfulIdleState();
                return;
            }

            SetState(new AccountState(
                idle.Connection,
                idle.Capability,
                idle.Sync == AccountSyncState.Synced
                    ? AccountSyncState.Failed
                    : idle.Sync,
                AccountOperation.None,
                idle.GuestId,
                idle.FirebaseUserId,
                message,
                idle.ConflictKind));
        }

        private void SetCloudAvailable()
        {
            SetState(new AccountState(
                AccountConnection.CloudAvailable,
                AccountCapability.Available,
                AccountSyncState.LocalOnly,
                AccountOperation.None,
                m_Guest.Current.GuestId,
                string.Empty,
                "Playing offline. Google backup is optional and available."));
        }

        private void SetLinked(
            string userId,
            AccountSyncState sync,
            string message)
        {
            SetState(new AccountState(
                AccountConnection.Linked,
                AccountCapability.Available,
                sync,
                AccountOperation.None,
                m_Guest.Current.GuestId,
                userId,
                message));
        }

        private void SetPending(string userId, string message)
        {
            SetState(new AccountState(
                AccountConnection.Pending,
                AccountCapability.Offline,
                AccountSyncState.NeedsRemoteRead,
                AccountOperation.None,
                m_Guest.Current.GuestId,
                userId,
                message));
        }

        private void SetOperation(AccountOperation operation, string message)
        {
            SetState(new AccountState(
                Current.Connection,
                Current.Capability,
                Current.Sync,
                operation,
                Current.GuestId,
                Current.FirebaseUserId,
                message,
                Current.ConflictKind));
        }

        private void RestoreTruthfulIdleState()
        {
            var userId = AuthenticatedUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                if (Current.Connection == AccountConnection.Linked)
                {
                    SetLinked(userId, AccountSyncState.Failed,
                        "Device save is safe. Cloud backup needs another try.");
                }
                else
                {
                    SetPending(userId,
                        "Signed in. Device save is safe; cloud backup will retry.");
                }
            }
            else if (CanUseCloud())
            {
                SetCloudAvailable();
            }
            else
            {
                SetState(new AccountState(
                    AccountConnection.CloudUnavailable,
                    AccountCapability.UnavailableConfiguration,
                    AccountSyncState.LocalOnly,
                    AccountOperation.None,
                    m_Guest.Current.GuestId,
                    string.Empty,
                    "Google backup is unavailable in this build. " +
                    "Offline progress still works."));
            }
        }

        private void SetState(AccountState state)
        {
            Current = state ?? throw new ArgumentNullException(nameof(state));
            StateChanged?.Invoke(state);
        }

        private static AccountState GuestState(string guestId) => new AccountState(
            AccountConnection.OfflineGuest,
            AccountCapability.Offline,
            AccountSyncState.LocalOnly,
            AccountOperation.None,
            guestId,
            string.Empty,
            "Playing offline. Progress stays on this device.");

        private static string ConflictMessage(SaveMergeConflictKind conflictKind)
        {
            return conflictKind == SaveMergeConflictKind.Birthday
                ? "Cloud backup needs a grown-up choice for private profile data."
                : "Cloud backup needs a choice between two progress versions.";
        }

        private void ClearConflict()
        {
            m_ConflictLocal = null;
            m_ConflictCloud = null;
            m_ConflictRemoteVersion = default;
            m_ConflictUserId = null;
        }
    }
}
