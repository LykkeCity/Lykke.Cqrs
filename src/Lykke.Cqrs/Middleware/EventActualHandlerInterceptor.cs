using System;
using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class EventActualHandlerInterceptor : IEventInterceptor
    {
        private readonly Func<object, ICommandSender, CommandHandlingResult> _actualHandler;

        internal EventActualHandlerInterceptor(Func<object, ICommandSender, CommandHandlingResult> actualHandler)
        {
            _actualHandler = actualHandler;
        }

        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            return Task.FromResult(_actualHandler.Invoke(context.Event, context.CommandSender));
        }
    }
}
