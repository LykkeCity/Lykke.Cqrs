using System;
using Lykke.Cqrs.Configuration.Routing;

namespace Lykke.Cqrs.Configuration
{
    public abstract class ContextRegistrationBase<TRegistration> : RegistrationBase<TRegistration, Context> where TRegistration : class, IRegistration
    {
        public string Name { get; private set; }

        protected ContextRegistrationBase(string name)
        {
            Name = name;
        }

        protected sealed override Context GetSubject(CqrsEngine cqrsEngine)
        {
            var context = CreateContext(cqrsEngine);
            cqrsEngine.Contexts.Add(context);
            return context;
        }

        protected abstract Context CreateContext(CqrsEngine cqrsEngine);

        public IListeningEventsDescriptor<TRegistration> ListeningEvents(params Type[] types)
        {
            return AddDescriptor(new ListeningEventsDescriptor<TRegistration>(this as TRegistration, types));
        }

        public IPublishingCommandsDescriptor<TRegistration> PublishingCommands(params Type[] commandTypes)
        {
            return AddDescriptor(new PublishingCommandsDescriptor<TRegistration>(this as TRegistration, commandTypes));
        }

        public ProcessingOptionsDescriptor<TRegistration> ProcessingOptions(string route)
        {
            return AddDescriptor(new ProcessingOptionsDescriptor<TRegistration>(this as TRegistration, route));
        }
    }
}