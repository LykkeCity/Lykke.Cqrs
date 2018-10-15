using System;
using System.Collections.Generic;
using System.Threading;

namespace Lykke.Cqrs.Tests
{
    internal class TestSaga
    {
        internal static List<object> Messages = new List<object>();
        internal static ManualResetEvent Complete = new ManualResetEvent(false);

        private void Handle(CashOutCreatedEvent @event, ICommandSender sender, string boundedContext)
        {
            var message = string.Format("Event from {0} is caught by saga:{1}", boundedContext, @event);
            Messages.Add(message);

            Complete.Set();

            Console.WriteLine(message);
        }

        private void Handle(int @event)
        {
        }

        private void Handle(string @event, ICommandSender commandSender)
        {
            Messages.Add(@event);
        }
    }
}