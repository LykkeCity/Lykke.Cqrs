using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptorInvokationDecorator : IEventInterceptor
    {
        private readonly EventInterceptionContext _interceptionContext;
        private readonly int _interceptorIndex;

        internal EventInterceptorInvokationDecorator(EventInterceptionContext interceptionContext, int interceptorIndex)
        {
            _interceptionContext = interceptionContext;
            _interceptorIndex = interceptorIndex;
        }

        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            var interceptionContext = context as EventInterceptionContext;
            var next = interceptionContext.InterceptorsProcessor.ResolveNext(_interceptorIndex, interceptionContext.ActualHandlerInterceptor);
            var contextForNext = interceptionContext.CopyForNext();
            contextForNext.Next = new EventInterceptorInvokationDecorator(contextForNext, _interceptorIndex + 1);
            return next.InterceptAsync(contextForNext);
        }
    }
}
