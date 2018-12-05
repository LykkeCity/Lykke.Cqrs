using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    public delegate void EventLoggingDelegate(ILog log, object handlerObject, object @event);

    /// <summary>
    /// Logging interceptor for events.
    /// </summary>
    public class EventLoggingInterceptor : IEventInterceptor
    {
        private readonly ILog _log;
        private readonly Dictionary<Type, EventLoggingDelegate> _customLoggingActionsMap;

        /// <summary>
        /// C-tor for old logging.
        /// </summary>
        [Obsolete]
        public EventLoggingInterceptor(ILog log, Dictionary<Type, EventLoggingDelegate> customLoggingActionsMap = null)
        {
            _log = log;
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        /// <summary>
        /// C-tor
        /// </summary>
        public EventLoggingInterceptor(ILogFactory logFactory, Dictionary<Type, EventLoggingDelegate> customLoggingActionsMap = null)
        {
            _log = logFactory.CreateLog(this);
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        /// <inheritdoc cref="IEventInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            EventLoggingDelegate loggingAction = null;
            if (_customLoggingActionsMap?.TryGetValue(context.Event.GetType(), out loggingAction) ?? false)
                loggingAction?.Invoke(_log, context.HandlerObject, context.Event);
            else
                DefaultLog(_log, context.HandlerObject, context.Event);

            return Task.FromResult(CommandHandlingResult.Ok());
        }

        /// <summary>
        /// Default logging for events.
        /// </summary>
        /// <param name="log">ILog instance.</param>
        /// <param name="handlerObject">Event handler instance.</param>
        /// <param name="event">Event.</param>
        /// <remarks>This could be used in case some custom logging interceptor is implemented for default cases.</remarks>
        public static void DefaultLog(ILog log, object handlerObject, object @event)
        {
            log.WriteInfo(handlerObject.GetType().Name, @event, @event.GetType().Name);
        }
    }
}
