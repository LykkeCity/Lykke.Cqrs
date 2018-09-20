using Lykke.Messaging.Contract;

namespace Lykke.Cqrs.InfrastructureCommands
{
    internal class RoutedCommand<T>
    {
        public RoutedCommand(T command, Endpoint originEndpoint, string originRoute)
        {
            Command = command;
            OriginEndpoint = originEndpoint;
            OriginRoute = originRoute;
        }

        public T Command { get; set; }
        public Endpoint OriginEndpoint { get; set; }
        public string OriginRoute { get; set; }
    }
}