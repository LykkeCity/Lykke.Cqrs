using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Cqrs.Middleware;

namespace Lykke.Cqrs.Abstractions.Middleware
{
    /// <summary>
    /// Interface for cqrs events processing middleware.
    /// </summary>
    [PublicAPI]
    public interface IEventInterceptor
    {
        /// <summary>
        /// Event processing call to middleware with provided inited context.
        /// </summary>
        /// <param name="context">Middleware processing context - <see cref="IEventInterceptionContext"/></param>
        /// <returns>Task with event processing result - <see cref="CommandHandlingResult"/></returns>
        Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context);
    }
}
