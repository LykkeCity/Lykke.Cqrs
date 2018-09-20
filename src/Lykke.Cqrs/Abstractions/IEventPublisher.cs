namespace Lykke.Cqrs
{
    public interface IEventPublisher
    {
        void PublishEvent(object @event);
    }
}