using System;
using Lykke.Cqrs.Configuration.Routing;

namespace Lykke.Cqrs.Configuration.Saga
{
    public interface ISagaRegistration : IRegistration, IHideObjectMembers
    {
        string Name { get; }
        IListeningEventsDescriptor<ISagaRegistration> ListeningEvents(params Type[] types);
        IPublishingCommandsDescriptor<ISagaRegistration> PublishingCommands(params Type[] commandsTypes);
        ProcessingOptionsDescriptor<ISagaRegistration> ProcessingOptions(string route);
    }
}