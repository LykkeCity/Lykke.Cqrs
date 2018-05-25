using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using Common.Log;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.Configuration;
using Lykke.Cqrs.Utils;

namespace Lykke.Cqrs
{
    public class CqrsEngine : ICqrsEngine, IDisposable
    {
        private readonly IMessagingEngine m_MessagingEngine;
        private readonly CompositeDisposable m_Subscription = new CompositeDisposable();
        private readonly IEndpointProvider m_EndpointProvider;
        private readonly List<Context> m_Contexts;
        private readonly IRegistration[] m_Registrations;
        private readonly IDependencyResolver m_DependencyResolver;
        private readonly bool m_CreateMissingEndpoints;

        protected IMessagingEngine MessagingEngine
        {
            get { return m_MessagingEngine; }
        }

        internal IEndpointResolver EndpointResolver { get; set; }
        internal List<Context> Contexts { get { return m_Contexts; } }
        internal IDependencyResolver DependencyResolver { get { return m_DependencyResolver; } }
        internal bool EnableInputMessagesLogging { get; private set; }

        public ILog Log { get; }
        public RouteMap DefaultRouteMap { get; private set; }

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

        public CqrsEngine(
            ILog log,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            bool createMissingEndpoints,
            bool enableInputMessagesLogging,
            params IRegistration[] registrations)
        {
            Log = log;
            m_CreateMissingEndpoints = createMissingEndpoints;
            m_DependencyResolver = dependencyResolver;
            m_Registrations = registrations;
            EndpointResolver = new DefaultEndpointResolver();
            m_MessagingEngine = messagingEngine;
            m_EndpointProvider = endpointProvider;
            m_Contexts = new List<Context>();
            DefaultRouteMap = new RouteMap("default");
            EnableInputMessagesLogging = enableInputMessagesLogging;
            Init();
        }

        private void Init()
        {
            foreach (var registration in m_Registrations)
            {
                registration.Create(this);
            }
            foreach (var registration in m_Registrations)
            {
                registration.Process(this);
            }

            EnsureEndpoints();

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
                                callback = (@event, acknowledge,headers) => context.EventDispatcher.Dispatch(remoteBoundedContext, new[] {Tuple.Create(@event, acknowledge)});
                                messageTypeName = "event";
                                break;
                            case RouteType.Commands:
                                callback = (command, acknowledge, headers) => context.CommandDispatcher.Dispatch(command, acknowledge, endpoint, routeName);
                                messageTypeName = "command";
                                break;
                        }

