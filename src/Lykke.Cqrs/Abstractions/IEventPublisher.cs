using JetBrains.Annotations;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for event publishing from bounded context.
    /// </summary>
    [PublicAPI]
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes provided event via messaging engine from bounded context.
        /// </summary>
        /// <param name="event">Event instance.</param>
        void PublishEvent(object @event);
    }
}