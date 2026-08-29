using System;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Crew
{
    public sealed class DialogueTokenArbiter
    {
        private readonly object m_Gate = new object();
        private long m_NextId;
        private DialogueToken m_Active;

        public ContentId Owner
        {
            get
            {
                lock (m_Gate)
                {
                    return m_Active?.Owner ?? default;
                }
            }
        }

        public int ActiveTokenCount
        {
            get
            {
                lock (m_Gate)
                {
                    return m_Active != null && m_Active.IsActive ? 1 : 0;
                }
            }
        }

        public bool TryAcquire(
            ContentId owner,
            int priority,
            bool interruptible,
            out DialogueToken token)
        {
            if (!owner.IsValid)
            {
                throw new ArgumentException(
                    "Dialogue owner requires a valid content ID.",
                    nameof(owner));
            }

            lock (m_Gate)
            {
                if (m_Active != null && m_Active.IsActive)
                {
                    if (!m_Active.Interruptible || priority <= m_Active.Priority)
                    {
                        token = null;
                        return false;
                    }

                    m_Active.Revoke();
                }

                token = new DialogueToken(
                    this,
                    ++m_NextId,
                    owner,
                    priority,
                    interruptible);
                m_Active = token;
                return true;
            }
        }

        internal void Release(long id)
        {
            lock (m_Gate)
            {
                if (m_Active != null && m_Active.Id == id)
                {
                    m_Active.Revoke();
                    m_Active = null;
                }
            }
        }
    }

    public sealed class DialogueToken : IDisposable
    {
        private DialogueTokenArbiter m_Owner;

        internal DialogueToken(
            DialogueTokenArbiter owner,
            long id,
            ContentId actorId,
            int priority,
            bool interruptible)
        {
            m_Owner = owner;
            Id = id;
            Owner = actorId;
            Priority = priority;
            Interruptible = interruptible;
            IsActive = true;
        }

        internal long Id { get; }
        public ContentId Owner { get; }
        public int Priority { get; }
        public bool Interruptible { get; }
        public bool IsActive { get; private set; }

        public void Dispose()
        {
            var owner = m_Owner;
            m_Owner = null;
            owner?.Release(Id);
            IsActive = false;
        }

        internal void Revoke()
        {
            IsActive = false;
            m_Owner = null;
        }
    }
}
