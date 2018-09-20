using System;

namespace Lykke.Cqrs
{
    public interface IProcess : IDisposable
    {
        void Start(ICommandSender commandSender, IEventPublisher eventPublisher);
    }
}
