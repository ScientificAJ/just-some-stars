using System;
using System.Collections.Generic;
using System.Linq;

namespace JustSomeStars.Runtime.Core
{
    public enum StartupResultState
    {
        Unknown = 0,
        Available = 1,
        Unavailable = 2,
        Failed = 3,
    }

    public readonly struct StartupResult
    {
        private StartupResult(
            StartupResultState state,
            string message,
            Exception failure)
        {
            State = state;
            Message = message ?? string.Empty;
            Failure = failure;
        }

        public StartupResultState State { get; }

        public string Message { get; }

        public Exception Failure { get; }

        public bool IsAvailable => State == StartupResultState.Available;

        public static StartupResult Available()
        {
            return new StartupResult(
                StartupResultState.Available,
                string.Empty,
                null);
        }

        public static StartupResult Unavailable(
            string message,
            Exception failure = null)
        {
            return new StartupResult(
                StartupResultState.Unavailable,
                RequireMessage(message),
                failure);
        }

        public static StartupResult Failed(
            string message,
            Exception failure = null)
        {
            return new StartupResult(
                StartupResultState.Failed,
                RequireMessage(message),
                failure);
        }

        private static string RequireMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "An unavailable or failed startup result requires a message.",
                    nameof(message));
            }

            return message.Trim();
        }
    }

    public enum GameServiceRole
    {
        Settings = 0,
        LocalSave = 1,
        Input = 2,
        ContentCatalogue = 3,
        ModeController = 4,
        Cloud = 5,
        Commerce = 6,
        Notifications = 7,
        Attribution = 8,
        Growth = 9,
        Progression = 10,
        QualityProfile = 11,
    }

    public enum ServiceRequirement
    {
        Required = 0,
        Optional = 1,
    }

    public enum ServiceStartupState
    {
        Available = 0,
        Unavailable = 1,
        Failed = 2,
        Cancelled = 3,
    }

    public sealed class ServiceStartupReport
    {
        internal ServiceStartupReport(
            GameServiceRole role,
            ServiceRequirement requirement,
            ServiceStartupState state,
            string message,
            Exception failure)
        {
            Role = role;
            Requirement = requirement;
            State = state;
            Message = message ?? string.Empty;
            Failure = failure;
        }

        public GameServiceRole Role { get; }

        public ServiceRequirement Requirement { get; }

        public ServiceStartupState State { get; }

        public string Message { get; }

        public Exception Failure { get; }
    }

    public sealed class StartupReport
    {
        private readonly object m_Gate = new object();
        private readonly List<ServiceStartupReport> m_Services =
            new List<ServiceStartupReport>();
        private readonly List<Exception> m_CleanupFailures =
            new List<Exception>();

        private Exception m_PrimaryFailure;
        private bool m_IsCancelled;
        private bool m_RoutedToFrontend;
        private string m_RequestedDestination;

        internal StartupReport()
        {
        }

        public IReadOnlyList<ServiceStartupReport> Services
        {
            get
            {
                lock (m_Gate)
                {
                    return m_Services.ToArray();
                }
            }
        }

        public Exception PrimaryFailure
        {
            get
            {
                lock (m_Gate)
                {
                    return m_PrimaryFailure;
                }
            }
        }

        public IReadOnlyList<Exception> CleanupFailures
        {
            get
            {
                lock (m_Gate)
                {
                    return m_CleanupFailures.ToArray();
                }
            }
        }

        public bool IsCancelled
        {
            get
            {
                lock (m_Gate)
                {
                    return m_IsCancelled;
                }
            }
        }

        public bool RoutedToFrontend
        {
            get
            {
                lock (m_Gate)
                {
                    return m_RoutedToFrontend;
                }
            }
        }

        public string RequestedDestination
        {
            get
            {
                lock (m_Gate)
                {
                    return m_RequestedDestination;
                }
            }
        }

        public bool IsSuccessful
        {
            get
            {
                lock (m_Gate)
                {
                    return m_PrimaryFailure == null && m_RoutedToFrontend;
                }
            }
        }

        internal void AddService(
            GameServiceRole role,
            ServiceRequirement requirement,
            ServiceStartupState state,
            string message,
            Exception failure)
        {
            lock (m_Gate)
            {
                m_Services.Add(new ServiceStartupReport(
                    role,
                    requirement,
                    state,
                    message,
                    failure));
            }
        }

        internal void Fail(Exception failure, bool isCancelled)
        {
            lock (m_Gate)
            {
                if (m_PrimaryFailure == null)
                {
                    m_PrimaryFailure = failure ??
                        throw new ArgumentNullException(nameof(failure));
                    m_IsCancelled = isCancelled;
                }
            }
        }

        internal void AddCleanupFailure(Exception failure)
        {
            lock (m_Gate)
            {
                m_CleanupFailures.Add(failure ??
                    throw new ArgumentNullException(nameof(failure)));
            }
        }

        internal void MarkRouted(string destination)
        {
            lock (m_Gate)
            {
                m_RoutedToFrontend = true;
                m_RequestedDestination = destination;
            }
        }
    }
}
