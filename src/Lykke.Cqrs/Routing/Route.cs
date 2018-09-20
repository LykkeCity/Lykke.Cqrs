using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lykke.Messaging;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs.Routing
{
    public class Route
    {
        private readonly Dictionary<RoutingKey, Endpoint> m_MessageRoutes = new Dictionary<RoutingKey, Endpoint>();
        private readonly Dictionary<RoutingKey, IEndpointResolver> m_RouteResolvers = new Dictionary<RoutingKey, IEndpointResolver>();
        private readonly string m_Context;

        public string Name { get; set; }
        public RouteType? Type { get; set; }

        public Route(string name, string context)
        {
            ProcessingGroup = new ProcessingGroupInfo();
            m_Context = context;
            Name = name;
        }

        public IDictionary<RoutingKey, Endpoint> MessageRoutes
        {
            get { return new ReadOnlyDictionary<RoutingKey, Endpoint>(m_MessageRoutes); }
        }

        public RoutingKey[] RoutingKeys
        {
            get { return m_RouteResolvers.Keys.ToArray(); }
        }

        public string ProcessingGroupName
        {
            get { return string.Format("cqrs.{0}.{1}", m_Context ?? "default", Name); }
        }

        public ProcessingGroupInfo ProcessingGroup { get; set; }

        public void AddPublishedCommand(Type command, uint priority, string boundedContext, IEndpointResolver resolver)
        {
            if (Type == null)
                Type = RouteType.Commands;
            if (Type != RouteType.Commands)
                throw new ApplicationException(string.Format("Can not publish commands with events route '{0}'.", Name));

            var routingKey = new RoutingKey
            {
                LocalContext = m_Context,
                MessageType = command,
                Priority = priority,
                RouteType = Type.Value,
                CommunicationType = CommunicationType.Publish,
                RemoteBoundedContext = boundedContext
            };
            m_RouteResolvers[routingKey] = resolver;
        }

        public void AddSubscribedCommand(Type command, uint priority, IEndpointResolver resolver)
        {
            if (Type == null)
                Type = RouteType.Commands;
            if (Type != RouteType.Commands)
                throw new ApplicationException(string.Format("Can not subscribe for commands on events route '{0}'.", Name));

            var routingKey = new RoutingKey
            {
                LocalContext = m_Context,
                MessageType = command,
                Priority = priority,
                RouteType = Type.Value,
                CommunicationType = CommunicationType.Subscribe
            };
            m_RouteResolvers[routingKey] = resolver;
        }

        public void AddPublishedEvent(Type @event, uint priority, IEndpointResolver resolver)
        {
            if (Type == null)
                Type = RouteType.Events;
            if (Type != RouteType.Events)
                throw new ApplicationException(string.Format("Can not publish for events with commands route '{0}'.", Name));

            var routingKey = new RoutingKey
            {
                LocalContext = m_Context,
                RouteType = Type.Value,
                MessageType = @event,
                Priority = priority,
                CommunicationType = CommunicationType.Publish  
            };
            m_RouteResolvers[routingKey] = resolver;
        }

        public void AddSubscribedEvent(Type @event, uint priority, string remoteBoundedContext, IEndpointResolver resolver, bool exclusive)
        {
            if (Type == null)
                Type = RouteType.Events;
            if (Type != RouteType.Events)
                throw new ApplicationException(string.Format("Can not subscribe for events on commands route '{0}'.", Name));

            var routingKey = new RoutingKey
            {
                LocalContext = m_Context,
                RouteType = Type.Value,
                MessageType = @event,
                RemoteBoundedContext = remoteBoundedContext,
                Priority = priority,
                CommunicationType = CommunicationType.Subscribe,
                Exclusive = exclusive
            };
            m_RouteResolvers[routingKey] = resolver;
        }

        public Endpoint this[RoutingKey key]
        {
            get { return m_MessageRoutes[key]; }
        }

        public void Resolve(IEndpointProvider endpointProvider)
        {
            foreach (var pair in m_RouteResolvers)
            {
                var endpoint = pair.Value.Resolve(Name, pair.Key, endpointProvider);
                m_MessageRoutes[pair.Key] = endpoint;
            }
        }
    }
}