using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Fluent API interface for route name specification.
    /// </summary>
    [PublicAPI]
    public interface IListeningRouteDescriptor<out T> : IDescriptor<IRouteMap>
    {
        /// <summary>
        /// Specifies route name for messages listening.
        /// </summary>
        /// <param name="route">Route name.</param>
        /// <returns>Parent fluent API interface.</returns>
        T On(string route);
    }
}