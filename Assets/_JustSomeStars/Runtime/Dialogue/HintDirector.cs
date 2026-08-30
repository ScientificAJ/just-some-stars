using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Dialogue
{
    public sealed class HintRule
    {
        public HintRule(string objectiveId, DialogueEntry entry, int attemptThreshold)
            : this(objectiveId, objectiveId, entry, attemptThreshold)
        {
        }

        public HintRule(
            string objectiveId,
            string behaviorSubjectId,
            DialogueEntry entry,
            int attemptThreshold)
        {
            ObjectiveId = new ContentId(objectiveId);
            BehaviorSubjectId = new ContentId(behaviorSubjectId);
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            if (attemptThreshold < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptThreshold));
            }

            AttemptThreshold = attemptThreshold;
        }

        public ContentId ObjectiveId { get; }
        public ContentId BehaviorSubjectId { get; }
        public DialogueEntry Entry { get; }
        public int AttemptThreshold { get; }
    }

    public sealed class HintDirector : IDisposable
    {
        private readonly IDialogueQueue m_Dialogue;
        private readonly AssistLevel m_Assist;
        private readonly Dictionary<ContentId, HintRule> m_Rules;
        private readonly IDisposable m_Subscription;
        private ContentId m_Objective;
        private int m_Attempts;
        private bool m_HintSent;
        private bool m_Disposed;

        public HintDirector(
            GameEventBus gameEvents,
            IDialogueQueue dialogue,
            AssistLevel assist,
            IEnumerable<HintRule> rules)
        {
            if (!Enum.IsDefined(typeof(AssistLevel), assist))
            {
                throw new ArgumentOutOfRangeException(nameof(assist));
            }

            m_Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            m_Assist = assist;
            m_Rules = new Dictionary<ContentId, HintRule>();
            foreach (var rule in rules ?? throw new ArgumentNullException(nameof(rules)))
            {
                if (rule == null || !m_Rules.TryAdd(rule.ObjectiveId, rule))
                {
                    throw new ArgumentException(
                        "Hint rules must be non-null and objective-unique.",
                        nameof(rules));
                }
            }

            m_Subscription = (gameEvents ?? throw new ArgumentNullException(nameof(gameEvents)))
                .Subscribe<PlayerBehaviorObserved>(Observe);
        }

        public void SetObjective(ContentId objectiveId)
        {
            ThrowIfDisposed();
            if (!objectiveId.IsValid)
            {
                throw new ArgumentException("Hint objective requires a valid ID.", nameof(objectiveId));
            }

            if (m_Objective != objectiveId)
            {
                m_Objective = objectiveId;
                m_Attempts = 0;
                m_HintSent = false;
            }
        }

        public void CompleteObjective(ContentId objectiveId)
        {
            ThrowIfDisposed();
            if (m_Objective == objectiveId)
            {
                m_Objective = default;
                m_Attempts = 0;
                m_HintSent = false;
            }
        }

        public bool RequestHint()
        {
            ThrowIfDisposed();
            if (!m_Objective.IsValid || !m_Rules.TryGetValue(m_Objective, out var rule))
            {
                return false;
            }

            m_Dialogue.Enqueue(rule.Entry);
            m_HintSent = true;
            return true;
        }

        public void TickElapsedSeconds(double elapsedSeconds)
        {
            ThrowIfDisposed();
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) ||
                double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            // Deliberately no timer behavior: hints react to play outcomes only.
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Subscription.Dispose();
            m_Disposed = true;
        }

        private void Observe(PlayerBehaviorObserved observation)
        {
            if (m_Disposed || m_HintSent ||
                !m_Rules.TryGetValue(m_Objective, out var rule))
            {
                return;
            }

            if (observation.SubjectId != rule.BehaviorSubjectId)
            {
                return;
            }

            m_Attempts++;
            var configuredThreshold = m_Assist switch
            {
                AssistLevel.Guided => 1,
                AssistLevel.Balanced => Math.Max(2, rule.AttemptThreshold),
                AssistLevel.Ace => int.MaxValue,
                _ => throw new InvalidOperationException("Unknown assist level."),
            };
            if (m_Attempts >= configuredThreshold)
            {
                m_Dialogue.Enqueue(rule.Entry);
                m_HintSent = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(HintDirector));
            }
        }
    }
}
