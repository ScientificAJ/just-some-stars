using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Missions
{
    public interface IChapterProgression : IGameService
    {
        ContentId ChapterId { get; }

        ContentId ApproachId { get; }

        string ResumeSceneName { get; }

        GameMode ResumeMode { get; }

        bool HasPendingDeparture { get; }

        bool IsActiveNode(string nodeId);

        Task FlushPendingAsync(CancellationToken cancellationToken);

        Task ConfirmDepartureAsync(CancellationToken cancellationToken);
    }

    public interface IChapterProgressionCoordinator : IChapterProgression
    {
        IChapterProgression ActiveProgression { get; }

        T RequireActive<T>() where T : class, IChapterProgression;
    }
}
