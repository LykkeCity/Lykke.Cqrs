using System;
using System.Collections.Generic;
using Lykke.Cqrs.Configuration.Saga;

namespace Lykke.Cqrs.Configuration.Routing
{
    public class ListeningEventsDescriptor<TRegistration> 
        : ListeningRouteDescriptor<ListeningEventsDescriptor<TRegistration>,TRegistration>, IListeningEventsDescriptor<TRegistration>
        where TRegistration : IRegistration
    {
        private string m_BoundedContext;
        private readonly Type[] m_Types;

        public ListeningEventsDescriptor(TRegistration registration, Type[] types)
            : base(registration)
        {
            m_Types = types;
            Descriptor = this;
        }

        IListeningRouteDescriptor<ListeningEventsDescriptor<TRegistration>> IListeningEventsDescriptor<TRegistration>.From(string boundedContext)
        {
            m_BoundedContext = boundedContext;
            return this;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(IRouteMap routeMap, IDependencyResolver resolver)
        {
            foreach (var type in m_Types)
            {
                routeMap[Route].AddSubscribedEvent(
                    type,
                    0,
                    m_BoundedContext,
                    EndpointResolver,
                    // Set exclusive for projections - only projections can subscribe to the events besides sagas
                    typeof(TRegistration) != typeof(ISagaRegistration));
            }
        }

        public override void Process(IRouteMap routeMap, CqrsEngine cqrsEngine)
        {
            EndpointResolver.SetFallbackResolver(cqrsEngine.EndpointResolver);
        }
    }
}