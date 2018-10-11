using System;
using JetBrains.Annotations;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    /// <summary>
    /// Context for event processing middleware.
    /// </summary>
    [PublicAPI]
    public interface IEventInterceptionContext
    {
        /// <summary>Event to be processed.</summary>
        object Event { get; set; }

        /// <summary>Event handler object.</summary>
        object HandlerObject { get; }

        /// <summary><see cref="ICommandSender"/> implementation.</summary>
        ICommandSender CommandSender { get; set; }

        /// <summary>Next middleware resolver.</summary>
        Func<IEventInterceptor, IEventInterceptor> NextResolver { get; }
    }
}