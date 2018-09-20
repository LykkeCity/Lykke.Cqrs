using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    public interface IEndpointResolver
    {
        Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider);
    }
}