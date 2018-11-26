﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.Configuration;
using Lykke.Cqrs.Middleware;
using Lykke.Cqrs.Utils;

namespace Lykke.Cqrs
{
    [PublicAPI]
    public class CqrsEngine : ICqrsEngine, IDisposable
    {
        private readonly CompositeDisposable _subscription = new CompositeDisposable();
        private readonly IEndpointProvider _endpointProvider;
        private readonly bool _createMissingEndpoints;
        private readonly ILogFactory _logFactory;
        private readonly ILog _log;

        protected IMessagingEngine MessagingEngine { get; }

        internal IEndpointResolver EndpointResolver { get; set; }
        internal List<Context> Contexts { get; }
        internal CommandInterceptorsQueue CommandInterceptorsQueue { get; }
        internal EventInterceptorsQueue EventInterceptorsQueue { get; }
        internal IDependencyResolver DependencyResolver { get; }
        internal bool EnableInputMessagesLogging { get; }

        public RouteMap DefaultRouteMap { get; }

        #region Obsolete ctors

        [Obsolete]
        public CqrsEngine(
            ILog log,
            IMessagingEngine messagingEngine,
            params IRegistration[] registrations)
            : this(
                log,
                new DefaultDependencyResolver(),
                messagingEngine,
                new DefaultEndpointProvider(),
                false,
                true,
                registrations)
        {
        }

        [Obsolete]
        public CqrsEngine(
            ILog log,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations)
            : this(
                log,
                new DefaultDependencyResolver(),
                messagingEngine,
                endpointProvider,
                false,
                true,
                registrations)
        {
        }

        [Obsolete]
        public CqrsEngine(
            ILog log,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations)
            : this(
                log,
                dependencyResolver,
                messagingEngine,
                endpointProvider,
                false,
                true,
                registrations)
        {
        }

        [Obsolete]
        public CqrsEngine(
            ILog log,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            bool createMissingEndpoints,
            params IRegistration[] registrations)
            : this(
                log,
                dependencyResolver,
                messagingEngine,
                endpointProvider,
                createMissingEndpoints,
                true,
                registrations)
        {
        }

        [Obsolete]
        public CqrsEngine(
            ILog log,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            bool createMissingEndpoints,
            bool enableInputMessagesLogging,
            params IRegistration[] registrations)
        {
            _log = log;
            _createMissingEndpoints = createMissingEndpoints;
            DependencyResolver = dependencyResolver;
            EndpointResolver = new DefaultEndpointResolver();
            MessagingEngine = messagingEngine;
            _endpointProvider = endpointProvider;
            Contexts = new List<Context>();
            DefaultRouteMap = new RouteMap("default");
            EnableInputMessagesLogging = enableInputMessagesLogging;
            CommandInterceptorsQueue = new CommandInterceptorsQueue();
            EventInterceptorsQueue = new EventInterceptorsQueue();

            PartiallyInitRegistrations(registrations);
        }

        #endregion

        public CqrsEngine(
            ILogFactory logFactory,
            IMessagingEngine messagingEngine,
            params IRegistration[] registrations)
            : this(
                logFactory,
                new DefaultDependencyResolver(),
                messagingEngine,
                new DefaultEndpointProvider(),
                false,
                true,
                registrations)
        {
        }

        public CqrsEngine(
            ILogFactory logFactory,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations)
            : this(
                logFactory,
                new DefaultDependencyResolver(),
                messagingEngine,
                endpointProvider,
                false,
                true,
                registrations)
        {
        }

        public CqrsEngine(
            ILogFactory logFactory,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations)
            : this(
                logFactory,
                dependencyResolver,
                messagingEngine,
                endpointProvider,
                false,
                true,
                registrations)
        {
        }

        public CqrsEngine(
            ILogFactory logFactory,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            bool createMissingEndpoints,
            params IRegistration[] registrations)
            : this(
                logFactory,
                dependencyResolver,
                messagingEngine,
                endpointProvider,
                createMissingEndpoints,
                true,
                registrations)
        {
        }

        public CqrsEngine(
            ILogFactory logFactory,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            bool createMissingEndpoints,
            bool enableInputMessagesLogging,
            params IRegistration[] registrations)
        {
            _log = logFactory.CreateLog(this);
            _logFactory = logFactory;
            _createMissingEndpoints = createMissingEndpoints;
            DependencyResolver = dependencyResolver;
            EndpointResolver = new DefaultEndpointResolver();
            MessagingEngine = messagingEngine;
            _endpointProvider = endpointProvider;
            Contexts = new List<Context>();
            DefaultRouteMap = new RouteMap("default");
            EnableInputMessagesLogging = enableInputMessagesLogging;
            CommandInterceptorsQueue = new CommandInterceptorsQueue();
            EventInterceptorsQueue = new EventInterceptorsQueue();

            PartiallyInitRegistrations(registrations);
        }

