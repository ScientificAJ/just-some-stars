using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Saving
{
    public enum LoadSaveStatus
    {
        Missing = 0,
        LoadedPrimary = 1,
        RecoveredBackup = 2,
        Unreadable = 3,
        StorageUnavailable = 4,
    }

    public sealed class LoadSaveResult
    {
        private readonly GameSave m_Save;

        internal LoadSaveResult(
            LoadSaveStatus status,
            GameSave save,
            string userMessage)
        {
            Status = status;
            m_Save = save?.Copy();
            UserMessage = userMessage ?? string.Empty;
        }

        public LoadSaveStatus Status { get; }

        public bool HasSave => m_Save != null;

        public GameSave Save => m_Save?.Copy();

        public string UserMessage { get; }

        internal LoadSaveResult Copy()
        {
            return new LoadSaveResult(Status, m_Save, UserMessage);
        }
    }

    public interface ISaveService : IGameService
    {
        ValueTask<LoadSaveResult> LoadAsync(CancellationToken cancellationToken);

        ValueTask SaveCheckpointAsync(
            GameSave save,
            CancellationToken cancellationToken);

        ValueTask<LoadSaveResult> RecoverAsync(CancellationToken cancellationToken);

        GameSave Merge(GameSave local, GameSave cloud);
    }
}
