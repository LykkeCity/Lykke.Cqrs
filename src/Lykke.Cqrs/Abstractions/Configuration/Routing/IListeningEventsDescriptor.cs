namespace Lykke.Cqrs.Configuration.Routing
{
    public interface IListeningEventsDescriptor<TRegistration> where TRegistration : IRegistration
    {
        IListeningRouteDescriptor<ListeningEventsDescriptor<TRegistration>> From(string boundedContext);
    }
}