using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal class FakeBatchContext
    {
    }

    internal class EventHandlerWithBatchSupport
    {
        public readonly List<Tuple<object, object>> HandledEvents = new List<Tuple<object, object>>();
        private int _failCount;

        internal EventHandlerWithBatchSupport(int failCount = 0)
        {
            _failCount = failCount;
        }

        [UsedImplicitly]
        internal CommandHandlingResult Handle(DateTime e, FakeBatchContext batchContext)
        {
            HandledEvents.Add(Tuple.Create<object, object>(e, batchContext));
            var retry = Interlocked.Decrement(ref _failCount) >= 0;
            return new CommandHandlingResult { Retry = retry, RetryDelay = 10 };
        }

        internal FakeBatchContext OnBatchStart()
        {
            BatchStartReported = true;
            LastCreatedBatchContext = new FakeBatchContext();
            return LastCreatedBatchContext;
        }

        internal FakeBatchContext LastCreatedBatchContext { get; set; }
        internal bool BatchStartReported { get; set; }
        internal bool BatchFinishReported { get; set; }

        internal void OnBatchFinish(FakeBatchContext context)
        {
            BatchFinishReported = true;
        }
    }
}