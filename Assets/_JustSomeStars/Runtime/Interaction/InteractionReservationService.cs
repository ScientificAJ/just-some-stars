using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Interaction
{
    public sealed class InteractionReservationService
    {
        private readonly object m_Gate = new object();
        private readonly Func<double> m_NowSeconds;
        private readonly Dictionary<long, ReservationState> m_Reservations =
            new Dictionary<long, ReservationState>();
        private readonly Dictionary<ContentId, HashSet<long>> m_ByAnchor =
            new Dictionary<ContentId, HashSet<long>>();
        private long m_NextReservationId;

        public InteractionReservationService(Func<double> nowSeconds = null)
        {
            m_NowSeconds = nowSeconds ?? MonotonicSeconds;
        }

        public int ActiveLeaseCount
        {
            get
            {
                List<CancellationTokenRegistration> registrations;
                int count;
                lock (m_Gate)
                {
                    registrations = PruneExpiredLocked(m_NowSeconds());
                    count = m_Reservations.Count;
                }

                DisposeRegistrations(registrations);
                return count;
            }
        }

        public bool TryReserve(
            ContentId anchorId,
            ContentId actorId,
            bool exclusive,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            out InteractionReservationLease lease)
        {
            RequireId(anchorId, nameof(anchorId));
            RequireId(actorId, nameof(actorId));
            var timeoutSeconds = RequireTimeout(timeout);
            cancellationToken.ThrowIfCancellationRequested();

            ReservationState state = null;
            List<CancellationTokenRegistration> expired;
            lock (m_Gate)
            {
                var now = m_NowSeconds();
                expired = PruneExpiredLocked(now);
                if (CanReserveLocked(anchorId, exclusive))
                {
                    var reservationId = ++m_NextReservationId;
                    state = new ReservationState(
                        reservationId,
                        anchorId,
                        actorId,
                        exclusive,
                        now + timeoutSeconds);
                    m_Reservations.Add(reservationId, state);
                    if (!m_ByAnchor.TryGetValue(anchorId, out var reservationIds))
                    {
                        reservationIds = new HashSet<long>();
                        m_ByAnchor.Add(anchorId, reservationIds);
                    }

                    reservationIds.Add(reservationId);
                }
            }

            DisposeRegistrations(expired);
            if (state == null)
            {
                lease = null;
                return false;
            }

            if (cancellationToken.CanBeCanceled)
            {
                var registration = cancellationToken.Register(
                    () => ReleaseFromCancellation(state.ReservationId));
                var keepRegistration = false;
                lock (m_Gate)
                {
                    if (m_Reservations.TryGetValue(
                            state.ReservationId,
                            out var activeState))
                    {
                        activeState.CancellationRegistration = registration;
                        activeState.HasCancellationRegistration = true;
                        keepRegistration = true;
                    }
                }

                if (!keepRegistration)
                {
                    registration.Dispose();
                }
            }

            lease = new InteractionReservationLease(
                this,
                state.ReservationId,
                anchorId,
                actorId);
            return true;
        }

        public int SweepExpired()
        {
            List<CancellationTokenRegistration> registrations;
            int removed;
            lock (m_Gate)
            {
                var before = m_Reservations.Count;
                registrations = PruneExpiredLocked(m_NowSeconds());
                removed = before - m_Reservations.Count;
            }

            DisposeRegistrations(registrations);
            return removed;
        }

        internal bool IsActive(long reservationId)
        {
            List<CancellationTokenRegistration> registrations;
            bool active;
            lock (m_Gate)
            {
                registrations = PruneExpiredLocked(m_NowSeconds());
                active = m_Reservations.ContainsKey(reservationId);
            }

            DisposeRegistrations(registrations);
            return active;
        }

        internal bool Renew(long reservationId, TimeSpan timeout)
        {
            var timeoutSeconds = RequireTimeout(timeout);
            List<CancellationTokenRegistration> registrations;
            bool renewed;
            lock (m_Gate)
            {
                var now = m_NowSeconds();
                registrations = PruneExpiredLocked(now);
                renewed = m_Reservations.TryGetValue(
                    reservationId,
                    out var state);
                if (renewed)
                {
                    state.ExpiresAtSeconds = now + timeoutSeconds;
                }
            }

            DisposeRegistrations(registrations);
            return renewed;
        }

        internal void Release(long reservationId)
        {
            CancellationTokenRegistration registration = default;
            var hasRegistration = false;
            lock (m_Gate)
            {
                if (!m_Reservations.TryGetValue(reservationId, out var state))
                {
                    return;
                }

                RemoveLocked(state);
                registration = state.CancellationRegistration;
                hasRegistration = state.HasCancellationRegistration;
            }

            if (hasRegistration)
            {
                registration.Dispose();
            }
        }

        private void ReleaseFromCancellation(long reservationId)
        {
            lock (m_Gate)
            {
                if (m_Reservations.TryGetValue(
                        reservationId,
                        out var state))
                {
                    RemoveLocked(state);
                }
            }
        }

        private bool CanReserveLocked(ContentId anchorId, bool exclusive)
        {
            if (!m_ByAnchor.TryGetValue(anchorId, out var reservationIds) ||
                reservationIds.Count == 0)
            {
                return true;
            }

            if (exclusive)
            {
                return false;
            }

            foreach (var reservationId in reservationIds)
            {
                if (m_Reservations[reservationId].Exclusive)
                {
                    return false;
                }
            }

            return true;
        }

        private List<CancellationTokenRegistration> PruneExpiredLocked(
            double nowSeconds)
        {
            List<ReservationState> expired = null;
            foreach (var reservation in m_Reservations.Values)
            {
                if (reservation.ExpiresAtSeconds <= nowSeconds)
                {
                    expired ??= new List<ReservationState>();
                    expired.Add(reservation);
                }
            }

            if (expired == null)
            {
                return null;
            }

            var registrations = new List<CancellationTokenRegistration>();
            foreach (var reservation in expired)
            {
                RemoveLocked(reservation);
                if (reservation.HasCancellationRegistration)
                {
                    registrations.Add(reservation.CancellationRegistration);
                }
            }

            return registrations;
        }

        private void RemoveLocked(ReservationState reservation)
        {
            m_Reservations.Remove(reservation.ReservationId);
            if (!m_ByAnchor.TryGetValue(
                    reservation.AnchorId,
                    out var reservationIds))
            {
                return;
            }

            reservationIds.Remove(reservation.ReservationId);
            if (reservationIds.Count == 0)
            {
                m_ByAnchor.Remove(reservation.AnchorId);
            }
        }

        private static void DisposeRegistrations(
            List<CancellationTokenRegistration> registrations)
        {
            if (registrations == null)
            {
                return;
            }

            foreach (var registration in registrations)
            {
                registration.Dispose();
            }
        }

        private static double RequireTimeout(TimeSpan timeout)
        {
            var seconds = timeout.TotalSeconds;
            if (seconds <= 0d || double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeout),
                    "Reservation timeout must be positive and finite.");
            }

            return seconds;
        }

        private static void RequireId(ContentId contentId, string parameterName)
        {
            if (!contentId.IsValid)
            {
                throw new ArgumentException(
                    "Reservation IDs must be valid content IDs.",
                    parameterName);
            }
        }

        private static double MonotonicSeconds()
        {
            return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        private sealed class ReservationState
        {
            public ReservationState(
                long reservationId,
                ContentId anchorId,
                ContentId actorId,
                bool exclusive,
                double expiresAtSeconds)
            {
                ReservationId = reservationId;
                AnchorId = anchorId;
                ActorId = actorId;
                Exclusive = exclusive;
                ExpiresAtSeconds = expiresAtSeconds;
            }

            public long ReservationId { get; }
            public ContentId AnchorId { get; }
            public ContentId ActorId { get; }
            public bool Exclusive { get; }
            public double ExpiresAtSeconds { get; set; }
            public CancellationTokenRegistration CancellationRegistration { get; set; }
            public bool HasCancellationRegistration { get; set; }
        }
    }

    public sealed class InteractionReservationLease : IDisposable
    {
        private InteractionReservationService m_Owner;
        private readonly long m_ReservationId;

        internal InteractionReservationLease(
            InteractionReservationService owner,
            long reservationId,
            ContentId anchorId,
            ContentId actorId)
        {
            m_Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            m_ReservationId = reservationId;
            AnchorId = anchorId;
            ActorId = actorId;
        }

        public ContentId AnchorId { get; }
        public ContentId ActorId { get; }
        public bool IsActive => m_Owner?.IsActive(m_ReservationId) == true;

        public bool Renew(TimeSpan timeout)
        {
            return m_Owner?.Renew(m_ReservationId, timeout) == true;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref m_Owner, null);
            owner?.Release(m_ReservationId);
        }
    }
}
