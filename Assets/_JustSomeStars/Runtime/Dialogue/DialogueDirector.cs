using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using UnityEngine;

namespace JustSomeStars.Runtime.Dialogue
{
    public enum DialogueOutcome
    {
        Completed = 0,
        Interrupted = 1,
        Cooldown = 2,
        Blocked = 3,
        Cancelled = 4,
    }

    public interface IDialogueClock
    {
        double NowSeconds { get; }
    }

    public interface IDialoguePresenter
    {
        Task PresentAsync(DialogueEntry entry, CancellationToken cancellationToken);
    }

    public interface IDialogueQueue
    {
        void Enqueue(DialogueEntry entry);
    }

    public sealed class DialogueDirector : IDisposable, IDialogueQueue
    {
        private readonly object m_Gate = new object();
        private readonly GameEventBus m_Events;
        private readonly DialogueTokenArbiter m_Arbiter;
        private readonly IDialoguePresenter m_Presenter;
        private readonly IDialogueClock m_Clock;
        private readonly Dictionary<ContentId, DialogueEntry> m_Catalog;
        private readonly Dictionary<ContentId, double> m_LastStarted =
            new Dictionary<ContentId, double>();
        private readonly List<DialogueEntry> m_Pending = new List<DialogueEntry>();

        private Task m_CurrentCompletion = Task.CompletedTask;
        private DialogueToken m_CurrentToken;
        private bool m_Disposed;

        public DialogueDirector(
            GameEventBus gameEvents,
            DialogueTokenArbiter arbiter,
            IDialoguePresenter presenter,
            IDialogueClock clock,
            IEnumerable<DialogueEntry> catalog)
        {
            m_Events = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
            m_Arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
            m_Presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            m_Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            var entries = (catalog ?? throw new ArgumentNullException(nameof(catalog)))
                .ToArray();
            if (entries.Any(entry => entry == null))
            {
                throw new ArgumentException("Dialogue catalog cannot contain null entries.", nameof(catalog));
            }

            foreach (var entry in entries)
            {
                entry.ValidateOrThrow();
            }

            m_Catalog = entries.GroupBy(entry => entry.StableId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count() == 1
                        ? group.Single()
                        : throw new ArgumentException(
                            $"Dialogue ID '{group.Key}' is duplicated.",
                            nameof(catalog)));
            ValidateFollowups();
        }

        public IReadOnlyList<DialogueEntry> Pending
        {
            get
            {
                lock (m_Gate)
                {
                    return m_Pending.ToArray();
                }
            }
        }

        public void Enqueue(DialogueEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            lock (m_Gate)
            {
                ThrowIfDisposed();
            }

            _ = ObserveQueuedAsync(RequestAsync(entry, CancellationToken.None));
        }

        public Task<DialogueOutcome> RequestAsync(
            DialogueEntry entry,
            CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            entry.ValidateOrThrow();
            lock (m_Gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                var now = m_Clock.NowSeconds;
                RequireClock(now);
                if (m_LastStarted.TryGetValue(entry.StableId, out var last) &&
                    now < last + entry.CooldownSeconds)
                {
                    return Task.FromResult(DialogueOutcome.Cooldown);
                }

                var previous = m_CurrentCompletion;
                if (!m_Arbiter.TryAcquire(
                        entry.StableId,
                        (int)entry.Priority,
                        entry.Interruptible,
                        out var token))
                {
                    EnqueueLocked(entry);
                    return Task.FromResult(DialogueOutcome.Blocked);
                }

                m_LastStarted[entry.StableId] = now;
                m_CurrentToken = token;
                var operation = RunAfterPreviousAsync(
                    entry,
                    token,
                    previous,
                    cancellationToken);
                m_CurrentCompletion = operation;
                return operation;
            }
        }

        public Task<DialogueOutcome> DrainNextAsync(CancellationToken cancellationToken)
        {
            DialogueEntry next;
            lock (m_Gate)
            {
                ThrowIfDisposed();
                if (m_Pending.Count == 0)
                {
                    return Task.FromResult(DialogueOutcome.Blocked);
                }

                next = m_Pending[0];
                m_Pending.RemoveAt(0);
            }

            return RequestAsync(next, cancellationToken);
        }

