namespace Lykke.Cqrs.Configuration.Routing
{
    public interface IPublishingCommandsDescriptor<TRegistration> where TRegistration : IRegistration
    {
        IPublishingRouteDescriptor<PublishingCommandsDescriptor<TRegistration>> To(string boundedContext);
    }
}