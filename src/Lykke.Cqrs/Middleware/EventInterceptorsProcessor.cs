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
                NextResolver = i => ResolveNext(i, actualHandlerInterceptor),
            };

            return interceptor.InterceptAsync(context);
        }

        private IEventInterceptor ResolveNext(IEventInterceptor current, IEventInterceptor actualHandlerInterceptor)
        {
            int currentIndex = _eventInterceptors.IndexOf(current);
            if (currentIndex == -1)
                throw new InvalidOperationException($"Event interceptor of type {current.GetType().Name} is missing in Cqrs engine registrations.");

            if (currentIndex == _eventInterceptors.Count - 1)
                return actualHandlerInterceptor;

            return _eventInterceptors[currentIndex + 1];
        }
    }
}
