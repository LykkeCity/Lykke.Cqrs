using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptorsProcessor
    {
        private readonly List<IEventInterceptor> _eventInterceptors = new List<IEventInterceptor>();

        internal void AddInterceptor(IEventInterceptor eventInterceptor)
        {
            _eventInterceptors.Add(eventInterceptor);
        }

        internal Task<CommandHandlingResult> RunInterceptorsAsync(
            object @event,
            object handlerObject,
            ICommandSender commandSender,
            Func<object, ICommandSender, CommandHandlingResult> actualHandler)
        {
            var actualHandlerInterceptor = new EventActualHandlerInterceptor(actualHandler);
            var interceptor = _eventInterceptors.FirstOrDefault() ?? actualHandlerInterceptor;

            var context = new EventInterceptionContext
            {
                Event = @event,
                HandlerObject = handlerObject,
                CommandSender = commandSender,
                InterceptorsProcessor = this,
                ActualHandlerInterceptor = actualHandlerInterceptor,
            };
            context.Next = new EventInterceptorInvokationDecorator(context, 0);

            return interceptor.InterceptAsync(context);
        }

        internal IEventInterceptor ResolveNext(int currentInterceptorIndex, IEventInterceptor actualHandlerInterceptor)
        {
            if (currentInterceptorIndex < 0)
                throw new IndexOutOfRangeException($"{nameof(currentInterceptorIndex)} must be non-negative");

            if (currentInterceptorIndex >= _eventInterceptors.Count)
                return null;

            if (currentInterceptorIndex == _eventInterceptors.Count - 1)
                return actualHandlerInterceptor;

            return _eventInterceptors[currentInterceptorIndex + 1];
        }
    }
}
