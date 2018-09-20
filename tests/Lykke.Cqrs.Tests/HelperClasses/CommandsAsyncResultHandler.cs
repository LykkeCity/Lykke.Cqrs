using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal class CommandsAsyncResultHandler
    {
        private readonly bool _shouldThrow;

        internal readonly List<object> HandledCommands = new List<object>();

        internal CommandsAsyncResultHandler(bool shouldThrow)
        {
            _shouldThrow = shouldThrow;
        }

        [UsedImplicitly]
        internal async Task<CommandHandlingResult> Handle(string command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            HandledCommands.Add(command);

            await Task.Delay(1);

            return CommandHandlingResult.Ok();
        }
    }
}