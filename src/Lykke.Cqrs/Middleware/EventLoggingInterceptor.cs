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
    public sealed class EventLoggingInterceptor : IEventInterceptor
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
            var eventType = context.Event.GetType();
            EventLoggingDelegate loggingAction = null;
            if (_customLoggingActionsMap?.TryGetValue(eventType, out loggingAction) ?? false)
                loggingAction?.Invoke(_log, context.HandlerObject, context.Event);
            else
                _log.WriteInfo(context.HandlerObject.GetType().Name, context.Event, eventType.Name);

            return Task.FromResult(CommandHandlingResult.Ok());
        }
    }
}