        internal CommandDispatcher CreateCommandsDispatcher(string name, long failedCommandRetryDelay)
        {
            return _logFactory != null
                ? new CommandDispatcher(
                    _logFactory,
                    name,
                    CommandInterceptorsQueue,
                    EnableInputMessagesLogging,
                    failedCommandRetryDelay)
                : new CommandDispatcher(
                    _log,
                    name,
                    CommandInterceptorsQueue,
                    EnableInputMessagesLogging,
                    failedCommandRetryDelay);
        }

        internal EventDispatcher CreateEventsDispatcher(string name)
        {
            return _logFactory != null
                ? new EventDispatcher(
                    _logFactory,
                    name,
                    EventInterceptorsQueue,
                    EnableInputMessagesLogging)
                : new EventDispatcher(
                    _log,
                    name,
                    EventInterceptorsQueue,
                    EnableInputMessagesLogging);
        }

        public void Start()
        {
            EnsureEndpoints(CommunicationType.Subscribe);

            InitSubscriptions();

            foreach (var boundedContext in Contexts)
            {
                boundedContext.Processes.ForEach(p => p.Start(boundedContext, boundedContext.EventsPublisher));
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Contexts
                .Where(b => b?.Processes != null)
                .SelectMany(p => p.Processes)
                .ToList()
                .ForEach(process => process.Dispose());
            Contexts
                .Where(b => b != null)
                .ToList()
                .ForEach(context => context.Dispose());

            _subscription?.Dispose();
        }

        public void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0)
        {
            if (!SendMessage(typeof (T), command, RouteType.Commands, boundedContext, priority, remoteBoundedContext))
            {
                if (boundedContext != null)
                    throw new InvalidOperationException($"Bound context '{boundedContext}' does not support command '{typeof(T)}' with priority {priority}");
                throw new InvalidOperationException($"Default route map does not contain rout for command '{typeof(T)}' with priority {priority}");
            }
        }

        public void PublishEvent(object @event, string boundedContext)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            if (!SendMessage(@event.GetType(), @event, RouteType.Events, boundedContext, 0))
                throw new InvalidOperationException($"Bound context '{boundedContext}' does not support event '{@event.GetType()}'");
        }

