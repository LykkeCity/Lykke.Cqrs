using System.Collections.Generic;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal class ResultEventHandler
    {
        private readonly bool _fail;
        private readonly long _retryDelay;

        internal readonly List<object> HandledEvents = new List<object>();

        internal ResultEventHandler(bool fail = false, long retryDelay = 600)
        {
            _fail = fail;
            _retryDelay = retryDelay;
        }

        [UsedImplicitly]
        internal CommandHandlingResult Handle(string e)
        {
            HandledEvents.Add(e);
            return new CommandHandlingResult { Retry = _fail, RetryDelay = _retryDelay };
        }
    }
}