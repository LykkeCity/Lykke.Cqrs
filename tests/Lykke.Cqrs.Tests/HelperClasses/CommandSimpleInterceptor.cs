using System;
using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;
using Lykke.Cqrs.Middleware;

namespace Lykke.Cqrs.Tests.HelperClasses
{
    internal class CommandSimpleInterceptor : ICommandInterceptor
    {
        internal bool Intercepted { get; private set; }
        internal ICommandInterceptor Next { get; private set; }
        internal DateTime? InterceptionTimestamp { get; private set; }

        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            Intercepted = true;
            Next = context.Next;
            InterceptionTimestamp = DateTime.UtcNow;

            return Next.InterceptAsync(context);
        }
    }
}
