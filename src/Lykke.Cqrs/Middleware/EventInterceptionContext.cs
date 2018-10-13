﻿using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptionContext : IEventInterceptionContext
    {
        public object Event { get; set; }
        public object HandlerObject { get; internal set; }
        public ICommandSender CommandSender { get; set; }
        public IEventInterceptor Next { get; internal set; }

        internal EventInterceptorsProcessor InterceptorsProcessor { get; set; }
        internal EventActualHandlerInterceptor ActualHandlerInterceptor { get; set; }

        internal EventInterceptionContext CopyForNext()
        {
            return new EventInterceptionContext
            {
                Event = Event,
                HandlerObject = HandlerObject,
                CommandSender = CommandSender,
                InterceptorsProcessor = InterceptorsProcessor,
                ActualHandlerInterceptor = ActualHandlerInterceptor,
            };
        }
    }
}
