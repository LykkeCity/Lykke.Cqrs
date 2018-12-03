using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    public class EventLoggingInterceptor : IEventInterceptor
    {
        private readonly ILog _log;
        private readonly Dictionary<Type, Action<ILog, object, object>> _customLoggingActionsMap;

        [Obsolete]
        public EventLoggingInterceptor(ILog log, Dictionary<Type, Action<ILog, object, object>> customLoggingActionsMap = null)
        {
            _log = log;
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        public EventLoggingInterceptor(ILogFactory logFactory, Dictionary<Type, Action<ILog, object, object>> customLoggingActionsMap = null)
        {
            _log = logFactory.CreateLog(this);
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            var eventType = context.Event.GetType();
            Action<ILog, object, object> loggingAction = null;
            if (_customLoggingActionsMap?.TryGetValue(eventType, out loggingAction) ?? false)
                loggingAction?.Invoke(_log, context.HandlerObject, context.Event);
            else
                _log.WriteInfo(context.HandlerObject.GetType().Name, context.Event?.ToJson(), eventType.Name);

            return Task.FromResult(CommandHandlingResult.Ok());
        }
    }
}
