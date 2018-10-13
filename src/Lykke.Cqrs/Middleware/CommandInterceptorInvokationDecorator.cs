using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class CommandInterceptorInvokationDecorator : ICommandInterceptor
    {
        private readonly CommandInterceptionContext _interceptionContext;
        private readonly int _interceptorIndex;

        internal CommandInterceptorInvokationDecorator(CommandInterceptionContext interceptionContext, int interceptorIndex)
        {
            _interceptionContext = interceptionContext;
            _interceptorIndex = interceptorIndex;
        }

        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            var interceptionContext = context as CommandInterceptionContext;
            var next = interceptionContext.InterceptorsProcessor.ResolveNext(_interceptorIndex, interceptionContext.ActualHandlerInterceptor);
            var contextForNext = interceptionContext.CopyForNext();
            contextForNext.Next = new CommandInterceptorInvokationDecorator(contextForNext, _interceptorIndex + 1);
            return next.InterceptAsync(contextForNext);
        }
    }
}
