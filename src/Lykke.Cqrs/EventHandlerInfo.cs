using System;

namespace Lykke.Cqrs
{
    internal class EventHandlerInfo
    {
        internal object HandlerObject { get; }
        internal string HandlerTypeName { get; }
        internal Func<object, ICommandSender, object, CommandHandlingResult> Handler { get; }
        internal ICommandSender CommandSender { get; }

        internal EventHandlerInfo(
            object handlerObject,
            ICommandSender commandSender,
            Func<object, ICommandSender, object, CommandHandlingResult> handler)
        {
            HandlerObject = handlerObject;
            HandlerTypeName = handlerObject.GetType().Name;
            Handler = handler;
            CommandSender = commandSender;
        }
    }
}
