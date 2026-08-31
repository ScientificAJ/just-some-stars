using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Saving
{
    public sealed class CloudCheckpointSaveService : ISaveService
    {
        private readonly ISaveService m_Local;
        private readonly IAccountService m_Account;

        public CloudCheckpointSaveService(
            ISaveService local,
            IAccountService account)
        {
            m_Local = local ?? throw new ArgumentNullException(nameof(local));
            m_Account = account ?? throw new ArgumentNullException(nameof(account));
        }

        public bool IsInitialized =>
            m_Local is LocalSaveService local && local.IsInitialized;

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken) =>
            m_Local.InitializeAsync(cancellationToken);

        public ValueTask<LoadSaveResult> LoadAsync(
            CancellationToken cancellationToken) =>
            m_Local.LoadAsync(cancellationToken);

        public async ValueTask SaveCheckpointAsync(
            GameSave save,
            CancellationToken cancellationToken)
        {
            await m_Local.SaveCheckpointAsync(save, cancellationToken);
            var state = m_Account.Current;
            if (state == null ||
                string.IsNullOrWhiteSpace(state.FirebaseUserId) ||
                (state.Connection != AccountConnection.Linked &&
                 state.Connection != AccountConnection.Pending))
            {
                return;
            }

            try
            {
                await m_Account.SyncAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The local checkpoint is already durable. Cloud retry is optional.
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                // The local checkpoint is already durable. Cloud retry is optional.
            }
        }

        public ValueTask<LoadSaveResult> RecoverAsync(
            CancellationToken cancellationToken) =>
            m_Local.RecoverAsync(cancellationToken);

        public GameSave Merge(GameSave local, GameSave cloud) =>
            m_Local.Merge(local, cloud);

        public ValueTask ShutdownAsync() => m_Local.ShutdownAsync();

        private static bool IsNonFatal(Exception exception) =>
            !(exception is OutOfMemoryException) &&
            !(exception is StackOverflowException) &&
            !(exception is AccessViolationException);
    }
}
