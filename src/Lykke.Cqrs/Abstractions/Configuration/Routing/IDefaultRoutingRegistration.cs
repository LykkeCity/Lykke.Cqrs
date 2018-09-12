using System;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Interface for default routing registration.
    /// </summary>
    [PublicAPI]
    public interface IDefaultRoutingRegistration : IRegistration, IHideObjectMembers
    {
        /// <summary>
        /// Registers command types that will be published on default routing.
        /// </summary>
        /// <param name="commandsTypes">Command types array.</param>
        /// <returns>Fluent API interface for target context specification.</returns>
        IPublishingCommandsDescriptor<IDefaultRoutingRegistration> PublishingCommands(params Type[] commandsTypes);
    }
}