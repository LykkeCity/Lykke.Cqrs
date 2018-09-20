using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal class EventAsyncResultHandler
    {
        private readonly bool _shouldThrow;

        internal readonly List<object> HandledEvents = new List<object>();

        internal EventAsyncResultHandler(bool shouldThrow)
        {
            _shouldThrow = shouldThrow;
        }

        [UsedImplicitly]
        internal async Task<CommandHandlingResult> Handle(string evt)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            await Task.Delay(1);

            HandledEvents.Add(evt);

            return CommandHandlingResult.Ok();
        }
    }
}