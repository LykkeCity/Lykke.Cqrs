using System.Threading.Tasks;
using JetBrains.Annotations;

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

        /// <summary>Invokes next middleware.</summary>
        Task<CommandHandlingResult> InvokeNextAsync();
    }
}