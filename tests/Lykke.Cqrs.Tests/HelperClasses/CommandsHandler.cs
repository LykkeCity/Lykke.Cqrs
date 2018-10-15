using System;
using System.Collections.Generic;
using System.Threading;

namespace Lykke.Cqrs.Tests
{
    internal class CommandsHandler
    {
        public readonly List<object> HandledCommands = new List<object>();
        private readonly bool _shouldThrow;
        private readonly int _processingTimeout;

        public CommandsHandler()
            : this(false, 0)
        {
        }

        public CommandsHandler(bool shouldThrow = false, int processingTimeout = 0)
        {
            _shouldThrow = shouldThrow;
            _processingTimeout = processingTimeout;
        }

        private void Handle(string command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            if (_processingTimeout > 0)
                Thread.Sleep(_processingTimeout);

            HandledCommands.Add(command);
        }

        private void Handle(int command, IEventPublisher eventPublisher)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            if (_processingTimeout > 0)
                Thread.Sleep(_processingTimeout);

            HandledCommands.Add(command);
        }

        private void Handle(DateTime command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            if (_processingTimeout > 0)
                Thread.Sleep(_processingTimeout);

            HandledCommands.Add(command);
        }
    }
}