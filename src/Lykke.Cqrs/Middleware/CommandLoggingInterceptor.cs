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
    public class CommandLoggingInterceptor : ICommandInterceptor
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
            CommandLoggingDelegate loggingAction = null;
            if (_customLoggingActionsMap?.TryGetValue(context.Command.GetType(), out loggingAction) ?? false)
                loggingAction?.Invoke(_log, context.HandlerObject, context.Command);
            else
                DefaultLog(_log, context.HandlerObject, context.Command);

            return Task.FromResult(CommandHandlingResult.Ok());
        }

        /// <summary>
        /// Default logging for commands.
        /// </summary>
        /// <param name="log">ILog instance.</param>
        /// <param name="handlerObject">Command handler instance.</param>
        /// <param name="command">Command.</param>
        /// <remarks>This could be used in case some custom logging interceptor is implemented for default cases.</remarks>
        public static void DefaultLog(ILog log, object handlerObject, object command)
        {
            log.WriteInfo(handlerObject.GetType().Name, command, command.GetType().Name);
        }
    }
}
