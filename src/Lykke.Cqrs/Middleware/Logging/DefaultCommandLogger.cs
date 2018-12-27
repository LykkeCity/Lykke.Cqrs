using System;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default command logger.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultCommandLogger : ICommandLogger
    {
        private readonly ILog _log;

        /// <summary>C-tor for old logging.</summary>
        [Obsolete]
        public DefaultCommandLogger(ILog log)
        {
            _log = log;
        }

        /// <summary>C-tor.</summary>
        public DefaultCommandLogger(ILogFactory logFactory)
        {
            _log = logFactory.CreateLog(this);
        }

        /// <inheritdoc cref="ICommandLogger"/>
        public void Log(object handler, object command)
        {
            _log.WriteInfo(handler.GetType().Name, command, command.GetType().Name);
        }
    }
}
