using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal class CustomCommandsHandler
    {
        [UsedImplicitly]
        internal void Handle(CreateCashOutCommand command, IEventPublisher eventPublisher)
        {
            eventPublisher.PublishEvent(new CashOutCreatedEvent());
        }
    }
}
