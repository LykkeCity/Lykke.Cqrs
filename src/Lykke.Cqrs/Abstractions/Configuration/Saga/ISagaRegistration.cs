using System;
using JetBrains.Annotations;
using Lykke.Cqrs.Configuration.Routing;

namespace Lykke.Cqrs.Configuration.Saga
{
    /// <summary>
    /// Fluent API interface for saga registration.
    /// </summary>
    [PublicAPI]
    public interface ISagaRegistration : IRegistration, IHideObjectMembers
    {
        /// <summary>
        /// Bounded context name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Registers events types for listening.
        /// </summary>
        /// <param name="types">Collection of event types.</param>
        /// <returns>Fluent API interface for source context specification.</returns>
        IListeningEventsDescriptor<ISagaRegistration> ListeningEvents(params Type[] types);

        /// <summary>
        /// Registers command types for publishing.
        /// </summary>
        /// <param name="commandsTypes">Collection of command types.</param>
        /// <returns>Fluent API interface for target context specification.</returns>
        IPublishingCommandsDescriptor<ISagaRegistration> PublishingCommands(params Type[] commandsTypes);

        /// <summary>
        /// Specifies route name for saga processing options.
        /// </summary>
        /// <param name="route"></param>
        /// <returns>Fluent API descriptor for processing group properties specification.</returns>
        ProcessingOptionsDescriptor<ISagaRegistration> ProcessingOptions(string route);
    }
}