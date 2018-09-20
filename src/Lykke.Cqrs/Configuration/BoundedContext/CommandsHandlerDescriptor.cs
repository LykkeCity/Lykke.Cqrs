using System;

namespace Lykke.Cqrs.Configuration.BoundedContext
{
    internal class CommandsHandlerDescriptor : DescriptorWithDependencies<Context>
    {
        public CommandsHandlerDescriptor(params object[] handlers):base(handlers)
        {
        }

        public CommandsHandlerDescriptor(params Type[] handlers):base(handlers)
        {
        }

        public override void Process(Context context, CqrsEngine cqrsEngine)
        {
            foreach (var handler in ResolvedDependencies)
            {
                context.CommandDispatcher.Wire(handler, new OptionalParameter<IEventPublisher>(context.EventsPublisher));
            }
        }
    }
}