using System.Collections.Generic;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    public interface IRouteMap : IEnumerable<Route>
    {
        Route this[string name] { get; }
    }
}