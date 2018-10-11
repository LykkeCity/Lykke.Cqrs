using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Cqrs.Middleware;

namespace Lykke.Cqrs.Abstractions.Middleware
{
    [PublicAPI]
    public interface IEventInterceptor
    {
        Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context);
    }
}
