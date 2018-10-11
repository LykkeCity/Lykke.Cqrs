using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Cqrs.Middleware;

namespace Lykke.Cqrs.Abstractions.Middleware
{
    /// <summary>
    /// Interface for cqrs commands processing middleware.
    /// </summary>
    [PublicAPI]
    public interface ICommandInterceptor
    {
        /// <summary>
        /// Command processing call to middleware with provided inited context.
        /// </summary>
        /// <param name="context">Middleware processing context - <see cref="ICommandInterceptionContext"/></param>
        /// <returns>Task with command processing result - <see cref="CommandHandlingResult"/></returns>
        Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context);
    }
}
