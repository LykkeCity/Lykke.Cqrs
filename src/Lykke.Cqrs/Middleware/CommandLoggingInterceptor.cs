using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    public delegate void CommandLoggingDelegate(ILog log, object handlerObject, object command);

    /// <summary>
    /// Logging interceptor for commands.
    /// </summary>
    public sealed class CommandLoggingInterceptor : ICommandInterceptor
    {
        private readonly ILog _log;
        private readonly Dictionary<Type, CommandLoggingDelegate> _customLoggingActionsMap;

        /// <summary>
        /// C-tor for old logging.
        /// </summary>
        [Obsolete]
        public CommandLoggingInterceptor(ILog log, Dictionary<Type, CommandLoggingDelegate> customLoggingActionsMap = null)
        {
            _log = log;
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        /// <summary>
        /// C-tor.
        /// </summary>
        public CommandLoggingInterceptor(ILogFactory logFactory, Dictionary<Type, CommandLoggingDelegate> customLoggingActionsMap = null)
        {
            _log = logFactory.CreateLog(this);
            _customLoggingActionsMap = customLoggingActionsMap;
        }

        /// <inheritdoc cref="ICommandInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            var commandType = context.Command.GetType();
            CommandLoggingDelegate loggingAction = null;
            if (_customLoggingActionsMap?.TryGetValue(commandType, out loggingAction) ?? false)
                loggingAction?.Invoke(_log, context.HandlerObject, context.Command);
            else
                _log.WriteInfo(context.HandlerObject.GetType().Name, context.Command, commandType.Name);

            return context.InvokeNextAsync();
        }
    }
}
