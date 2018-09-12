using JetBrains.Annotations;
using Lykke.Cqrs.Routing;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for endpoints resolving.
    /// </summary>
    [PublicAPI]
    public interface IEndpointResolver
    {
        /// <summary>
        /// Resolves endpoint with provided route info.
        /// </summary>
        /// <param name="route">Route name.</param>
        /// <param name="key">Routing key.</param>
        /// <param name="endpointProvider">Endpoints provider.</param>
        /// <returns>Resolved endpoint.</returns>
        Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider);
    }
}