using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Inerface for event logging.
    /// </summary>
    [PublicAPI]
    public interface IEventLogger
    {
        /// <summary>
        /// Logs event.
        /// </summary>
        /// <param name="handler">Event handler instance.</param>
        /// <param name="event">Event object.</param>
        /// <returns>Task for <see cref="CommandHandlingResult"/></returns>
        Task<CommandHandlingResult> Log(object handler, object @event);
    }
}
