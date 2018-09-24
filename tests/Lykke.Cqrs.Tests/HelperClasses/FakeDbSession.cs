using System.Collections.Generic;

namespace Lykke.Cqrs.Tests
{
    internal class FakeDbSession
    {
        public bool Commited { get; set; }
        public List<string> Events { get; set; }

        public FakeDbSession()
        {
            Events=new List<string>();
        }

        public void Commit()
        {
            Commited = true;
        }

        public void ApplyEvent(string @event)
        {
            Events.Add(@event);
        }
    }
}