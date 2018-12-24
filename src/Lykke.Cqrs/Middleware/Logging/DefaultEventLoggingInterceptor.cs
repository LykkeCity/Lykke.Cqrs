using System;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default event logging interceptor.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultEventLoggingInterceptor : IEventInterceptor
    {
        private readonly IEventLogger _eventLogger;

        /// <summary>C-tor for old logging.</summary>
        [Obsolete]
        public DefaultEventLoggingInterceptor(ILog log)
            : this(new DefaultEventLogger(log))
        {
        }

        /// <summary>C-tor.</summary>
        public DefaultEventLoggingInterceptor(ILogFactory logFactory)
            : this(new DefaultEventLogger(logFactory))
        {
        }

        /// <summary>C-tor.</summary>
        public DefaultEventLoggingInterceptor(IEventLogger eventLogger)
        {
            _eventLogger = eventLogger;
        }

        /// <inheritdoc cref="IEventInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            _eventLogger.Log(context.HandlerObject, context.Event);

            return context.InvokeNextAsync();
        }
    }
}
