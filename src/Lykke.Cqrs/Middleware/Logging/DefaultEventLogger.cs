using System;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default event logger.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultEventLogger: IEventLogger
    {
        private readonly ILog _log;

        /// <summary>C-tor for old logging.</summary>
        [Obsolete]
        public DefaultEventLogger(ILog log)
        {
            _log = log;
        }

        /// <summary>C-tor.</summary>
        public DefaultEventLogger(ILogFactory logFactory)
        {
            _log = logFactory.CreateLog(this);
        }

        /// <inheritdoc cref="IEventLogger"/>
        public void Log(object handler, object @event)
        {
            _log.WriteInfo(handler.GetType().Name, @event, @event.GetType().Name);
        }
    }
}
