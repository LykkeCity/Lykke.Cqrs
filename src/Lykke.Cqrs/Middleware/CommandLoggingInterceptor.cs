using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    public class CommandLoggingInterceptor : ICommandInterceptor
    {
        private readonly ILog _log;
        private readonly Dictionary<Type, Action<ILog, object, object>> _customLoggingActionsMap;

        [Obsolete]
        public CommandLoggingInterceptor(ILog log, Dictionary<Type, Action<ILog, object, object>> customLoggingActionsMap = null)
        {
            _log = log;
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        public CommandLoggingInterceptor(ILogFactory logFactory, Dictionary<Type, Action<ILog, object, object>> customLoggingActionsMap = null)
        {
            _log = logFactory.CreateLog(this);
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            var commandType = context.Command.GetType();
            Action<ILog, object, object> loggingAction = null;
            if (_customLoggingActionsMap?.TryGetValue(commandType, out loggingAction) ?? false)
                loggingAction?.Invoke(_log, context.HandlerObject, context.Command);
            else
                _log.WriteInfo(context.HandlerObject.GetType().Name, context.Command?.ToJson(), commandType.Name);

            return Task.FromResult(CommandHandlingResult.Ok());
        }
    }
}
