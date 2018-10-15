namespace Lykke.Cqrs.Middleware
{
    internal class CommandInterceptionCommonContext
    {
        public object Command { get; set; }
        public object HandlerObject { get; internal set; }
        public IEventPublisher EventPublisher { get; set; }
    }
}
