using System.Threading;
using System.Threading.Tasks;

namespace JustSomeStars.Runtime.Core
{
    public interface IGameService
    {
        /// <summary>
        /// Initializes the service. The cancellation token scopes only this
        /// asynchronous operation and must not be retained after it completes.
        /// Implementations must observe cancellation promptly, stop starting or
        /// acquiring child work, and settle this operation. Service lifetime ends
        /// through <see cref="ShutdownAsync"/>.
        ///
        /// A service that ignores cancellation and never settles is a contract
        /// breaker: bootstrap cleanup and every replacement remain deliberately
        /// blocked until it settles or the process restarts. The bootstrap never
        /// overlaps cleanup by preempting an in-flight initializer.
        /// </summary>
        ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Performs idempotent, best-effort cleanup. The bootstrap can call this
        /// after initialization succeeds, fails, throws, or is cancelled, so an
        /// implementation must safely release any partially acquired resources
        /// and must not complete until its owned work has reached quiescence.
        /// </summary>
        ValueTask ShutdownAsync();
    }
}
