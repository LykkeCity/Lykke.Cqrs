using System;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    public class DefaultEndpointResolver : IEndpointResolver
    {
        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            if (endpointProvider.Contains(route))
            {
                return endpointProvider.Get(route);
            }
            throw new ApplicationException(string.Format("Endpoint '{0}' not found",route));
        }
    }
}