using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Fluent API interface for source context specification.
    /// </summary>
    [PublicAPI]
    public interface IListeningEventsDescriptor<TRegistration> where TRegistration : IRegistration
    {
        /// <summary>
        /// Specifies source context for events listening.
        /// </summary>
        /// <param name="boundedContext">Source context name.</param>
        /// <returns>Fluent API interface for route specification.</returns>
        IListeningRouteDescriptor<ListeningEventsDescriptor<TRegistration>> From(string boundedContext);
    }
}