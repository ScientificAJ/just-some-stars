using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Runtime.Core
{
    public interface ISceneTransition
    {
        ValueTask RouteAsync(
            string destination,
            CancellationToken cancellationToken);
    }

    public sealed class UnitySceneTransition : ISceneTransition
    {
        public async ValueTask RouteAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentException(
                    "A scene destination is required.",
                    nameof(destination));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // LoadSceneAsync cannot be cancelled after Unity accepts the request.
            // From this call onward, report the actual completion rather than a
            // cancellation that cannot stop scene activation.
            var operation = SceneManager.LoadSceneAsync(
                destination,
                LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException(
                    $"Unity did not create a load operation for scene '{destination}'.");
            }

            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }
    }
}
