using System;
using System.Collections.Generic;
using System.Linq;

namespace Lykke.Cqrs.Configuration.Routing
{
    public class ListeningCommandsDescriptor<TRegistration> : ListeningRouteDescriptor<ListeningCommandsDescriptor<TRegistration>, TRegistration>
        where TRegistration : IRegistration
    {
        public Type[] Types { get; private set; }

        public ListeningCommandsDescriptor(TRegistration registration, Type[] types)
            : base(registration)
        {
            Types = types;
            Descriptor = this;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public ListeningCommandsDescriptor<TRegistration> Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;

            return this;
        }

        public override void Create(Context context, IDependencyResolver resolver)
        {
            foreach (var type in Types)
            {
                if (LowestPriority > 0)
                {
                    for (uint priority = 1; priority <= LowestPriority; priority++)
                    {
                        ((IRouteMap)context)[Route].AddSubscribedCommand(type, priority, EndpointResolver);
                    }
                }
                else
                {
                    ((IRouteMap)context)[Route].AddSubscribedCommand(type, 0, EndpointResolver);
                }
            }
        }

        public override void Process(Context context, CqrsEngine cqrsEngine)
        {
            EndpointResolver.SetFallbackResolver(cqrsEngine.EndpointResolver);

            var notHandledCommandTypes = context.CommandDispatcher.GetUnhandledCommandTypes(Types);
            if (notHandledCommandTypes.Count > 0)
                throw new InvalidOperationException(
                    $"Command types ({string.Join(", ", notHandledCommandTypes.Select(t => t.Name))}) that are listened on route '{Route}' have no handler");
        }
    }
}