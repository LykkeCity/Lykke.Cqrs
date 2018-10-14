namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptionContext
    {
        public object Event { get; set; }
        public object HandlerObject { get; internal set; }
        public ICommandSender CommandSender { get; set; }
    }
}
