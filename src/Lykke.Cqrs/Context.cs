using System;
using System.Collections.Generic;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    public class Context : RouteMap, IDisposable, ICommandSender
    {
        private readonly CqrsEngine _cqrsEngine;
        private readonly Dictionary<string, Destination> _tempDestinations = new Dictionary<string, Destination>();

        internal CommandDispatcher CommandDispatcher { get; }
        internal EventDispatcher EventDispatcher { get; }
        internal List<IProcess> Processes { get; }
        internal long FailedCommandRetryDelay { get; }

        public EventsPublisher EventsPublisher { get; }
        public IRouteMap Routes => this;

        internal Context(CqrsEngine cqrsEngine, string name, long failedCommandRetryDelay)
            : base(name)
        {
            if (name.ToLower() == "default")
                throw new ArgumentException("default is reserved name", nameof(name));
            _cqrsEngine = cqrsEngine;
            FailedCommandRetryDelay = failedCommandRetryDelay;
            EventsPublisher = new EventsPublisher(cqrsEngine, this);
            CommandDispatcher = cqrsEngine.CreateCommandsDispatcher(Name, failedCommandRetryDelay);
            EventDispatcher = cqrsEngine.CreateEventsDispatcher(Name);

            Processes = new List<IProcess>();
        }

        public void SendCommand<T>(T command, string remoteBoundedContext, uint priority = 0)
        {
            _cqrsEngine.SendCommand(command, Name, remoteBoundedContext, priority);
        }

        public void Dispose()
        {
            CommandDispatcher.Dispose();
            EventDispatcher.Dispose();
        }

        internal bool GetTempDestination(string transportId, Func<Destination> generate, out Destination destination)
        {
            lock (_tempDestinations)
            {
                if (!_tempDestinations.TryGetValue(transportId, out destination))
                {
                    destination = generate();
                    _tempDestinations[transportId] = destination;
                    return true;
                }
            }
            return false;
        }
    }
}