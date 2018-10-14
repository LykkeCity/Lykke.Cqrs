using System.Threading.Tasks;

namespace Lykke.Cqrs.Middleware
{
    internal class CommandInterceptorContext : ICommandInterceptionContext
    {
        private readonly CommandInterceptionContext _context;
        private readonly int _interceptorIndex;
        private readonly CommandInterceptorsQueue _interceptorsProcessor;
        private readonly CommandActualHandlerInterceptor _actualHandlerInterceptor;

        public object Command
        {
            get => _context.Command;
            set => _context.Command = value;
        }

        public object HandlerObject => _context.HandlerObject;

        public IEventPublisher EventPublisher
        {
            get => _context.EventPublisher;
            set => _context.EventPublisher = value;
        }

        internal CommandInterceptorContext(
            CommandInterceptionContext context,
            int interceptorIndex,
            CommandInterceptorsQueue interceptorsProcessor,
            CommandActualHandlerInterceptor actualHandlerInterceptor)
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

            var contextForNext = new CommandInterceptorContext(
                _context,
                _interceptorIndex + 1,
                _interceptorsProcessor,
                _actualHandlerInterceptor);
            return next.InterceptAsync(contextForNext);
        }
    }
}
