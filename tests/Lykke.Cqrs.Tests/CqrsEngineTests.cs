using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lykke.Common.Log;
using Lykke.Messaging;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Messaging.RabbitMq;
using Lykke.Messaging.Serialization;
using Lykke.Cqrs.Configuration;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Moq;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    [TestFixture]
    internal class CqrsEngineTests
    {
        private readonly ILogFactory _logFactory;

        public CqrsEngineTests()
        {
            _logFactory = LogFactory.Create().AddUnbufferedConsole();
        }

        public void Dispose()
        {
            _logFactory?.Dispose();
        }

        [Test]
        public void ListenSameCommandOnDifferentEndpointsTest()
        {
            var commandHandler = new CommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                    })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                        Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                        Register.BoundedContext("bc")
                           .PublishingEvents(typeof(int)).With("eventExchange")
                           .ListeningCommands(typeof(string)).On("exchange1")
                           .ListeningCommands(typeof(string)).On("exchange2")
                           .WithCommandsHandler(commandHandler)))
                {
                    engine.StartPublishers();
                    engine.StartSubscribers();
                    messagingEngine.Send("test1", new Endpoint("InMemory", "exchange1", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("test2", new Endpoint("InMemory", "exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("test3", new Endpoint("InMemory", "exchange3", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(7000);

                    Assert.That(commandHandler.HandledCommands, Is.EquivalentTo(new[] { "test1", "test2" }));
                }
            }
        }

        [Test]
        public void ContextUsesDefaultRouteForCommandPublishingIfItDoesNotHaveItsOwnTest()
        {
            var bcCommands = new Endpoint("InMemory", "bcCommands", serializationFormat: SerializationFormat.Json);
            var defaultCommands = new Endpoint("InMemory", "defaultCommands", serializationFormat: SerializationFormat.Json);
            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                    })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.BoundedContext("bc2")
                        .PublishingCommands(typeof(int)).To("bc1").With("bcCommands"),
                    Register.DefaultRouting
                        .PublishingCommands(typeof(string)).To("bc1").With("defaultCommands")
                        .PublishingCommands(typeof(int)).To("bc1").With("defaultCommands")))
                {
                    engine.StartPublishers();
                    var received = new AutoResetEvent(false);
                    using (messagingEngine.Subscribe(defaultCommands, o => received.Set(), s => { }, typeof(string)))
                    {
                        engine.SendCommand("test", "bc2", "bc1");
                        Assert.That(received.WaitOne(2000), Is.True, "not defined for context command was not routed with default route map");
                    }

                    using (messagingEngine.Subscribe(bcCommands, o => received.Set(), s => { }, typeof(int)))
                    {
                        engine.SendCommand(1, "bc2", "bc1");
                        Assert.That(received.WaitOne(2000), Is.True, "defined for context command was not routed with context route map");
                    }
                }
            }
        }

        [Test, Ignore("integration")]
        public void SagaTest()
        {
            var commandHandler = new CustomCommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"rmq", new TransportInfo("amqp://localhost/LKK", "guest", "guest", "None", "RabbitMq")}
                    }),
                new RabbitMqTransportFactory(_logFactory)))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(
                        new RabbitMqConventionEndpointResolver("rmq", SerializationFormat.Json, environment: "dev")),
                    Register.BoundedContext("operations")
                        .PublishingCommands(typeof(CreateCashOutCommand)).To("lykke-wallet").With("operations-commands")
                        .ListeningEvents(typeof(CashOutCreatedEvent)).From("lykke-wallet").On("lykke-wallet-events"),

                    Register.BoundedContext("lykke-wallet")
                        .FailedCommandRetryDelay((long)TimeSpan.FromSeconds(2).TotalMilliseconds)
                        .ListeningCommands(typeof(CreateCashOutCommand)).On("operations-commands")
                        .PublishingEvents(typeof(CashOutCreatedEvent)).With("lykke-wallet-events")
                        .WithCommandsHandler(commandHandler),

                    Register.Saga<TestSaga>("swift-cashout")
                        .ListeningEvents(typeof(CashOutCreatedEvent)).From("lykke-wallet").On("lykke-wallet-events"),

                    Register.DefaultRouting.PublishingCommands(typeof(CreateCashOutCommand)).To("lykke-wallet")
                        .With("operations-commands"))
                )
                {
                    engine.StartPublishers();
                    engine.StartSubscribers();
                    engine.SendCommand(new CreateCashOutCommand { Payload = "test data" }, null, "lykke-wallet");

                    Assert.True(TestSaga.Complete.WaitOne(2000), "Saga has not got events or failed to send command");
                }
            }
        }

        [Test]
        public void FluentApiTest()
        {
            var endpointProvider = new Mock<IEndpointProvider>();
            endpointProvider.Setup(r => r.Get("high")).Returns(new Endpoint("InMemory", "high", true, SerializationFormat.Json));
            endpointProvider.Setup(r => r.Get("low")).Returns(new Endpoint("InMemory", "low", true, SerializationFormat.Json));
            endpointProvider.Setup(r => r.Get("medium")).Returns(new Endpoint("InMemory", "medium", true, SerializationFormat.Json));

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")},
                    {"rmq", new TransportInfo("none", "none", "none", null, "InMemory")}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    endpointProvider.Object,
                    Register.BoundedContext("bc")
                        .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                        .ListeningCommands(typeof(string)).On("commandsRoute")
                        //same as .PublishingCommands(typeof(string)).To("bc").With("selfCommandsRoute")  
                        .WithLoopback("selfCommandsRoute")
                        .PublishingEvents(typeof(int)).With("eventsRoute")

                        //explicit prioritization 
                        .ListeningCommands(typeof(string)).On("explicitlyPrioritizedCommandsRoute")
                        .Prioritized(lowestPriority: 2)
                        .WithEndpoint("high").For(key => key.Priority == 0)
                        .WithEndpoint("medium").For(key => key.Priority == 1)
                        .WithEndpoint("low").For(key => key.Priority == 2)

                        //resolver based prioritization 
                        .ListeningCommands(typeof(string)).On("prioritizedCommandsRoute")
                        .Prioritized(lowestPriority: 2)
                        .WithEndpointResolver(new InMemoryEndpointResolver())
                        .WithCommandsHandler(typeof(CommandsHandler))
                        .ProcessingOptions("explicitlyPrioritizedCommandsRoute").MultiThreaded(10)
                        .ProcessingOptions("prioritizedCommandsRoute").MultiThreaded(10).QueueCapacity(1024),
                    Register.Saga<TestSaga>("saga")
                        .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                        .ListeningEvents(typeof(int)).From("operations").On("operationEventsRoute"),
                    Register.DefaultRouting
                        .PublishingCommands(typeof(string)).To("operations").With("defaultCommandsRoute")
                        .PublishingCommands(typeof(int)).To("operations").With("defaultCommandsRoute"),
                    Register.DefaultEndpointResolver(
                        new RabbitMqConventionEndpointResolver("rmq", SerializationFormat.Json))
                ))
                {
                    engine.StartPublishers();
                    engine.StartSubscribers();
                }
            }
        }

        [Test]
        public void PrioritizedCommandsProcessingTest()
        {
            var endpointProvider = new Mock<IEndpointProvider>();
            endpointProvider.Setup(r => r.Get("exchange1")).Returns(new Endpoint("InMemory", "bc.exchange1", true, SerializationFormat.Json));
            endpointProvider.Setup(r => r.Get("exchange2")).Returns(new Endpoint("InMemory", "bc.exchange2", true, SerializationFormat.Json));
            var commandHandler = new CommandsHandler(false, 100);

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                    })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    endpointProvider.Object,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.BoundedContext("bc")
                        .PublishingEvents(typeof(int)).With("eventExchange")//.WithLoopback("eventQueue")
                        .ListeningCommands(typeof(string)).On("commandsRoute")
                            .Prioritized(lowestPriority: 1)
                                .WithEndpoint("exchange1").For(key => key.Priority == 1)
                                .WithEndpoint("exchange2").For(key => key.Priority == 2)
                        .ProcessingOptions("commandsRoute").MultiThreaded(2)
                        .WithCommandsHandler(commandHandler)))
                {
                    engine.StartPublishers();
                    engine.StartSubscribers();
                    messagingEngine.Send("low1", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low2", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low3", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low4", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low5", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low6", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low7", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low8", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low9", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low10", new Endpoint("InMemory", "bc.exchange2", serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("high", new Endpoint("InMemory", "bc.exchange1", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(2000);

                    Assert.True(commandHandler.HandledCommands.Take(2).Any(c => (string)c == "high"));
                }
            }
        }

        // TODO: upgrade to Moq
        //[Test]
        //public void EndpointVerificationTest()
        //{
        //    var endpointProvider = new Mock<IEndpointProvider>();
        //    endpointProvider.Setup(p => p.Contains(It.IsAny<string>())).Returns(false);

        //    var messagingEngine = new Mock<IMessagingEngine>();
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.localEvents"), Arg<ProcessingGroupInfo>.Is.Anything));
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.remoteEvents"), Arg<ProcessingGroupInfo>.Is.Anything));
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.localCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.remoteCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.SomeIntegration.sagaEvents"), Arg<ProcessingGroupInfo>.Is.Anything));
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.SomeIntegration.sagaCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.default.defaultCommands"), Arg<ProcessingGroupInfo>.Is.Anything));
        //    messagingEngine.Expect(e => e.AddProcessingGroup(Arg<string>.Is.Equal("cqrs.operations.prioritizedCommands"), Arg<ProcessingGroupInfo>.Matches(info => info.ConcurrencyLevel == 2)));
        //    string error;
        //    messagingEngine.Expect(e => e.VerifyEndpoint(new Endpoint(), EndpointUsage.None, false, out error)).IgnoreArguments().Return(true).Repeat.Times(18);
        //    //subscription for remote events
        //    messagingEngine.Expect(e => e.Subscribe(
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<CallbackDelegate<object>>.Is.Anything,
        //        Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.operations.remoteEvents"),
        //        Arg<int>.Is.Equal(0),
        //        Arg<Type[]>.List.Equal(new[] { typeof(int), typeof(long) }))).Return(Disposable.Empty);

        //    //subscription for local events
        //    messagingEngine.Expect(e => e.Subscribe(
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<CallbackDelegate<object>>.Is.Anything,
        //        Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.operations.localEvents"),
        //        Arg<int>.Is.Equal(0),
        //        Arg<Type[]>.List.Equal(new[] { typeof(bool) }))).Return(Disposable.Empty);

        //    //subscription for localCommands
        //    messagingEngine.Expect(e => e.Subscribe(
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<CallbackDelegate<object>>.Is.Anything,
        //        Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.operations.localCommands"),
        //        Arg<int>.Is.Equal(0),
        //        Arg<Type[]>.List.Equal(new[] { typeof(string), typeof(DateTime) }))).Return(Disposable.Empty);

        //    //subscription for prioritizedCommands (priority 1 and 2)
        //    messagingEngine.Expect(e => e.Subscribe(
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<CallbackDelegate<object>>.Is.Anything,
        //        Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.operations.prioritizedCommands"),
        //        Arg<int>.Is.Equal(1),
        //        Arg<Type[]>.List.Equal(new[] { typeof(byte) }))).Return(Disposable.Empty);
        //    messagingEngine.Expect(e => e.Subscribe(
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<CallbackDelegate<object>>.Is.Anything,
        //        Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.operations.prioritizedCommands"),
        //        Arg<int>.Is.Equal(2),
        //        Arg<Type[]>.List.Equal(new[] { typeof(byte) }))).Return(Disposable.Empty);

        //    //subscription for saga events
        //    messagingEngine.Expect(e => e.Subscribe(
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<CallbackDelegate<object>>.Is.Anything,
        //        Arg<Action<string, AcknowledgeDelegate>>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.SomeIntegration.sagaEvents"),
        //        Arg<int>.Is.Equal(0),
        //        Arg<Type[]>.List.Equal(new[] { typeof(int) }))).Return(Disposable.Empty);


        //    //send command to remote BC
        //    messagingEngine.Expect(e => e.Send(
        //        Arg<object>.Is.Equal("testCommand"),
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.operations.remoteCommands"),
        //        Arg<Dictionary<string, string>>.Is.Anything
        //        ));

        //    //publish event from local BC
        //    messagingEngine.Expect(e => e.Send(
        //        Arg<object>.Is.Equal(true),
        //        Arg<Endpoint>.Is.Anything,
        //        Arg<string>.Is.Equal("cqrs.operations.localEvents"),
        //        Arg<Dictionary<string, string>>.Is.Anything
        //        ));


        //    using (var ce = new CqrsEngine(messagingEngine,
        //            Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("tr1", "protobuf")),
        //            Register.BoundedContext("operations")
        //                .PublishingEvents(typeof(bool)).With("localEvents").WithLoopback()
        //                .ListeningCommands(typeof(string), typeof(DateTime)).On("localCommands").WithLoopback()
        //                .ListeningCommands(typeof(byte)).On("prioritizedCommands").Prioritized(2)
        //                .ListeningEvents(typeof(int), typeof(long)).From("integration").On("remoteEvents")
        //                .PublishingCommands(typeof(string)).To("integration").With("remoteCommands").Prioritized(5)
        //                .ProcessingOptions("prioritizedCommands").MultiThreaded(2),
        //           Register.Saga<TestSaga>("SomeIntegration")
        //                .ListeningEvents(typeof(int)).From("bc1").On("sagaEvents")
        //                .PublishingCommands(typeof(string)).To("bc2").With("sagaCommands")
        //               // .ProcessingOptions("commands").MultiThreaded(5)
        //               ,
        //               Register.DefaultRouting.PublishingCommands(typeof(string)).To("bc3").With("defaultCommands")
        //            ))
        //    {
        //        ce.Contexts.Find(bc => bc.Name == "operations").EventsPublisher.PublishEvent(true);
        //        ce.SendCommand("testCommand", "operations", "integration", 1);
        //    }

        //    messagingEngine.VerifyAllExpectations();
        //}

        [Test]
        public void ProcessTest()
        {
            var testProcess = new TestProcess();
            var commandHandler = new CommandsHandler();
            using (var engine = new InMemoryCqrsEngine(
                _logFactory,
                Register.BoundedContext("local")
                    .ListeningCommands(typeof(string)).On("commands1").WithLoopback()
                    .PublishingEvents(typeof(int)).With("events")
                    .WithCommandsHandler(commandHandler)
                    .WithProcess(testProcess)
            ))
            {
                engine.StartAll();
                Assert.True(testProcess.Started.WaitOne(1000), "process was not started");
                Thread.Sleep(1000);
            }

            Assert.True(testProcess.Disposed.WaitOne(1000), "process was not disposed on engine dispose");
            Assert.True(commandHandler.HandledCommands.Count > 0, "commands sent by process were not processed");
        }

        /*
        [Test]
        [Ignore("Does not work on tc")]
        public void EventStoreTest()
        {
            var log = MockRepository.GenerateMock<ILog>();
            var eventStoreConnection = EventStoreConnection.Create(ConnectionSettings.Default,
                                                                    new IPEndPoint(IPAddress.Loopback, 1113));
            eventStoreConnection.Connect();
            using (var engine = new InMemoryCqrsEngine(
                BoundedConfiguration.Context.BoundedContext("local")
                                    .PublishingEvents(typeof (int), typeof (TestAggregateRootNameChangedEvent),
                                                        typeof (TestAggregateRootCreatedEvent))
                                    .To("events")
                                    .RoutedTo("events")
                                    .ListeningCommands(typeof (string)).On("commands1").RoutedFromSameEndpoint()
                                    .WithCommandsHandler<CommandsHandler>()
                                    .WithProcess<TestProcess>()
                                    .WithEventStore(dispatchCommits => Wireup.Init()
                                                                            .LogTo(type => log)
                                                                            .UsingInMemoryPersistence()
                                                                            .InitializeStorageEngine()
                                                                            .UsingJsonSerialization()
                                                                            .UsingSynchronousDispatchScheduler()
                                                                            .DispatchTo(dispatchCommits))
                ))
            {
                engine.SendCommand("test", "local", "local");

                Thread.Sleep(500);
                Console.WriteLine("Disposing...");
            }
            Console.WriteLine("Dispose completed.");
        }
        */

        // TODO: upgrade to Moq
        //[Test]
        //[Ignore("integration")]
        //public void ReplayEventsRmqTest()
        //{
        //    var endpointResolver = MockRepository.GenerateMock<IEndpointResolver>();
        //    endpointResolver.Expect(r => r.Resolve(Arg<string>.Is.Equal("commands"), Arg<RoutingKey>.Matches(k => k.MessageType == typeof(string) && k.RouteType == RouteType.Commands), Arg<IEndpointProvider>.Is.Anything)).Return(new Endpoint("rmq", "commandsExchange", "commands", true, "json"));
        //    endpointResolver.Expect(r => r.Resolve(Arg<string>.Is.Equal("events"), Arg<RoutingKey>.Matches(k => k.MessageType == typeof(TestAggregateRootNameChangedEvent) && k.RouteType == RouteType.Events), Arg<IEndpointProvider>.Is.Anything)).Return(new Endpoint("rmq", "eventsExchange", "events", true, "json"));
        //    endpointResolver.Expect(r => r.Resolve(Arg<string>.Is.Equal("events"), Arg<RoutingKey>.Matches(k => k.MessageType == typeof(TestAggregateRootCreatedEvent) && k.RouteType == RouteType.Events), Arg<IEndpointProvider>.Is.Anything)).Return(new Endpoint("rmq", "eventsExchange", "events", true, "json"));

        //    var transports = new Dictionary<string, TransportInfo> { { "rmq", new TransportInfo("localhost", "guest", "guest", null, "RabbitMq") } };
        //    var messagingEngine = new MessagingEngine(new TransportResolver(transports), new RabbitMqTransportFactory());


        //    var eventsListener = new EventsListener();
        //    var localBoundedContext = Register.BoundedContext("local")
        //        .PublishingEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).With("events").WithLoopback()
        //        .ListeningCommands(typeof(string)).On("commands").WithLoopback()
        //        .ListeningInfrastructureCommands().On("commands").WithLoopback()
        //        .WithCommandsHandler<EsCommandHandler>()
        //        .WithNEventStore(dispatchCommits => Wireup.Init()
        //            .LogToOutputWindow()
        //            .UsingInMemoryPersistence()
        //            .InitializeStorageEngine()
        //            .UsingJsonSerialization()
        //            .UsingSynchronousDispatchScheduler()
        //            .DispatchTo(dispatchCommits));
        //    using (messagingEngine)
        //    {
        //        using (
        //            var engine = new CqrsEngine(messagingEngine, localBoundedContext,
        //                Register.DefaultEndpointResolver(endpointResolver),
        //                Register.BoundedContext("projections").WithProjection(eventsListener, "local")))
        //        {
        //            var guid = Guid.NewGuid();
        //            engine.SendCommand(guid + ":create", "local", "local");
        //            engine.SendCommand(guid + ":changeName:newName", "local", "local");

        //            Thread.Sleep(2000);
        //            //engine.SendCommand(new ReplayEventsCommand { Destination = "events", From = DateTime.MinValue }, "local");
        //            engine.ReplayEvents("local", "local", DateTime.MinValue, null);
        //            Thread.Sleep(2000);
        //            Console.WriteLine("Disposing...");
        //        }
        //    }


        //    Assert.That(eventsListener.Handled.Count, Is.EqualTo(4), "Events were not redelivered");

        //}

        // TODO: upgrade to Moq
        //[Test]
        //[TestCase(new Type[0], 3, TestName = "AllEvents")]
        //[TestCase(new[] { typeof(TestAggregateRootNameChangedEvent) }, 2, TestName = "FilteredEvents")]
        //public void ReplayEventsTest(Type[] types, int expectedReplayCount)
        //{
        //    var log = MockRepository.GenerateMock<ILog>();
        //    var eventsListener = new EventsListener();
        //    var localBoundedContext = Register.BoundedContext("local")
        //        .PublishingEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).With("events").WithLoopback()
        //        .ListeningCommands(typeof(string)).On("commands").WithLoopback()
        //        .ListeningInfrastructureCommands().On("commands").WithLoopback()
        //        .WithCommandsHandler<EsCommandHandler>()
        //        .WithNEventStore(dispatchCommits => Wireup.Init()
        //            .LogTo(type => log)
        //            .UsingInMemoryPersistence()
        //            .InitializeStorageEngine()
        //            .UsingJsonSerialization()
        //            .UsingSynchronousDispatchScheduler()
        //            .DispatchTo(dispatchCommits));

        //    using (
        //        var engine = new InMemoryCqrsEngine(localBoundedContext,
        //            Register.BoundedContext("projections")
        //            .ListeningEvents(typeof(TestAggregateRootNameChangedEvent), typeof(TestAggregateRootCreatedEvent)).From("local").On("events")
        //                .WithProjection(eventsListener, "local", batchSize: 10, applyTimeoutInSeconds: 1)
        //                .PublishingInfrastructureCommands().To("local").With("commands")))
        //    {
        //        var guid = Guid.NewGuid();
        //        engine.SendCommand(guid + ":create", "local", "local");
        //        engine.SendCommand(guid + ":changeName:newName", "local", "local");
        //        engine.SendCommand(guid + ":changeName:newName", "local", "local");
        //        Thread.Sleep(1000);

        //        ManualResetEvent replayFinished = new ManualResetEvent(false);
        //        engine.ReplayEvents("projections", "local", DateTime.MinValue, null, l => replayFinished.Set(), types);

        //        Assert.That(replayFinished.WaitOne(5000), Is.True, "Events were not replayed");
        //        Thread.Sleep(1000);
        //        Console.WriteLine("Disposing...");
        //    }

        //    Assert.That(eventsListener.Handled.Count, Is.EqualTo(3 + expectedReplayCount), "Wrong number of events was replayed");
        //}

        [Test, Ignore("integration")]
        public void BatchDispatchUnackRmqTest()
        {
            var handler = new EventHandlerWithBatchSupport(1);
            var endpointProvider = new Mock<IEndpointProvider>();

            using (
                var messagingEngine =
                    new MessagingEngine(
                        _logFactory,
                        new TransportResolver(new Dictionary<string, TransportInfo>
                        {
                            {"RabbitMq", new TransportInfo("amqp://localhost", "guest", "guest", null, "RabbitMq")}
                        }), new RabbitMqTransportFactory(_logFactory)))
            {
                messagingEngine.CreateTemporaryDestination("RabbitMq", null);

                var endpoint = new Endpoint("RabbitMq", "testExchange", "testQueue", true, SerializationFormat.Json);
                endpointProvider.Setup(r => r.Get("route")).Returns(endpoint);
                endpointProvider.Setup(r => r.Contains("route")).Returns(true);

                using (var engine = new CqrsEngine(
                    _logFactory,
                    new DefaultDependencyResolver(),
                    messagingEngine,
                    endpointProvider.Object,
                    false,
                    Register.BoundedContext("bc")
                        .ListeningEvents(typeof(DateTime)).From("other").On("route")
                        .WithProjection(handler, "other", 1, 0,
                            h => h.OnBatchStart(),
                            (h, c) => h.OnBatchFinish(c)
                        )))
                {
                    engine.StartSubscribers();
                    messagingEngine.Send(DateTime.Now, endpoint);
                    Thread.Sleep(20000);
                }
            }
        }

        [Test]
        public void UnhandledListenedEventsTest()
        {
            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                })))
            {
                Assert.Throws<InvalidOperationException>(
                    () => new CqrsEngine(
                        _logFactory,
                        messagingEngine,
                        Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                        Register.Saga<TestSaga>("swift-cashout")
                            .ListeningEvents(GetType()).From("lykke-wallet").On("lykke-wallet-events")),
                    "Engine must throw exception if Saga doesn't handle listened events");
            }
        }

        [Test]
        public void UnhandledListenedCommandsTest()
        {
            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                })))
            {
                Assert.Throws<InvalidOperationException>(
                    () => new CqrsEngine(
                        _logFactory,
                        messagingEngine,
                        Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                        Register.BoundedContext("swift-cashout")
                            .ListeningCommands(GetType()).On("lykke-wallet")),
                    "Engine must throw exception if command handler doesn't handle listened commands");
            }
        }
    }
}