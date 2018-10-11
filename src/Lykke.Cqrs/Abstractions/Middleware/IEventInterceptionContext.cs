using System;
using JetBrains.Annotations;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    [PublicAPI]
    public interface IEventInterceptionContext
    {
        object Event { get; set; }

        object HandlerObject { get; }

        ICommandSender CommandSender { get; set; }

        Func<IEventInterceptor, IEventInterceptor> NextResolver { get; }
    }
}