        private bool SendMessage(Type type, object message, RouteType routeType, string context, uint priority, string remoteBoundedContext = null)
        {
            RouteMap routeMap = DefaultRouteMap;
            if (context != null)
            {
                routeMap = Contexts.FirstOrDefault(bc => bc.Name == context);
                if (routeMap == null)
                    throw new ArgumentException($"Bound context {context} not found", nameof(context));
            }
            var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                routeType == RouteType.Commands ? "Cqrs send command" : "Cqrs publish event",
                type.Name,
                context,
                remoteBoundedContext);
            try
            {
                var published = routeMap.PublishMessage(
                    MessagingEngine,
                    type,
                    message,
                    routeType,
                    priority,
                    remoteBoundedContext);
                if (!published && routeType == RouteType.Commands)
                    published = DefaultRouteMap.PublishMessage(
                        MessagingEngine,
                        type,
                        message,
                        routeType,
                        priority,
                        remoteBoundedContext);
                return published;
            }
            catch (Exception e)
            {
                TelemetryHelper.SubmitException(telemtryOperation, e);
                throw;
            }
            finally
            {
                TelemetryHelper.SubmitOperationResult(telemtryOperation);
            }
        }

        private void PartiallyInitRegistrations(IEnumerable<IRegistration> registrations)
        {
            foreach (var registration in registrations)
            {
                registration.Create(this);
            }

            foreach (var registration in registrations)
            {
                registration.Process(this);
            }

            foreach (var routeMap in new List<RouteMap> { DefaultRouteMap }.Concat(Contexts))
            {
                foreach (var route in routeMap)
                {
                    MessagingEngine.AddProcessingGroup(route.ProcessingGroupName, route.ProcessingGroup);
                }

                routeMap.ResolveRoutes(_endpointProvider);
            }

            EnsureEndpoints(CommunicationType.Publish);
        }

        private void EnsureEndpoints(CommunicationType processingCommunicationType)
        {
            var allEndpointsAreValid = true;
            var errorMessage = new StringBuilder("Some endpoints are not valid:").AppendLine();

            foreach (var routeMap in new List<RouteMap> { DefaultRouteMap }.Concat(Contexts))
            {
                foreach (var route in routeMap)
                {
                    foreach (var messageRoute in route.MessageRoutes)
                    {
                        var routingKey = messageRoute.Key;
                        if (routingKey.CommunicationType != processingCommunicationType)
                            continue;

                        var endpoint = messageRoute.Value;
                        var messageTypeName = route.Type.ToString().ToLower().TrimEnd('s');

                        bool result = true;
                        string error;
                        switch (routingKey.CommunicationType)
                        {
                            case CommunicationType.Publish:
                                if (!MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Publish, _createMissingEndpoints, out error))
                                {
                                    errorMessage
                                        .AppendFormat(
                                            "Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for publishing: {5}.",
                                            routeMap.Name,
                                            route.Name,
                                            messageTypeName,
                                            routingKey.MessageType.Name,
                                            endpoint, error)
                                        .AppendLine();
                                    result = false;
                                }

                                _log.WriteInfo(
                                    nameof(CqrsEngine),
                                    nameof(EnsureEndpoints),
                                    $"Context {routeMap.Name}: publishing '{routingKey.MessageType.Name}' to {endpoint}\t{(result ? "OK" : "ERROR:" + error)}");
                                break;
                            case CommunicationType.Subscribe:
                                if (!MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Subscribe, _createMissingEndpoints, out error))
                                {
                                    errorMessage
                                        .AppendFormat(
                                            "Route '{1}' within bounded context '{0}' for {2} type '{3}' has resolved endpoint {4} that is not properly configured for subscription: {5}.",
                                            routeMap.Name,
                                            route.Name,
                                            messageTypeName,
                                            routingKey.MessageType.Name,
                                            endpoint,
                                            error)
                                        .AppendLine();
                                    result = false;
                                }

                                _log.WriteInfo(
                                    nameof(CqrsEngine),
                                    nameof(EnsureEndpoints),
                                    $"Context {routeMap.Name}: subscribing '{routingKey.MessageType.Name}' on {endpoint}\t{(result ? "OK" : "ERROR:" + error)}");
                                break;
                        }
                        allEndpointsAreValid = allEndpointsAreValid && result;
                    }
                }
            }

            if (!allEndpointsAreValid)
                throw new ApplicationException(errorMessage.ToString());
        }

        private void InitSubscriptions()
        {
            foreach (var boundedContext in Contexts)
            {
                foreach (var route in boundedContext.Routes)
                {
                    Context context = boundedContext;
                    var subscriptions = route.MessageRoutes
                        .Where(r => r.Key.CommunicationType == CommunicationType.Subscribe)
                        .Select(r => new
                        {
                            type = r.Key.MessageType,
                            priority = r.Key.Priority,
                            remoteBoundedContext = r.Key.RemoteBoundedContext,
                            endpoint = new Endpoint(
                                r.Value.TransportId,
                                "",
                                r.Value.Destination.Subscribe,
                                r.Value.SharedDestination,
                                r.Value.SerializationFormat)
                        })
                        .GroupBy(x => Tuple.Create(x.endpoint, x.priority, x.remoteBoundedContext))
                        .Select(g => new
                        {
                            endpoint = g.Key.Item1,
                            priority = g.Key.Item2,
                            remoteBoundedContext = g.Key.Item3,
                            types = g.Select(x => x.type).ToArray()
                        });

                    foreach (var subscription in subscriptions)
                    {
                        var processingGroup = route.ProcessingGroupName;
                        var routeName = route.Name;
                        var endpoint = subscription.endpoint;
                        var remoteBoundedContext = subscription.remoteBoundedContext;
                        CallbackDelegate<object> callback = null;
                        string messageTypeName = null;
                        switch (route.Type)
                        {
                            case RouteType.Events:
                                callback = (@event, acknowledge, headers) => context.EventDispatcher.Dispatch(remoteBoundedContext, new[] { Tuple.Create(@event, acknowledge) });
                                messageTypeName = "event";
                                break;
                            case RouteType.Commands:
                                callback = (command, acknowledge, headers) => context.CommandDispatcher.Dispatch(command, acknowledge, endpoint, routeName);
                                messageTypeName = "command";
                                break;
                        }

                        _subscription.Add(MessagingEngine.Subscribe(
                            endpoint,
                            callback,
                            (type, acknowledge) => throw new InvalidOperationException($"Unknown {messageTypeName} received: {type}"),
                            processingGroup,
                            (int)subscription.priority,
                            subscription.types));
                    }
                }
            }
        }
    }
}
