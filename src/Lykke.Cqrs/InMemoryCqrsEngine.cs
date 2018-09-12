using System.Collections.Generic;
using System.Linq;
using Lykke.Common.Log;
using Lykke.Messaging;
using Lykke.Cqrs.Configuration;

namespace Lykke.Cqrs
{
    public class InMemoryCqrsEngine : CqrsEngine
    {
        public InMemoryCqrsEngine(ILogFactory logFactory, params IRegistration[] registrations)
            : base(
                logFactory,
                new MessagingEngine(
                    logFactory,
                    new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new IRegistration[]{Register.DefaultEndpointResolver(new InMemoryEndpointResolver())}.Concat(registrations).ToArray()
            )
        {
        }
        public InMemoryCqrsEngine(
            ILogFactory logFactory,
            IDependencyResolver dependencyResolver,
            params IRegistration[] registrations)
            : base(
                logFactory,
                dependencyResolver,
                new MessagingEngine(
                    logFactory,
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