                        m_Subscription.Add(m_MessagingEngine.Subscribe(
                            endpoint,
                            callback,
                            (type, acknowledge) =>
                            {
                                throw new InvalidOperationException(string.Format("Unknown {0} received: {1}", messageTypeName, type));
                                //acknowledge(0, true);
                            },
                            processingGroup,
                            (int)subscription.priority,
                            subscription.types));
                    }
                }
            }

            foreach (var boundedContext in Contexts)
            {
                boundedContext.Processes.ForEach(p => p.Start(boundedContext, boundedContext.EventsPublisher));
            }
        }

        private void EnsureEndpoints()
        {
            var allEndpointsAreValid = true;
            var errorMessage = new StringBuilder("Some endpoints are not valid:").AppendLine();
            Log.WriteInfo(nameof(CqrsEngine), nameof(EnsureEndpoints), "Endpoints verification");

            foreach (var context in new []{DefaultRouteMap}.Concat(Contexts))
            {
                foreach (var route in context)
                {
                    m_MessagingEngine.AddProcessingGroup(route.ProcessingGroupName, route.ProcessingGroup);
                }
            }

            foreach (var routeMap in (new[] { DefaultRouteMap }).Concat(Contexts))
            {
                Log.WriteInfo(nameof(CqrsEngine), nameof(EnsureEndpoints), $"Context '{routeMap.Name}':");

                routeMap.ResolveRoutes(m_EndpointProvider);
                foreach (var route in routeMap)
                {
                    Log.WriteInfo(nameof(CqrsEngine), nameof(EnsureEndpoints), $"\t{route.Type} route '{route.Name}':");
                    foreach (var messageRoute in route.MessageRoutes)
                    {
                        string error;
                        var routingKey = messageRoute.Key;
                        var endpoint = messageRoute.Value;
                        var messageTypeName = route.Type.ToString().ToLower().TrimEnd('s');
                        bool result = true;

                        if (routingKey.CommunicationType == CommunicationType.Publish)
                        {
                            if (!m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Publish, m_CreateMissingEndpoints, out error))
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

                            Log.WriteInfo(
                                nameof(CqrsEngine),
                                nameof(EnsureEndpoints),
                                $"\t\tPublishing  '{routingKey.MessageType.Name}' to {endpoint}\t{(result ? "OK" : "ERROR:" + error)}");
                        }

                        if (routingKey.CommunicationType == CommunicationType.Subscribe)
                        {
                            if (!m_MessagingEngine.VerifyEndpoint(endpoint, EndpointUsage.Subscribe, m_CreateMissingEndpoints, out error))
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

                            Log.WriteInfo(
                                nameof(CqrsEngine),
                                nameof(EnsureEndpoints),
                                $"\t\tSubscribing '{routingKey.MessageType.Name}' on {endpoint}\t{(result ? "OK" : "ERROR:" + error)}");
                        }
                        allEndpointsAreValid = allEndpointsAreValid && result;
                    }
                }
            }

            if (!allEndpointsAreValid)
                throw new ApplicationException(errorMessage.ToString());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Contexts
                    .Where(b => b != null && b.Processes != null)
                    .SelectMany(p => p.Processes)
                    .ToList()
                    .ForEach(process => process.Dispose());
                Contexts
                    .Where(b => b != null)
                    .ToList()
                    .ForEach(context => context.Dispose());

                if (m_Subscription != null)
                    m_Subscription.Dispose();
            }
        }

        public void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0)
        {
            if (!SendMessage(typeof (T), command, RouteType.Commands, boundedContext, priority, remoteBoundedContext))
            {
                if (boundedContext != null)
                    throw new InvalidOperationException(string.Format("bound context '{0}' does not support command '{1}' with priority {2}", boundedContext, typeof(T), priority));
                throw new InvalidOperationException(string.Format("Default route map does not contain rout for command '{0}' with priority {1}", typeof(T), priority));
            }
        }

        public void PublishEvent(object @event, string boundedContext)
        {
            if (@event == null)
                throw new ArgumentNullException("event");
            if (!SendMessage(@event.GetType(), @event, RouteType.Events, boundedContext, 0))
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'", boundedContext, @event.GetType()));
        }

        private bool SendMessage(Type type, object message, RouteType routeType, string context, uint priority, string remoteBoundedContext = null)
        {
            RouteMap routeMap = DefaultRouteMap;
            if (context != null)
            {
                routeMap = Contexts.FirstOrDefault(bc => bc.Name == context);
                if (routeMap == null)
                {
                    throw new ArgumentException(string.Format("bound context {0} not found", context), "context");
                }
            }
            var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                routeType == RouteType.Commands ? "Cqrs send command" : "Cqrs publish event",
                type.Name,
                context,
                remoteBoundedContext);
            try
            {
                var published = routeMap.PublishMessage(
                    m_MessagingEngine,
                    type,
                    message,
                    routeType,
                    priority,
                    remoteBoundedContext);
                if (!published && routeType == RouteType.Commands)
                    published = DefaultRouteMap.PublishMessage(
                        m_MessagingEngine,
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

        /*
        internal void PublishEvent(object @event, Endpoint endpoint, string processingGroup, Dictionary<string, string> headers = null)
        {
            m_MessagingEngine.Send(@event, endpoint, processingGroup,headers);
        }
        */
    }
}
