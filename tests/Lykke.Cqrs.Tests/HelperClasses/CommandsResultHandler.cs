using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Lykke.Cqrs.InfrastructureCommands;

namespace Lykke.Cqrs.Tests
{
    internal class CommandsResultHandler
    {
        private readonly bool _shouldFail;
        private readonly long _retryDelay;

        internal readonly List<object> HandledCommands = new List<object>();

        internal CommandsResultHandler(bool shouldFail = false, long retryDelay = 600)
        {
            _shouldFail = shouldFail;
            _retryDelay = retryDelay;
        }

        [UsedImplicitly]
        internal CommandHandlingResult Handle(string command)
        {
            HandledCommands.Add(command);

            return new CommandHandlingResult{ Retry = _shouldFail, RetryDelay = _retryDelay };
        }

        [UsedImplicitly]
        internal CommandHandlingResult Handle(int command)
        {
            HandledCommands.Add(command);

            return new CommandHandlingResult { Retry = _shouldFail, RetryDelay = _retryDelay };
        }

        [UsedImplicitly]
        internal CommandHandlingResult Handle(RoutedCommand<DateTime> command)
        {
            HandledCommands.Add(command);

            return new CommandHandlingResult { Retry = _shouldFail, RetryDelay = _retryDelay };
        }
    }
}