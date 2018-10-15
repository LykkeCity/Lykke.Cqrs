using System.Threading.Tasks;

namespace Lykke.Cqrs.Middleware
{
    internal class EventInterceptionContext : IEventInterceptionContext
    {
        private readonly EventInterceptionCommonContext _context;
        private readonly int _interceptorIndex;
        private readonly EventInterceptorsQueue _interceptorsProcessor;
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

        internal EventInterceptionContext(
            EventInterceptionCommonContext context,
            int interceptorIndex,
            EventInterceptorsQueue interceptorsProcessor,
            EventActualHandlerInterceptor actualHandlerInterceptor)
        {
            _context = context;
            _interceptorIndex = interceptorIndex;
            _interceptorsProcessor = interceptorsProcessor;
            _actualHandlerInterceptor = actualHandlerInterceptor;
        }

        public Task<CommandHandlingResult> InvokeNextAsync()
        {
            var next = _interceptorsProcessor.TryResolveNext(_interceptorIndex)
                ?? _actualHandlerInterceptor;

            var contextForNext = new EventInterceptionContext(
                _context,
                _interceptorIndex + 1,
                _interceptorsProcessor,
                _actualHandlerInterceptor);
            return next.InterceptAsync(contextForNext);
        }
    }
}
