using System;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class CommandInterceptionContext : ICommandInterceptionContext
    {
        public object Command { get; set; }
        public object HandlerObject { get; internal set; }
        public IEventPublisher EventPublisher { get; set; }
        public Func<ICommandInterceptor, ICommandInterceptor> NextResolver { get; internal set; }
    }
}
