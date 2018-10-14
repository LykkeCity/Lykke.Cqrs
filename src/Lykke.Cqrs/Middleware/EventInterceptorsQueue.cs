using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptorsQueue
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

            var commonContext = new EventInterceptionCommonContext
            {
                Event = @event,
                HandlerObject = handlerObject,
                CommandSender = commandSender,
            };
            var interceptorContext = new EventInterceptionContext(
                commonContext,
                0,
                this,
                actualHandlerInterceptor);

            return interceptor.InterceptAsync(interceptorContext);
        }

        internal IEventInterceptor TryResolveNext(int currentInterceptorIndex)
        {
            if (currentInterceptorIndex < 0)
                throw new IndexOutOfRangeException($"{nameof(currentInterceptorIndex)} must be non-negative");

            if (currentInterceptorIndex >= _eventInterceptors.Count - 1)
                return null;

            return _eventInterceptors[currentInterceptorIndex + 1];
        }
    }
}
