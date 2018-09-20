using System;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs.Configuration.Routing
{
    public class ExplicitEndpointDescriptor<TDescriptor, TRegistration> 
        where TDescriptor : RouteDescriptorBase<TRegistration> 
        where TRegistration : IRegistration
    {
        private readonly string m_Endpoint;
        private readonly TDescriptor m_Descriptor;

        public ExplicitEndpointDescriptor(string endpoint, TDescriptor descriptor)
        {
            m_Descriptor = descriptor;
            m_Endpoint = endpoint;
        }

        public TDescriptor For(Func<RoutingKey, bool> criteria)
        {
            m_Descriptor.AddExplicitEndpoint(criteria, m_Endpoint);
            return m_Descriptor;
        }

        public TDescriptor ForAllRoutingKey()
        {
            m_Descriptor.AddExplicitEndpoint(key => true, m_Endpoint);
            return m_Descriptor;
        }
    }
}