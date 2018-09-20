using System;
using System.Collections.Generic;

namespace Lykke.Cqrs.Tests
{
    internal class EventListener
    {
        public readonly List<Tuple<string, string>> EventsWithBoundedContext = new List<Tuple<string, string>>();
        public readonly List<string> Events = new List<string>();

        void Handle(string m, string boundedContext)
        {
            EventsWithBoundedContext.Add(Tuple.Create(m, boundedContext));
            Console.WriteLine(boundedContext + ":" + m);
        }
    }
}