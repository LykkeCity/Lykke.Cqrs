using System;
using System.Collections.Generic;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    public class RabbitMqConventionEndpointResolver : IEndpointResolver
    {
        private readonly Dictionary<Tuple<string, RoutingKey>, Endpoint> _cache =
            new Dictionary<Tuple<string, RoutingKey>, Endpoint>();

        private readonly string _transport;
        private readonly string _serializationFormat;
        private readonly string _exclusiveQueuePostfix;
        private readonly string _environmentPrefix;
        private readonly string _commandsKeyword;
        private readonly string _eventsKeyword;

        public RabbitMqConventionEndpointResolver(
            string transport,
            string serializationFormat,
            string exclusiveQueuePostfix = null,
            string environment = null,
            string commandsKeyword = null,
            string eventsKeyword = null)
        {
            _environmentPrefix = environment != null ? $"{environment}." : string.Empty;
            _exclusiveQueuePostfix = $".{exclusiveQueuePostfix ?? "projections"}";
            _transport = transport;
            _serializationFormat = serializationFormat;
            _commandsKeyword = commandsKeyword;
            _eventsKeyword = eventsKeyword;
        }

        private string CreateQueueName(string queue, bool exclusive)
        {
            return $"{_environmentPrefix}{queue}{(exclusive ? _exclusiveQueuePostfix : string.Empty)}";
        }

        private string CreateExchangeName(string exchange)
        {
            return $"topic://{_environmentPrefix}{exchange}";
        }

        private Endpoint CreateEndpoint(string route, RoutingKey key)
        {
            var rmqRoutingKey = key.Priority == 0 ? key.MessageType.Name : key.MessageType.Name + "." + key.Priority;
            var queueName = key.Priority == 0 ? route : route + "." + key.Priority;
            if (key.RouteType == RouteType.Commands && key.CommunicationType == CommunicationType.Subscribe)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = CreateExchangeName(
                            $"{key.LocalContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{rmqRoutingKey}"),
                        Subscribe = CreateQueueName(
                            $"{key.LocalContext}.queue.{GetKewordByRoutType(key.RouteType)}.{queueName}",
                            key.Exclusive)
                    },
                    SerializationFormat = _serializationFormat,
                    SharedDestination = true,
                    TransportId = _transport
                };
            }

            if (key.RouteType == RouteType.Commands && key.CommunicationType == CommunicationType.Publish)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = CreateExchangeName(
                            $"{key.RemoteBoundedContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{rmqRoutingKey}"),
                        Subscribe = null
                    },
                    SerializationFormat = _serializationFormat,
                    SharedDestination = true,
                    TransportId = _transport
                };
            }

            if (key.RouteType == RouteType.Events && key.CommunicationType == CommunicationType.Subscribe)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = CreateExchangeName(
                            $"{key.RemoteBoundedContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{key.MessageType.Name}"),
                        Subscribe = CreateQueueName(
                            $"{key.LocalContext}.queue.{key.RemoteBoundedContext}.{GetKewordByRoutType(key.RouteType)}.{route}",
                            key.Exclusive)
                    },
                    SerializationFormat = _serializationFormat,
                    SharedDestination = true,
                    TransportId = _transport
                };
            }

            if (key.RouteType == RouteType.Events && key.CommunicationType == CommunicationType.Publish)
            {
                return new Endpoint
                {
                    Destination = new Destination
                    {
                        Publish = CreateExchangeName(
                            $"{key.LocalContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{key.MessageType.Name}"),
                        Subscribe = null
                    },
                    SerializationFormat = _serializationFormat,
                    SharedDestination = true,
                    TransportId = _transport
                };
            }
            return default(Endpoint);
        }

        private string GetKewordByRoutType(RouteType routeType)
        {
            string keyword = null;
            switch (routeType)
            {
                case RouteType.Commands:
                    keyword = _commandsKeyword;
                    break;
                case RouteType.Events:
                    keyword = _eventsKeyword;
                    break;
            }
            return keyword ?? routeType.ToString().ToLower();
        }

        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(Tuple.Create(route, key), out var ep))
                    return ep;

                if (endpointProvider.Contains(route))
                {
                    ep = endpointProvider.Get(route);
                    _cache.Add(Tuple.Create(route, key), ep);
                    return ep;
                }

                ep = CreateEndpoint(route, key);
                _cache.Add(Tuple.Create(route, key), ep);
                return ep;
            }
        }
    }
}