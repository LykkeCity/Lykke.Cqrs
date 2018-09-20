using System;
using System.Collections.Generic;

namespace Lykke.Cqrs.Tests
{
    internal class EventListenerWithBatchSupport  
    {
        public readonly List<FakeDbSession> Sessions = new List<FakeDbSession>();
        public readonly List<string> Events = new List<string>();

        void Handle(string m, FakeDbSession session)
        {
            Events.Add(m);
            if (session != null)
                session.ApplyEvent(m);
            Console.WriteLine(m);
        }

        public FakeDbSession CreateDbSession()
        {
            var session = new FakeDbSession();
            Sessions.Add(session);
            return session;
        }
    }
}