using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    public class RouteMap : IRouteMap
    {
        private readonly Dictionary<string, Route> m_RouteMap = new Dictionary<string, Route>();

        public string Name { get; private set; }

        public RouteMap(string name)
        {
            Name = name;
        }

        public IEnumerator<Route> GetEnumerator()
        {
            return m_RouteMap.Values.Where(route => route.Type != null).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        Route IRouteMap.this[string name]
        {
            get
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException("name should be not empty string", nameof(name));
                if (m_RouteMap.TryGetValue(name, out var route))
                    return route;

                route = new Route(name, Name);
                m_RouteMap.Add(name, route);
                return route;
            }
        }

        public bool PublishMessage(
            IMessagingEngine messagingEngine,
            Type type,
            object message,
            RouteType routeType,
            uint priority,
            string remoteBoundedContext = null)
        {
            var publishDirections = (
                    from route in this
                    from messageRoute in route.MessageRoutes
                    where messageRoute.Key.MessageType == type &&
                        messageRoute.Key.RouteType == routeType &&
                        messageRoute.Key.Priority == priority &&
                        messageRoute.Key.RemoteBoundedContext == remoteBoundedContext
                    select new
                    {
                        processingGroup = route.ProcessingGroupName,
                        endpoint = messageRoute.Value
                    }
                ).ToList();
            if (!publishDirections.Any())
                return false;

            foreach (var direction in publishDirections)
            {
                messagingEngine.Send(message, direction.endpoint, direction.processingGroup);
            }
            return true;
        }

        internal void ResolveRoutes(IEndpointProvider endpointProvider)
        {
            foreach (Route route in m_RouteMap.Values)
            {
                route.Resolve(endpointProvider);
            }
        }
    }
}