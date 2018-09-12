using System;
using JetBrains.Annotations;
using Lykke.Cqrs.Configuration.Routing;

namespace Lykke.Cqrs.Configuration.BoundedContext
{
    /// <summary>
    /// Fluent API interface for bounded context registration.
    /// </summary>
    [PublicAPI]
    public interface IBoundedContextRegistration : IRegistration, IHideObjectMembers
    {
        /// <summary>Bounded context name.</summary>
        string Name { get; }

        /// <summary>Event store existence flag.</summary>
        bool HasEventStore { get; }

        /// <summary>
        /// Specifies delay for failed command retry in milliseconds.
        /// </summary>
        /// <param name="delay">Retry delay value in milliseconds.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration FailedCommandRetryDelay(long delay);

        /// <summary>
        /// Specifies delay for failed command retry.
        /// </summary>
        /// <param name="delay">Retry delay value.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration FailedCommandRetryDelay(TimeSpan delay);

        /// <summary>
        /// Specififes command types for listening by current bounded context.
        /// </summary>
        /// <param name="commandTypes">Command types collection.</param>
        /// <returns>Fluent API interface for route name specification.</returns>
        IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(params Type[] commandTypes);

        /// <summary>
        /// Specifies command types for publishing from current bounded context.
        /// </summary>
        /// <param name="commandTypes">Command types collection.</param>
        /// <returns>Fluent API interface for target context specification.</returns>
        IPublishingCommandsDescriptor<IBoundedContextRegistration> PublishingCommands(params Type[] commandTypes);

        /// <summary>
        /// Specififes event types for listening by current bounded context.
        /// </summary>
        /// <param name="eventTypes">Event types collection.</param>
        /// <returns>Fluent API interface for source context specification.</returns>
        IListeningEventsDescriptor<IBoundedContextRegistration> ListeningEvents(params Type[] eventTypes);

        /// <summary>
        /// Specifies event types for publishing from current bounded context.
        /// </summary>
        /// <param name="eventTypes">Event types collection.</param>
        /// <returns>Fluent API interface for route name specification.</returns>
        IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(params Type[] eventTypes);

        /// <summary>
        /// Specifies route name for bounded context processing options.
        /// </summary>
        /// <param name="route">Route name.</param>
        /// <returns>Fluent API descriptor for processing group properties specification.</returns>
        ProcessingOptionsDescriptor<IBoundedContextRegistration> ProcessingOptions(string route);

        /// <summary>
        /// Adds commands handler to bounded context registration.
        /// </summary>
        /// <param name="handler">Commands handler instance.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithCommandsHandler(object handler);

        /// <summary>
        /// Adds commands handler to bounded context registration.
        /// </summary>
        /// <typeparam name="T">Commands handler type.</typeparam>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithCommandsHandler<T>();

        /// <summary>
        /// Adds commands handler to bounded context registration.
        /// </summary>
        /// <param name="handler">Commands handler type.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithCommandsHandler(Type handler);

        /// <summary>
        /// Adds commands handlers to bounded context registration.
        /// </summary>
        /// <param name="handlers">Commands handles ypes colleciton.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithCommandsHandlers(params Type[] handlers);

        /// <summary>
        /// Adds projection to bounded context registration.
        /// </summary>
        /// <typeparam name="TProjection">Projection type.</typeparam>
        /// <typeparam name="TBatchContext">Batch context type.</typeparam>
        /// <param name="projection">Projection instance.</param>
        /// <param name="fromBoundContext">Source context name.</param>
        /// <param name="batchSize">Batch max size.</param>
        /// <param name="applyTimeoutInSeconds">Batch timeout.</param>
        /// <param name="beforeBatchApply">Batch pre-processing handler.</param>
        /// <param name="afterBatchApply">Batch post-processing handler.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(
            TProjection projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Func<TProjection, TBatchContext> beforeBatchApply = null,
            Action<TProjection, TBatchContext> afterBatchApply = null);

        /// <summary>
        /// Adds projection to bounded context registration.
        /// </summary>
        /// <param name="projection">Projection type.</param>
        /// <param name="fromBoundContext">Source context name.</param>
        /// <param name="batchSize">Batch max size.</param>
        /// <param name="applyTimeoutInSeconds">Batch timeout.</param>
        /// <param name="batchContextType">Batch context type.</param>
        /// <param name="beforeBatchApply">Batch pre-processing handler.</param>
        /// <param name="afterBatchApply">Batch post-processing handler.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithProjection(
            Type projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Type batchContextType = null,
            Func<object, object> beforeBatchApply = null,
            Action<object, object> afterBatchApply = null);

        /// <summary>
        /// Adds projection to bounded context registration.
        /// </summary>
        /// <param name="projection">Projection instance.</param>
        /// <param name="fromBoundContext">Source context name.</param>
        /// <param name="batchSize">Batch max size.</param>
        /// <param name="applyTimeoutInSeconds">Batch timeout.</param>
        /// <param name="batchContextType">Batch context type.</param>
        /// <param name="beforeBatchApply">Batch pre-processing handler.</param>
        /// <param name="afterBatchApply">Batch post-processing handler.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithProjection(
            object projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Type batchContextType = null,
            Func<object, object> beforeBatchApply = null,
            Action<object, object> afterBatchApply = null);

        /// <summary>
        /// Adds projection to bounded context registration.
        /// </summary>
        /// <typeparam name="TProjection">Projection type.</typeparam>
        /// <typeparam name="TBatchContext">Batch context type.</typeparam>
        /// <param name="fromBoundContext">Source context name.</param>
        /// <param name="batchSize">Batch max size.</param>
        /// <param name="applyTimeoutInSeconds">Batch timeout.</param>
        /// <param name="beforeBatchApply">Batch pre-processing handler.</param>
        /// <param name="afterBatchApply">Batch post-processing handler.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Func<TProjection, TBatchContext> beforeBatchApply = null,
            Action<TProjection, TBatchContext> afterBatchApply = null);

        /// <summary>
        /// Adds process to bounded context registration.
        /// </summary>
        /// <param name="process">Process instance.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithProcess(object process);

        /// <summary>
        /// Adds process to bounded context registration.
        /// </summary>
        /// <param name="process">Process type.</param>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithProcess(Type process);

        /// <summary>
        /// Adds process to bounded context registration.
        /// </summary>
        /// <typeparam name="TProcess">Process type.</typeparam>
        /// <returns>Same fluent API interface.</returns>
        IBoundedContextRegistration WithProcess<TProcess>() where TProcess : IProcess;
    }
}