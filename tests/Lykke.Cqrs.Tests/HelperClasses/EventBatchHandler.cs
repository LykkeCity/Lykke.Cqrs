using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal class EventBatchHandler
    {
        private readonly bool _shouldThrow;

        internal readonly List<object> HandledEvents = new List<object>();

        internal EventBatchHandler(bool shouldThrow)
        {
            _shouldThrow = shouldThrow;
        }

        [UsedImplicitly]
        internal CommandHandlingResult[] Handle(string[] e)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            return e
                .Select(i =>
                {
                    HandledEvents.Add(i);
                    return CommandHandlingResult.Ok();
                })
                .ToArray();
        }
    }
}