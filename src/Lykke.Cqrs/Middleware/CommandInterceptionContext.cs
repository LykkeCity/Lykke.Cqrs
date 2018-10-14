using System.Threading.Tasks;

namespace Lykke.Cqrs.Middleware
{
    internal class CommandInterceptionContext : ICommandInterceptionContext
    {
        private readonly CommandInterceptionCommonContext _commonContext;
        private readonly int _interceptorIndex;
        private readonly CommandInterceptorsQueue _interceptorsProcessor;
        private readonly CommandActualHandlerInterceptor _actualHandlerInterceptor;

        public object Command
        {
            get => _commonContext.Command;
            set => _commonContext.Command = value;
        }

        public object HandlerObject => _commonContext.HandlerObject;

        public IEventPublisher EventPublisher
        {
            get => _commonContext.EventPublisher;
            set => _commonContext.EventPublisher = value;
        }

        internal CommandInterceptionContext(
            CommandInterceptionCommonContext commonContext,
            int interceptorIndex,
            CommandInterceptorsQueue interceptorsProcessor,
            CommandActualHandlerInterceptor actualHandlerInterceptor)
        {
            _commonContext = commonContext;
            _interceptorIndex = interceptorIndex;
            _interceptorsProcessor = interceptorsProcessor;
            _actualHandlerInterceptor = actualHandlerInterceptor;
        }

        public Task<CommandHandlingResult> InvokeNextAsync()
        {
            var next = _interceptorsProcessor.ResolveNext(_interceptorIndex, _actualHandlerInterceptor);
            if (next == null)
                return Task.FromResult(CommandHandlingResult.Ok());

            var contextForNext = new CommandInterceptionContext(
                _commonContext,
                _interceptorIndex + 1,
                _interceptorsProcessor,
                _actualHandlerInterceptor);
            return next.InterceptAsync(contextForNext);
        }
    }
}
