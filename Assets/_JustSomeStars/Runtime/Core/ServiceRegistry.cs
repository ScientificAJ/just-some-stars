using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JustSomeStars.Runtime.Core
{
    public sealed class ServiceRegistry
    {
        private readonly object m_Gate = new object();
        private readonly Dictionary<Type, object> m_Services =
            new Dictionary<Type, object>();
        private readonly List<Type> m_RegistrationOrder = new List<Type>();

        public IReadOnlyList<Type> RegisteredContracts
        {
            get
            {
                lock (m_Gate)
                {
                    return new ReadOnlyCollection<Type>(
                        m_RegistrationOrder.ToArray());
                }
            }
        }

        public void Register<T>(T service)
            where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var contract = typeof(T);
            lock (m_Gate)
            {
                if (m_Services.ContainsKey(contract))
                {
                    throw new InvalidOperationException(
                        $"A service is already registered for contract '{contract.FullName}'.");
                }

                m_Services.Add(contract, service);
                m_RegistrationOrder.Add(contract);
            }
        }

        public T Get<T>()
            where T : class
        {
            if (TryGet<T>(out var service))
            {
                return service;
            }

            var contract = typeof(T);
            throw new KeyNotFoundException(
                $"No service is registered for contract '{contract.FullName}'.");
        }

        public bool TryGet<T>(out T service)
            where T : class
        {
            lock (m_Gate)
            {
                if (m_Services.TryGetValue(typeof(T), out var registered))
                {
                    service = (T)registered;
                    return true;
                }
            }

            service = null;
            return false;
        }
    }
}
