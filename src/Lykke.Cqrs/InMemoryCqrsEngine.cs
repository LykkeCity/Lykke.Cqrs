using System.Collections.Generic;
using System.Linq;
using Common.Log;
using Lykke.Messaging;
using Lykke.Cqrs.Configuration;

namespace Lykke.Cqrs
{
    public class InMemoryCqrsEngine : CqrsEngine
    {
        public InMemoryCqrsEngine(params IRegistration[] registrations) :
            base(
                new LogToConsole(),
                new MessagingEngine(
                    new LogToConsole(),
                    new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new IRegistration[]{Register.DefaultEndpointResolver(new InMemoryEndpointResolver())}.Concat(registrations).ToArray()
            )
        {
        }
        public InMemoryCqrsEngine(IDependencyResolver dependencyResolver, params IRegistration[] registrations) :
            base(
                new LogToConsole(),
                dependencyResolver,
                new MessagingEngine(
                    new LogToConsole(),
                    new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new DefaultEndpointProvider(),
                new  IRegistration[]{Register.DefaultEndpointResolver(new InMemoryEndpointResolver())}.Concat(registrations).ToArray()
            )
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                MessagingEngine.Dispose();
            }
        }
    }
}
