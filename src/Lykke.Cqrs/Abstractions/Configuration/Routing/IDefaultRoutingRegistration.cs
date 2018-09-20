using System;

namespace Lykke.Cqrs.Configuration.Routing
{
    public interface IDefaultRoutingRegistration : IRegistration, IHideObjectMembers
    {
        IPublishingCommandsDescriptor<IDefaultRoutingRegistration> PublishingCommands(params Type[] commandsTypes);
    }
}