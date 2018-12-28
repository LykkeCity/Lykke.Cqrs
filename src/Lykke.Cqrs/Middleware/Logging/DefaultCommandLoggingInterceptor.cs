using System;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default command logging interceptor.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultCommandLoggingInterceptor : ICommandInterceptor
    {
        private readonly ICommandLogger _commandLogger;

        /// <summary>C-tor for old logging.</summary>
        [Obsolete]
        public DefaultCommandLoggingInterceptor(ILog log)
            : this(new DefaultCommandLogger(log))
        {
        }

        /// <summary>C-tor.</summary>
        public DefaultCommandLoggingInterceptor(ILogFactory logFactory)
            : this(new DefaultCommandLogger(logFactory))
        {
        }

        /// <summary>C-tor.</summary>
        public DefaultCommandLoggingInterceptor(ICommandLogger commandLogger)
        {
            _commandLogger = commandLogger;
        }

        /// <inheritdoc cref="ICommandInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            _commandLogger.Log(context.HandlerObject, context.Command);

            return context.InvokeNextAsync();
        }
    }
}
