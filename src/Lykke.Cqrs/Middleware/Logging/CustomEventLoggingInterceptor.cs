﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware.Logging
{
    public delegate void EventLoggingDelegate(IEventLogger defaultLogger, object handlerObject, object @event);

    /// <summary>
    /// Event interceptor for custom logging.
    /// </summary>
    [PublicAPI]
    public sealed class CustomEventLoggingInterceptor : IEventInterceptor
    {
        private readonly IEventLogger _defaultLogger;
        private readonly Dictionary<Type, EventLoggingDelegate> _customLoggingActionsMap;

        /// <summary>C-tor for old logging.</summary>
        /// <param name="log">ILog implementation.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        [Obsolete]
        public CustomEventLoggingInterceptor(ILog log, Dictionary<Type, EventLoggingDelegate> customLoggingActionsMap)
            : this(new DefaultEventLogger(log), customLoggingActionsMap)
        {
        }

        /// <summary>C-tor.</summary>
        /// <param name="logFactory">ILogFactory implementation.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        public CustomEventLoggingInterceptor(ILogFactory logFactory, Dictionary<Type, EventLoggingDelegate> customLoggingActionsMap)
            : this(new DefaultEventLogger(logFactory), customLoggingActionsMap)
        {
        }

        /// <summary>C-tor.</summary>
        /// <param name="defaultLogger">Event logger for default logging.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        public CustomEventLoggingInterceptor(IEventLogger defaultLogger, Dictionary<Type, EventLoggingDelegate> customLoggingActionsMap)
        {
            _defaultLogger = defaultLogger;
            _customLoggingActionsMap = customLoggingActionsMap ?? throw new ArgumentNullException(nameof(customLoggingActionsMap));
        }

        /// <inheritdoc cref="IEventInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            if (_customLoggingActionsMap.TryGetValue(context.Event.GetType(), out var customLoggingAction))
                customLoggingAction?.Invoke(_defaultLogger, context.HandlerObject, context.Event);
            else
                _defaultLogger.Log(context.HandlerObject, context.Event);

            return context.InvokeNextAsync();
        }
    }
}
