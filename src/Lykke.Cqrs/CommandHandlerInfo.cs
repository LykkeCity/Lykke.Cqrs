using System;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    internal class CommandHandlerInfo
    {
        internal object HandlerObject { get; }
        internal string HandlerTypeName { get; }
        internal Func<object, IEventPublisher, Endpoint, string, CommandHandlingResult> Handler { get; }
        internal IEventPublisher EventPublisher { get; }

        internal CommandHandlerInfo(
            object handlerObject,
            IEventPublisher eventPublisher,
            Func<object, IEventPublisher, Endpoint, string, CommandHandlingResult> handler)
        {
            HandlerObject = handlerObject;
            HandlerTypeName = handlerObject.GetType().Name;
            Handler = handler;
            EventPublisher = eventPublisher;
        }
    }
}
