using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Processing options descriptor class.
    /// </summary>
    [PublicAPI]
    public class ProcessingOptionsDescriptor<TRegistrtaion> : RegistrationWrapper<TRegistrtaion>, IDescriptor<IRouteMap>
        where TRegistrtaion : IRegistration
    {
        private readonly string m_Route;

        private uint m_ThreadCount;
        private uint? m_QueueCapacity;

        /// <summary>
        /// C-tor.
        /// </summary>
        public ProcessingOptionsDescriptor(TRegistrtaion registration, string route) : base(registration)
        {
            m_Route = route;
        }

        /// <inheritdoc cref="IDescriptor{TSubject}"/>
        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        /// <inheritdoc cref="IDescriptor{TSubject}"/>
        public void Create(IRouteMap routeMap, IDependencyResolver resolver)
        {
            routeMap[m_Route].ProcessingGroup.ConcurrencyLevel = m_ThreadCount;
            if(m_QueueCapacity!=null)
                routeMap[m_Route].ProcessingGroup.QueueCapacity = m_QueueCapacity.Value;
        }

        /// <inheritdoc cref="IDescriptor{TSubject}"/>
        public void Process(IRouteMap routeMap, CqrsEngine cqrsEngine)
        {
        }

        /// <summary>
        /// Specifies number of concurrent threads for route messages processing in messaging engine.
        /// </summary>
        /// <param name="threadCount">Number of concurrent threads.</param>
        /// <returns>Same fluent API object.</returns>
        public ProcessingOptionsDescriptor<TRegistrtaion> MultiThreaded(uint threadCount)
        {
            if (threadCount == 0)
                throw new ArgumentOutOfRangeException(nameof(threadCount), "threadCount should be greater then 0");
            m_ThreadCount = threadCount;
            return this;
        }

        /// <summary>
        /// Specifies queue capacity for route messages processing in messaging engine.
        /// </summary>
        /// <param name="queueCapacity">Queue capacity.</param>
        /// <returns>Same fluent API object.</returns>
        public ProcessingOptionsDescriptor<TRegistrtaion> QueueCapacity(uint queueCapacity)
        {
            m_QueueCapacity = queueCapacity;
            return this;
        }
    }
}