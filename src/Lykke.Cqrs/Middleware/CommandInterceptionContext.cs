using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class CommandInterceptionContext : ICommandInterceptionContext
    {
        public object Command { get; set; }
        public object HandlerObject { get; internal set; }
        public IEventPublisher EventPublisher { get; set; }
        public ICommandInterceptor Next { get; internal set; }

        internal CommandInterceptorsProcessor InterceptorsProcessor { get; set; }
        internal CommandActualHandlerInterceptor ActualHandlerInterceptor { get; set; }

        internal CommandInterceptionContext CopyForNext()
        {
            return new CommandInterceptionContext
            {
                Command = Command,
                HandlerObject = HandlerObject,
                EventPublisher = EventPublisher,
                InterceptorsProcessor = InterceptorsProcessor,
                ActualHandlerInterceptor = ActualHandlerInterceptor,
            };
        }
    }
}
