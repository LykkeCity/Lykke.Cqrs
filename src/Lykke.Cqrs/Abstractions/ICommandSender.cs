using System;

namespace Lykke.Cqrs
{
    public interface ICommandSender : IDisposable
    {
        void SendCommand<T>(T command, string remoteBoundedContext, uint priority = 0);
    }
}