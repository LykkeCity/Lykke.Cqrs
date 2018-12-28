using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware.Logging
{
    public delegate void CommandLoggingDelegate(ICommandLogger defaultLogger, object handlerObject, object command);

    /// <summary>
    /// Command interceptor for custom logging.
    /// </summary>
    [PublicAPI]
    public sealed class CustomCommandLoggingInterceptor : ICommandInterceptor
    {
        private readonly ICommandLogger _defaultLogger;
        private readonly Dictionary<Type, CommandLoggingDelegate> _customLoggingActionsMap;

        /// <summary>C-tor for old logging.</summary>
        /// <param name="log">ILog implementation.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        [Obsolete]
        public CustomCommandLoggingInterceptor(ILog log, Dictionary<Type, CommandLoggingDelegate> customLoggingActionsMap)
            : this(new DefaultCommandLogger(log), customLoggingActionsMap)
        {
        }

        /// <summary>C-tor.</summary>
        /// <param name="logFactory">ILogFactory implementation.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        public CustomCommandLoggingInterceptor(ILogFactory logFactory, Dictionary<Type, CommandLoggingDelegate> customLoggingActionsMap)
            : this(new DefaultCommandLogger(logFactory), customLoggingActionsMap)
        {
        }

        /// <summary>C-tor.</summary>
        /// <param name="defaultLogger">Command logger for default logging.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        public CustomCommandLoggingInterceptor(ICommandLogger defaultLogger, Dictionary<Type, CommandLoggingDelegate> customLoggingActionsMap)
        {
            _defaultLogger = defaultLogger;
            _customLoggingActionsMap = customLoggingActionsMap ?? throw new ArgumentNullException(nameof(customLoggingActionsMap));
        }

        /// <inheritdoc cref="ICommandInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            if (_customLoggingActionsMap.TryGetValue(context.Command.GetType(), out var customLoggingAction))
                customLoggingAction?.Invoke(_defaultLogger, context.HandlerObject, context.Command);
            else
                _defaultLogger.Log(context.HandlerObject, context.Command);

            return context.InvokeNextAsync();
        }
    }
}
