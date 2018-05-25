using System;
using System.Collections.Generic;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    public class Context : RouteMap, IDisposable, ICommandSender
    {
        private readonly CqrsEngine m_CqrsEngine;
        private readonly Dictionary<string, Destination> m_TempDestinations = new Dictionary<string, Destination>();

        internal CommandDispatcher CommandDispatcher { get; private set; }
        internal EventDispatcher EventDispatcher { get; private set; }
        internal List<IProcess> Processes { get; private set; }
        internal long FailedCommandRetryDelay { get; set; }

        public EventsPublisher EventsPublisher { get; private set; }
        public IRouteMap Routes { get { return this; } }

        internal Context(CqrsEngine cqrsEngine, string name, long failedCommandRetryDelay)
            : base(name)
        {
            if (name.ToLower() == "default")
                throw new ArgumentException("default is reserved name", "name");
            m_CqrsEngine = cqrsEngine;
            FailedCommandRetryDelay = failedCommandRetryDelay;
            EventsPublisher = new EventsPublisher(cqrsEngine, this);
            CommandDispatcher = new CommandDispatcher(
                cqrsEngine.Log,
                Name,
                cqrsEngine.EnableInputMessagesLogging,
                failedCommandRetryDelay);
            EventDispatcher = new EventDispatcher(
                cqrsEngine.Log,
                Name,
                cqrsEngine.EnableInputMessagesLogging);
            Processes = new List<IProcess>();
        }

        public void SendCommand<T>(T command, string remoteBoundedContext, uint priority = 0)
        {
            m_CqrsEngine.SendCommand(command, Name, remoteBoundedContext, priority);
        }

        public void Dispose()
        {
            CommandDispatcher.Dispose();
            EventDispatcher.Dispose();
        }

        internal bool GetTempDestination(string transportId, Func<Destination> generate, out Destination destination)
        {
            lock (m_TempDestinations)
            {
                if (!m_TempDestinations.TryGetValue(transportId, out destination))
                {
                    destination = generate();
                    m_TempDestinations[transportId] = destination;
                    return true;
                }
            }
            return false;
        }
    }
}