using JetBrains.Annotations;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interace for cqrs engine.
    /// </summary>
    [PublicAPI]
    public interface ICqrsEngine
    {
        /// <summary>
        /// Sends command via messaging engine from source context to target context.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="command">Command instance.</param>
        /// <param name="boundedContext">Source context.</param>
        /// <param name="remoteBoundedContext">Target context.</param>
        /// <param name="priority">Command priority.</param>
        void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0);

        /// <summary>
        /// Publishes event from provided source context.
        /// </summary>
        /// <param name="event">Cqrs event.</param>
        /// <param name="boundedContext">Source context.</param>
        void PublishEvent(object @event, string boundedContext);

        /// <summary>
        /// Starts cqrs engine subscribers.
        /// </summary>
        void StartSubscribers();

        /// <summary>
        /// Starts cqrs engine publishers.
        /// </summary>
        void StartPublishers();

        /// <summary>
        /// Starts cqrs engine processes.
        /// </summary>
        void StartProcesses();

        /// <summary>
        /// Starts full cqrs engine - same as StartSubscribers, StartPublishers and StartProcesses called together.
        /// </summary>
        void StartAll();
    }
}