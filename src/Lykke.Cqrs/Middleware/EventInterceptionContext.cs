using System;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptionContext : IEventInterceptionContext
    {
        public object Event { get; set; }
        public object HandlerObject { get; internal set; }
        public ICommandSender CommandSender { get; set; }
        public Func<IEventInterceptor, IEventInterceptor> NextResolver { get; internal set; }
    }
}
