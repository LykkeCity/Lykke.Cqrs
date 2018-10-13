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
                InterceptorsProcessor = this,
                ActualHandlerInterceptor = actualHandlerInterceptor,
            };
            context.Next = new CommandInterceptorInvokationDecorator(context, 0);

            return interceptor.InterceptAsync(context);
        }

        internal ICommandInterceptor ResolveNext(int currentInterceptorIndex, ICommandInterceptor actualHandlerInterceptor)
        {
            if (currentInterceptorIndex < 0)
                throw new IndexOutOfRangeException($"{nameof(currentInterceptorIndex)} must be non-negative");

            if (currentInterceptorIndex >= _commandInterceptors.Count)
                return null;

            if (currentInterceptorIndex == _commandInterceptors.Count - 1)
                return actualHandlerInterceptor;

            return _commandInterceptors[currentInterceptorIndex + 1];
        }
    }
}
