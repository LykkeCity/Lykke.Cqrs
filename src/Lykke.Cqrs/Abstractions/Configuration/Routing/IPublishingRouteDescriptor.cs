namespace Lykke.Cqrs.Configuration.Routing
{
    public interface IPublishingRouteDescriptor<out T> : IDescriptor<IRouteMap> 
    {
        T  With(string route);
    }
}