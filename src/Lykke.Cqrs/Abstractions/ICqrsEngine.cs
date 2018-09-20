namespace Lykke.Cqrs
{
    public interface ICqrsEngine
    {
        void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0);
    }
}