using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Fluent API interface for target context specification.
    /// </summary>
    [PublicAPI]
    public interface IPublishingCommandsDescriptor<TRegistration> where TRegistration : IRegistration
    {
        /// <summary>
        /// Specifies target context for commands publishing.
        /// </summary>
        /// <param name="boundedContext">Target context name.</param>
        /// <returns>Fluent API interface for route specification.</returns>
        IPublishingRouteDescriptor<PublishingCommandsDescriptor<TRegistration>> To(string boundedContext);
    }
}