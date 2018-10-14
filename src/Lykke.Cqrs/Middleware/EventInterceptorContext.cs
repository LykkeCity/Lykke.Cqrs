using System.Threading.Tasks;

namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptorContext : IEventInterceptionContext
    {
        private readonly EventInterceptionContext _context;
        private readonly int _interceptorIndex;
        private readonly EventInterceptorsProcessor _interceptorsProcessor;
        private readonly EventActualHandlerInterceptor _actualHandlerInterceptor;

        public object Event
        {
            get => _context.Event;
            set => _context.Event = value;
        }

        public object HandlerObject => _context.HandlerObject;

        public ICommandSender CommandSender
        {
            get => _context.CommandSender;
            set => _context.CommandSender = value;
        }

        internal EventInterceptorContext(
            EventInterceptionContext context,
            int interceptorIndex,
            EventInterceptorsProcessor interceptorsProcessor,
            EventActualHandlerInterceptor actualHandlerInterceptor)
        {
            _context = context;
            _interceptorIndex = interceptorIndex;
            _interceptorsProcessor = interceptorsProcessor;
            _actualHandlerInterceptor = actualHandlerInterceptor;
        }

        public Task<CommandHandlingResult> InvokeNext()
        {
            var next = _interceptorsProcessor.ResolveNext(_interceptorIndex, _actualHandlerInterceptor);
            if (next == null)
                return Task.FromResult(CommandHandlingResult.Ok());

            var contextForNext = new EventInterceptorContext(
                _context,
                _interceptorIndex + 1,
                _interceptorsProcessor,
                _actualHandlerInterceptor);
            return next.InterceptAsync(contextForNext);
        }
    }
}
