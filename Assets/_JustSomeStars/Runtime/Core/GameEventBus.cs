using System;
using System.Collections.Generic;
using System.Threading;

namespace JustSomeStars.Runtime.Core
{
    public sealed class GameEventBus
    {
        private readonly object m_Gate = new object();
        private readonly Dictionary<Type, List<object>> m_Subscriptions =
            new Dictionary<Type, List<object>>();

        public IDisposable Subscribe<TEvent>(Action<TEvent> subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            var subscription = new Subscription<TEvent>(this, subscriber);
            lock (m_Gate)
            {
                var eventType = typeof(TEvent);
                if (!m_Subscriptions.TryGetValue(eventType, out var subscriptions))
                {
                    subscriptions = new List<object>();
                    m_Subscriptions.Add(eventType, subscriptions);
                }

                subscriptions.Add(subscription);
            }

            return subscription;
        }

        public void Publish<TEvent>(TEvent gameEvent)
        {
            Subscription<TEvent>[] snapshot;
            lock (m_Gate)
            {
                if (!m_Subscriptions.TryGetValue(
                        typeof(TEvent),
                        out var subscriptions) ||
                    subscriptions.Count == 0)
                {
                    return;
                }

                snapshot = new Subscription<TEvent>[subscriptions.Count];
                for (var index = 0; index < subscriptions.Count; index++)
                {
                    snapshot[index] = (Subscription<TEvent>)subscriptions[index];
                }
            }

            foreach (var subscription in snapshot)
            {
                subscription.Invoke(gameEvent);
            }
        }

        private void Remove<TEvent>(Subscription<TEvent> subscription)
        {
            lock (m_Gate)
            {
                var eventType = typeof(TEvent);
                if (!m_Subscriptions.TryGetValue(eventType, out var subscriptions))
                {
                    return;
                }

                subscriptions.Remove(subscription);
                if (subscriptions.Count == 0)
                {
                    m_Subscriptions.Remove(eventType);
                }
            }
        }

        private sealed class Subscription<TEvent> : IDisposable
        {
            private GameEventBus m_Owner;
            private Action<TEvent> m_Subscriber;
            private int m_IsDisposed;

            public Subscription(
                GameEventBus owner,
                Action<TEvent> subscriber)
            {
                m_Owner = owner;
                m_Subscriber = subscriber;
            }

            public void Invoke(TEvent gameEvent)
            {
                if (Volatile.Read(ref m_IsDisposed) == 0)
                {
                    m_Subscriber(gameEvent);
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref m_IsDisposed, 1) != 0)
                {
                    return;
                }

                var owner = Interlocked.Exchange(ref m_Owner, null);
                owner?.Remove(this);
            }
        }
    }
}
