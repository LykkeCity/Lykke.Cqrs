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
        void Log(object handler, object @event);
    }
}
