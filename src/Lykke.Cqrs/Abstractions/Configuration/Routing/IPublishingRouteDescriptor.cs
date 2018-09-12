using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Fluent API interface for route specification.
    /// </summary>
    [PublicAPI]
    public interface IPublishingRouteDescriptor<out T> : IDescriptor<IRouteMap> 
    {
        /// <summary>
        /// Specifies route name for messages publishing.
        /// </summary>
        /// <param name="route">Route name.</param>
        /// <returns>Parent fluent API interface.</returns>
        T  With(string route);
    }
}