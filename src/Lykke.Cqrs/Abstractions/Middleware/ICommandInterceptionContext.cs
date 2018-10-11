using System;
using JetBrains.Annotations;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    [PublicAPI]
    public interface ICommandInterceptionContext
    {
        object Command { get; set; }

        object HandlerObject { get; }

        IEventPublisher EventPublisher { get; set; }

        Func<ICommandInterceptor, ICommandInterceptor> NextResolver { get; }
    }
}