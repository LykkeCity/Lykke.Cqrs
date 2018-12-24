using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Inerface for command logging.
    /// </summary>
    [PublicAPI]
    public interface ICommandLogger
    {
        /// <summary>
        /// Logs command.
        /// </summary>
        /// <param name="handler">Command handler instance.</param>
        /// <param name="command">Command object.</param>
        /// <returns>Task for <see cref="CommandHandlingResult"/></returns>
        Task<CommandHandlingResult> Log(object handler, object command);
    }
}