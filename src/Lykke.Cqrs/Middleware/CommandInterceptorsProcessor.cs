using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class CommandInterceptorsProcessor
    {
        private readonly List<ICommandInterceptor> _commandInterceptors = new List<ICommandInterceptor>();

        internal void AddInterceptor(ICommandInterceptor commandInterceptor)
        {
            _commandInterceptors.Add(commandInterceptor);
        }

        internal Task<CommandHandlingResult> RunInterceptorsAsync(
            object command,
            object handlerObject,
            IEventPublisher eventPublisher,
            Func<object, IEventPublisher, CommandHandlingResult> actualHandler)
        {
            var actualHandlerInterceptor = new CommandActualHandlerInterceptor(actualHandler);
            var interceptor = _commandInterceptors.FirstOrDefault() ?? actualHandlerInterceptor;

            var context = new CommandInterceptionContext
            {
                Command = command,
                HandlerObject = handlerObject,
                EventPublisher = eventPublisher,
                NextResolver = i => ResolveNext(i, actualHandlerInterceptor),
            };

            return interceptor.InterceptAsync(context);
        }

        private ICommandInterceptor ResolveNext(ICommandInterceptor current, ICommandInterceptor actualHandlerInterceptor)
        {
            int currentIndex = _commandInterceptors.IndexOf(current);
            if (currentIndex == -1)
                throw new InvalidOperationException($"Command interceptor of type {current.GetType().Name} is missing in Cqrs engine registrations.");

            if (currentIndex == _commandInterceptors.Count - 1)
                return actualHandlerInterceptor;

            return _commandInterceptors[currentIndex + 1];
        }
    }
}
