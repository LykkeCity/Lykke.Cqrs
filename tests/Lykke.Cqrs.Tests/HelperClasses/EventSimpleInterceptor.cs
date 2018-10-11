using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;
using Lykke.Cqrs.Middleware;

namespace Lykke.Cqrs.Tests.HelperClasses
{
    internal class EventSimpleInterceptor : IEventInterceptor
    {
        internal bool Intercepted { get; private set; }
        internal IEventInterceptor Next { get; private set; }

        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            Intercepted = true;
            Next = context.NextResolver(this);

            return Next.InterceptAsync(context);
        }
    }
}