        public void Dispose()
        {
            lock (m_Gate)
            {
                if (m_Disposed)
                {
                    return;
                }

                m_Disposed = true;
                m_CurrentToken?.Dispose();
                m_CurrentToken = null;
                m_Pending.Clear();
            }
        }

        private async Task<DialogueOutcome> RunAfterPreviousAsync(
            DialogueEntry entry,
            DialogueToken token,
            Task previous,
            CancellationToken callerCancellation)
        {
            try
            {
                try
                {
                    await previous;
                }
                catch (OperationCanceledException)
                {
                    // Preempted presentations terminate before replacement starts.
                }
                catch
                {
                    // A previous presenter fault cannot retain the shared token.
                }

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    callerCancellation,
                    token.CancellationToken);
                await PresentChainAsync(entry, linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                m_Events.Publish(new ConversationCompleted(entry.StableId));

                return DialogueOutcome.Completed;
            }
            catch (OperationCanceledException) when (!callerCancellation.IsCancellationRequested)
            {
                return DialogueOutcome.Interrupted;
            }
            catch (OperationCanceledException)
            {
                return DialogueOutcome.Cancelled;
            }
            finally
            {
                token.Dispose();
                lock (m_Gate)
                {
                    if (ReferenceEquals(m_CurrentToken, token))
                    {
                        m_CurrentToken = null;
                    }
                }
            }
        }

        private async Task PresentChainAsync(
            DialogueEntry entry,
            CancellationToken cancellationToken)
        {
            await m_Presenter.PresentAsync(entry, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var followupId in entry.FollowupIds)
            {
                await PresentChainAsync(
                    m_Catalog[new ContentId(followupId)],
                    cancellationToken);
            }
        }

        private async Task ObserveQueuedAsync(Task<DialogueOutcome> operation)
        {
            try
            {
                var outcome = await operation;
                if (outcome == DialogueOutcome.Blocked)
                {
                    return;
                }

                DialogueEntry next = null;
                lock (m_Gate)
                {
                    if (!m_Disposed && m_CurrentToken == null && m_Pending.Count > 0)
                    {
                        next = m_Pending[0];
                        m_Pending.RemoveAt(0);
                    }
                }

                if (next != null)
                {
                    await ObserveQueuedAsync(RequestAsync(next, CancellationToken.None));
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ValidateFollowups()
        {
            foreach (var entry in m_Catalog.Values)
            {
                foreach (var followup in entry.FollowupIds)
                {
                    if (!m_Catalog.ContainsKey(new ContentId(followup)))
                    {
                        throw new ArgumentException(
                            $"Dialogue '{entry.StableId}' references missing follow-up '{followup}'.");
                    }
                }
            }

            foreach (var entry in m_Catalog.Values)
            {
                DetectCycle(entry.StableId, new HashSet<ContentId>(), new HashSet<ContentId>());
            }
        }

        private void DetectCycle(
            ContentId current,
            ISet<ContentId> visiting,
            ISet<ContentId> visited)
        {
            if (visited.Contains(current))
            {
                return;
            }

            if (!visiting.Add(current))
            {
                throw new ArgumentException(
                    $"Dialogue follow-up graph contains a cycle at '{current}'.");
            }

            foreach (var followup in m_Catalog[current].FollowupIds)
            {
                DetectCycle(new ContentId(followup), visiting, visited);
            }

            visiting.Remove(current);
            visited.Add(current);
        }

        private void EnqueueLocked(DialogueEntry entry)
        {
            if (!m_Pending.Any(candidate => candidate.StableId == entry.StableId))
            {
                m_Pending.Add(entry);
                m_Pending.Sort(CompareEntries);
            }
        }

        private static int CompareEntries(DialogueEntry left, DialogueEntry right)
        {
            var priority = right.Priority.CompareTo(left.Priority);
            return priority != 0
                ? priority
                : StringComparer.Ordinal.Compare(
                    left.StableId.Value,
                    right.StableId.Value);
        }

        private static void RequireClock(double now)
        {
            if (now < 0 || double.IsNaN(now) || double.IsInfinity(now))
            {
                throw new InvalidOperationException(
                    "Dialogue clock must be finite and monotonic from zero.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(DialogueDirector));
            }
        }
    }
}
