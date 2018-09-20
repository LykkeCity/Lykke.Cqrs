using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Log;
using Lykke.Messaging;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Cqrs.Configuration;
using Moq;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    class CommandHandler
    {
        public List<object> AcceptedCommands = new List<object>();
        private int m_ProcessingTimeout;

        public CommandHandler(int processingTimeout)
        {
            m_ProcessingTimeout = processingTimeout;
        }
        public CommandHandler()
            : this(0)
        {
        }

        public void Handle(CreateCashOutCommand command, IEventPublisher eventPublisher)
        {
            Thread.Sleep(m_ProcessingTimeout);

            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " command recived:" + command.Payload);

            eventPublisher.PublishEvent(new CashOutCreatedEvent());
        }
    }

    [TestFixture]
    public class CqrsEngineTests
    {
        [Test]
        public void ListenSameCommandOnDifferentEndpointsTest()
        {
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new LogToConsole(),
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                var commandHandler = new CommandHandler();
                using (var engine = new CqrsEngine(new LogToConsole(), messagingEngine,
                                                   Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                                                   Register.BoundedContext("bc")
                                                                      .PublishingEvents(typeof(int)).With("eventExchange")
                                                                      .ListeningCommands(typeof(string)).On("exchange1")
                                                                      .ListeningCommands(typeof(string)).On("exchange2")
                                                                      .WithCommandsHandler(commandHandler))
                    )
                {
                    messagingEngine.Send("test1", new Endpoint("InMemory", "exchange1", serializationFormat: "json"));
                    messagingEngine.Send("test2", new Endpoint("InMemory", "exchange2", serializationFormat: "json"));
                    messagingEngine.Send("test3", new Endpoint("InMemory", "exchange3", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Assert.That(commandHandler.AcceptedCommands, Is.EquivalentTo(new[] { "test1", "test2" }));
                }
            }
        }

        [Test]
        public void ContextUsesDefaultRouteForCommandPublishingIfItDoesNotHaveItsOwnTest()
        {
            var bcCommands = new Endpoint("InMemory", "bcCommands", serializationFormat: "json");
            var defaultCommands = new Endpoint("InMemory", "defaultCommands", serializationFormat: "json");
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new LogToConsole(),
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                using (var engine = new CqrsEngine(new LogToConsole(), messagingEngine,
                                                                      Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                                                                      Register.BoundedContext("bc2")
                                                                        .PublishingCommands(typeof(int)).To("bc1").With("bcCommands"),
                                                                      Register.DefaultRouting
                                                                        .PublishingCommands(typeof(string)).To("bc1").With("defaultCommands")
                                                                        .PublishingCommands(typeof(int)).To("bc1").With("defaultCommands")
                                                                      )
                    )
                {
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

        [Test]
        public void SagaTest()
        {
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new LogToConsole(),
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"rmq", new TransportInfo("amqp://localhost/LKK", "guest", "guest", "None", "RabbitMq")}
                            }), new RabbitMqTransportFactory()))
            {
                var commandHandler = new CommandHandler();
                using (var engine = new CqrsEngine(new LogToConsole(), messagingEngine,
                    Register.DefaultEndpointResolver(
                        new RabbitMqConventionEndpointResolver("rmq", "json", environment: "dev")),
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
                    engine.SendCommand(new CreateCashOutCommand { Payload = "test data" }, null, "lykke-wallet");

                    Assert.That(TestSaga.Complete.WaitOne(2000), Is.True,
                        "Saga has not got events or failed to send command");
                }
            }
        }

        [Test]
        public void FluentApiTest()
        {
            var endpointProvider = new Mock<IEndpointProvider>();
            endpointProvider.Setup(r => r.Get("high")).Returns(new Endpoint("InMemory", "high", true, "json"));
            endpointProvider.Setup(r => r.Get("low")).Returns(new Endpoint("InMemory", "low", true, "json"));
            endpointProvider.Setup(r => r.Get("medium")).Returns(new Endpoint("InMemory", "medium", true, "json"));

            var messagingEngine =
                new MessagingEngine(
                    new LogToConsole(),
                    new TransportResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")},
                        {"rmq", new TransportInfo("none", "none", "none", null, "InMemory")}
                    }));
            using (messagingEngine)
            {


                new CqrsEngine(new LogToConsole(), messagingEngine, endpointProvider.Object,
                    Register.BoundedContext("bc")
                        .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                        .ListeningEvents(typeof(int)).From("operations").On("operationEventsRoute")
                        .ListeningCommands(typeof(string)).On("commandsRoute")
                            //same as .PublishingCommands(typeof(string)).To("bc").With("selfCommandsRoute")  
                            .WithLoopback("selfCommandsRoute")
                        .PublishingEvents(typeof(int)).With("eventsRoute")
                            //same as.ListeningEvents(typeof(int)).From("bc").On("selfEventsRoute")
                            .WithLoopback("selfEventsRoute")



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
                        .ProcessingOptions("explicitlyPrioritizedCommandsRoute").MultiThreaded(10)
                        .ProcessingOptions("prioritizedCommandsRoute").MultiThreaded(10).QueueCapacity(1024),
                    Register.Saga<TestSaga>("saga")
                        .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                        .ListeningEvents(typeof(int)).From("operations").On("operationEventsRoute"),
                    Register.DefaultRouting
                        .PublishingCommands(typeof(string)).To("operations").With("defaultCommandsRoute")
                        .PublishingCommands(typeof(int)).To("operations").With("defaultCommandsRoute"),
                    Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("rmq", "json"))
                   );
            }
        }

        [Test]
        public void PrioritizedCommandsProcessingTest()
        {
            var endpointProvider = new Mock<IEndpointProvider>();
            endpointProvider.Setup(r => r.Get("exchange1")).Returns(new Endpoint("InMemory", "bc.exchange1", true, "json"));
            endpointProvider.Setup(r => r.Get("exchange2")).Returns(new Endpoint("InMemory", "bc.exchange2", true, "json"));
            using (
                var messagingEngine =
                    new MessagingEngine(
                        new LogToConsole(),
                        new TransportResolver(new Dictionary<string, TransportInfo>
                            {
                                {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                            })))
            {
                var commandHandler = new CommandHandler(100);
                using (var engine = new CqrsEngine(new LogToConsole(), messagingEngine, endpointProvider.Object,
                                                   Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                                                   Register.BoundedContext("bc")
                                                    .PublishingEvents(typeof(int)).With("eventExchange").WithLoopback("eventQueue")
                                                    .ListeningCommands(typeof(string)).On("commandsRoute")
                                                        .Prioritized(lowestPriority: 1)
                                                            .WithEndpoint("exchange1").For(key => key.Priority == 1)
                                                            .WithEndpoint("exchange2").For(key => key.Priority == 2)
                                                    .ProcessingOptions("commandsRoute").MultiThreaded(2)
                                                    .WithCommandsHandler(commandHandler))
                    )
                {
                    messagingEngine.Send("low1", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low2", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low3", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low4", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low5", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low6", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low7", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low8", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low9", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("low10", new Endpoint("InMemory", "bc.exchange2", serializationFormat: "json"));
                    messagingEngine.Send("high", new Endpoint("InMemory", "bc.exchange1", serializationFormat: "json"));
                    Thread.Sleep(2000);
                    Console.WriteLine(string.Join("\n", commandHandler.AcceptedCommands));
                    Assert.That(commandHandler.AcceptedCommands.Take(2).Any(c => (string)c == "high"), Is.True);
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
            var commandHandler = new CommandHandler();
            using (new InMemoryCqrsEngine(
             Register.BoundedContext("local")
                                .ListeningCommands(typeof(string)).On("commands1").WithLoopback()
                                .PublishingEvents(typeof(int)).With("events").WithLoopback()
                                .WithCommandsHandler(commandHandler)
                                .WithProcess(testProcess)
             ))
            {
                Assert.That(testProcess.Started.WaitOne(1000), Is.True, "process was not started");
                Thread.Sleep(1000);
                Console.WriteLine("Disposing...");
            }
            Assert.That(testProcess.Disposed.WaitOne(1000), Is.True, "process was not disposed on engine dispose");
            Assert.That(commandHandler.AcceptedCommands.Count, Is.GreaterThan(0), "commands sent by process were not processed");
            Console.WriteLine("Dispose completed.");
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
                                    .WithCommandsHandler<CommandHandler>()
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
    }

    public class TestProcess : IProcess
    {
        public ManualResetEvent Started = new ManualResetEvent(false);
        public ManualResetEvent Disposed = new ManualResetEvent(false);
        Thread workerThread;
        private ICommandSender m_CommandSender;

        public TestProcess()
        {
            workerThread = new Thread(SendCommands);
        }

        private void SendCommands(object obj)
        {

            int i = 0;
            do
            {
                m_CommandSender.SendCommand("command #" + i++, "local");
            } while (!Disposed.WaitOne(100));

        }

        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
        {
            m_CommandSender = commandSender;
            workerThread.Start();
            Console.WriteLine("Test process started");
            Started.Set();
        }

        public void Dispose()
        {
            Console.WriteLine("Test process disposed");
            Disposed.Set();
        }
    }

    public class TestProcess1 : IProcess
    {
        private readonly ManualResetEvent m_Disposed = new ManualResetEvent(false);
        readonly Thread m_WorkerThread;
        private ICommandSender m_CommandSender;

        public TestProcess1()
        {
            m_WorkerThread = new Thread(SendCommands);
        }

        private void SendCommands(object obj)
        {

            int i = 0;
            while (!m_Disposed.WaitOne(100))
            {
                m_CommandSender.SendCommand("command #" + i++, "local");
            }
        }

        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
        {
            m_CommandSender = commandSender;
            m_WorkerThread.Start();
        }

        public void Dispose()
        {
            m_Disposed.Set();
            m_WorkerThread.Join();
        }
    }
}