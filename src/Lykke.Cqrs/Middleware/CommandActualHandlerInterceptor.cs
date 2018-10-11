using System;
using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class CommandActualHandlerInterceptor : ICommandInterceptor
    {
        private readonly Func<object, IEventPublisher, CommandHandlingResult> _actualHandler;

        internal CommandActualHandlerInterceptor(Func<object, IEventPublisher, CommandHandlingResult> actualHandler)
        {
            _actualHandler = actualHandler;
        }

        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            return Task.FromResult(_actualHandler.Invoke(context.Command, context.EventPublisher));
        }
    }
}
