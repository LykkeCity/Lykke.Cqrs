using System.Threading.Tasks;
using JetBrains.Annotations;

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

        /// <summary>Invokes next middleware.</summary>
        Task<CommandHandlingResult> InvokeNextAsync();
    }
}