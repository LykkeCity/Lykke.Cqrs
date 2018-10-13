using JetBrains.Annotations;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    /// <summary>
    /// Context for command processing middleware.
    /// </summary>
    [PublicAPI]
    public interface ICommandInterceptionContext
    {
        /// <summary>Command to be processed.</summary>
        object Command { get; set; }

        /// <summary>Command handler object.</summary>
        object HandlerObject { get; }

        /// <summary><see cref="IEventPublisher"/> implementation.</summary>
        IEventPublisher EventPublisher { get; set; }

        /// <summary>Next middleware.</summary>
        ICommandInterceptor Next { get; }
    }
}