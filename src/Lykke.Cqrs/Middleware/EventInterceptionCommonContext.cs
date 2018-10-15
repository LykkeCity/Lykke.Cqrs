namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptionCommonContext
    {
        public object Event { get; set; }
        public object HandlerObject { get; internal set; }
        public ICommandSender CommandSender { get; set; }
    }
}
