using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Castle.Facilities.Startable;
using Castle.MicroKernel.Handlers;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Lykke.Common.Log;
using Lykke.Cqrs.Castle;
using Lykke.Messaging;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Cqrs.Configuration;
using Lykke.Cqrs.InfrastructureCommands;
using Lykke.Cqrs.Routing;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Moq;
using NUnit.Framework;
using ProtoBuf;

namespace Lykke.Cqrs.Tests
{
    internal class CommandsHandler
    {
        public readonly List<object> HandledCommands = new List<object>();

        public CommandsHandler()
        {
            Console.WriteLine();
        }

        private void Handle(string m)
        {
            Console.WriteLine("Command received:" + m);
            HandledCommands.Add(m);
        }

        private CommandHandlingResult Handle(int m)
        {
            Console.WriteLine("Command received:" + m);
            HandledCommands.Add(m);
            return new CommandHandlingResult { Retry = true, RetryDelay = 100 };
        }

        private CommandHandlingResult Handle(RoutedCommand<DateTime> m)
        {
            Console.WriteLine("Command received:" + m.Command + " Origination Endpoint:" + m.OriginEndpoint);
            HandledCommands.Add(m);
            return new CommandHandlingResult { Retry = true, RetryDelay = 100 };
        }

        private void Handle(long m)
        {
            Console.WriteLine("Command received:" + m);
            throw new Exception();
        }
    }

    internal class EventListenerWithBatchSupport  
    {
        public readonly List<FakeDbSession> Sessions = new List<FakeDbSession>();
        public readonly List<string> Events = new List<string>();

        void Handle(string m, FakeDbSession session)
        {
            Events.Add(m);
            if (session != null)
                session.ApplyEvent(m);
            Console.WriteLine(m);
        }

        public FakeDbSession CreateDbSession()
        {
            var session = new FakeDbSession();
            Sessions.Add(session);
            return session;
        }
    }

    internal class EventListener
    {
        public readonly List<Tuple<string, string>> EventsWithBoundedContext = new List<Tuple<string, string>>();
        public readonly List<string> Events = new List<string>();

        void Handle(string m, string boundedContext)
        {
            EventsWithBoundedContext.Add(Tuple.Create(m, boundedContext));
            Console.WriteLine(boundedContext + ":" + m);
        }
    }

    internal class FakeDbSession
    {
        public bool Commited { get; set; }
        public List<string> Events { get; set; }

        public FakeDbSession()
        {
            Events=new List<string>();
        }

        public void Commit()
        {
            Commited = true;
        }

        public void ApplyEvent(string @event)
        {
            Events.Add(@event);
        }
    }

    class CqrEngineDependentComponent
    {
        public static bool Started { get; set; }
        public CqrEngineDependentComponent(ICqrsEngine engine)
        {
        }
        public void Start()
        {
            Started = true;
        }
    }

    [TestFixture]
    public class CqrsFacilityTests
    {
        private readonly ILogFactory _logFactory;

        public CqrsFacilityTests()
        {
            _logFactory = LogFactory.Create().AddUnbufferedConsole();
        }

        public void Dispose()
        {
            _logFactory?.Dispose();
        }

        [Test]
        public void ComponentCanNotBeProjectionAndCommandsHandlerSimultaneousely()
        {
            using (var container = new WindsorContainer())
            {
                container.AddFacility<CqrsFacility>(f => f.RunInMemory().Contexts(Register.BoundedContext("bc")));

                Assert.That(() => 
                container.Register(Component.For<CommandsHandler>().AsCommandsHandler("bc").AsProjection("bc", "remote")), Throws.TypeOf<InvalidOperationException>());
            }
        }

        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterBootstrapTest()
        {
            bool reslovedCqrsDependentComponentBeforeInit = false;
            using (var container = new WindsorContainer())
            {
                container.Register(Component.For<CqrEngineDependentComponent>());
                container.AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory());
                try
                {
                    container.Resolve<CqrEngineDependentComponent>();
                    reslovedCqrsDependentComponentBeforeInit = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                container.Resolve<ICqrsEngineBootstrapper>().Start();

                container.Resolve<CqrEngineDependentComponent>();
                Assert.That(reslovedCqrsDependentComponentBeforeInit, Is.False, "ICqrsEngine was resolved as dependency before it was initialized");
            }
        }

        [Test]
        public void CqrsEngineIsResolvableAsDependencyOnlyAfterBootstrapStartableFacilityTest()
        {
            using (var container = new WindsorContainer())
            {
                container.AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory())
                    .AddFacility<StartableFacility>(); // (f => f.DeferredTryStart());
                container.Register(Component.For<IMessagingEngine>().Instance(new Mock<IMessagingEngine>().Object));
                container.Register(Component.For<CqrEngineDependentComponent>().StartUsingMethod("Start"));
                Assert.That(CqrEngineDependentComponent.Started, Is.False, "Component was started before commandSender initialization");
                container.Resolve<ICqrsEngineBootstrapper>().Start();
                Assert.That(CqrEngineDependentComponent.Started, Is.True, "Component was not started after commandSender initialization");
            }
        }

        [Test]
        public void ProjectionWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container.Register(Component.For<IMessagingEngine>().Instance(new Mock<IMessagingEngine>().Object))
                    .AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory().Contexts(
                            Register.BoundedContext("local").ListeningEvents(typeof(string)).From("remote").On("remoteEVents")
                            ))
                    .Register(Component.For<EventListener>().AsProjection("local", "remote"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();

                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var eventListener = container.Resolve<EventListener>();
                cqrsEngine.Contexts.First(c => c.Name == "local").EventDispatcher.Dispatch("remote", "test", (delay, acknowledge) => { });
                Assert.That(eventListener.EventsWithBoundedContext, Is.EquivalentTo(new[] { Tuple.Create("test", "remote") }), "Event was not dispatched");
            }
        }

        [Test]
        public void ProjectionWiringBatchTest()
        {
            using (var container = new WindsorContainer())
            {
                container.Register(Component.For<IMessagingEngine>().Instance(new Mock<IMessagingEngine>().Object))
                    .AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory().Contexts(
                            Register.BoundedContext("local").ListeningEvents(typeof(string)).From("remote").On("remoteEVents")
                            ))
                    .Register(Component.For<EventListenerWithBatchSupport>()
                    .AsProjection("local", "remote",
                            batchSize: 2,
                            applyTimeoutInSeconds: 0,
                            beforeBatchApply: listener => listener.CreateDbSession(),
                            afterBatchApply: (listener, sbSession) => sbSession.Commit()))
                    .Resolve<ICqrsEngineBootstrapper>().Start();

                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var eventListener = container.Resolve<EventListenerWithBatchSupport>();
                var eventDispatcher = cqrsEngine.Contexts.First(c => c.Name == "local").EventDispatcher;
                eventDispatcher.Dispatch("remote", "event1", (delay, acknowledge) => { });
                eventDispatcher.Dispatch("remote", "event2", (delay, acknowledge) => { });
                eventDispatcher.Dispatch("remote", "event3", (delay, acknowledge) => { });
                eventDispatcher.Dispatch("remote", "event4", (delay, acknowledge) => { });

                Assert.That(eventListener.Events, Is.EquivalentTo(new[] { "event1", "event2", "event3", "event4" }), "Event was not dispatched");
                Assert.That(eventListener.Sessions.Any(), Is.True, "Batch start callback was not called");
                Assert.That(eventListener.Sessions.Count, Is.EqualTo(2), "Event were not dispatched in batches");
                Assert.That(eventListener.Sessions[0].Events, Is.EquivalentTo(new[] { "event1", "event2" }), "Wrong events in batch");
                Assert.That(eventListener.Sessions[1].Events, Is.EquivalentTo(new[] { "event3", "event4" }), "Wrong events in batch");
                Assert.That(eventListener.Sessions[0].Commited, Is.True, "Batch applied callback was not called");
                Assert.That(eventListener.Sessions[1].Commited, Is.True, "Batch applied callback was not called");
            }
        }

        [Test]
        public void CommandsHandlerWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(new Mock<IMessagingEngine>().Object))
                    .AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory().Contexts(Register.BoundedContext("bc")))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc").LifestyleSingleton())
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch("test", (delay, acknowledge) => { }, new Endpoint(), "route");
                Thread.Sleep(1300);
                Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { "test" }), "Command was not dispatched");
            }
        }

        [Test]
        public void DependencyOnICommandSenderTest()
        {
            using (var container = new WindsorContainer())
            {
                var messagingEngine = new Mock<IMessagingEngine>().Object;
                var bootstrapper = container
                    .Register(Component.For<IMessagingEngine>().Instance(messagingEngine))
                    .AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory().Contexts(
                        Register.BoundedContext("bc").ListeningCommands(typeof(string)).On("cmd").WithLoopback(),
                        Register.DefaultRouting.PublishingCommands(typeof(string)).To("bc").With("cmd"))
                    )
                    .Register(Component.For<CommandSenderDependentComponent>())
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>();
                HandlerException exception = null;
                try
                {
                    container.Resolve<CommandSenderDependentComponent>();
                }
                catch (HandlerException e)
                {
                    exception = e;
                }
                Assert.That(exception, Is.Not.Null, "Component with ICommandSender dependency is resolvable before cqrs engine is bootstrapped");
                Assert.That(exception.Message.Contains("Service 'Lykke.Cqrs.ICommandSender' which was not registered"), Is.True, "Component with ICommandSender dependency is resolvable before cqrs engine is bootstrapped");
                bootstrapper.Start();
                var component = container.Resolve<CommandSenderDependentComponent>();
                component.CommandSender.SendCommand("test", "bc");
                var commandsHandler = container.Resolve<CommandsHandler>();
                Thread.Sleep(200);
                Assert.That(commandsHandler.HandledCommands.Select(o => o.ToString()).ToArray, Is.EqualTo(new[] { "test" }), "Command was not dispatched");
            }
        }

        [Test]
        public void CommandsHandlerWithResultWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(new Mock<IMessagingEngine>().Object))
                    .AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory().Contexts(Register.BoundedContext("bc")))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();

                bool acknowledged = false;
                long retrydelay = 0;
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch(1, (delay, acknowledge) =>
                {
                    retrydelay = delay;
                    acknowledged = acknowledge;
                }, new Endpoint(), "route");
                Thread.Sleep(200);
                Assert.That(commandsHandler.HandledCommands, Is.EqualTo(new[] { 1 }), "Command was not dispatched");
                Assert.That(retrydelay, Is.EqualTo(100));
                Assert.That(acknowledged, Is.EqualTo(false));
            }
        }

        [Test]
        public void CommandsHandlerWithResultAndCommandOriginEndpointWiringTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(new Mock<IMessagingEngine>().Object))
                    .AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory().Contexts(Register.BoundedContext("bc")))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();

                bool acknowledged = false;
                long retrydelay = 0;
                var endpoint = new Endpoint();
                var command = DateTime.Now;
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch(command, (delay, acknowledge) =>
                {
                    retrydelay = delay;
                    acknowledged = acknowledge;
                }, endpoint, "route");
                Thread.Sleep(200);
                Assert.That(commandsHandler.HandledCommands.Count, Is.EqualTo(1), "Command was not dispatched");
                Assert.That(commandsHandler.HandledCommands[0], Is.TypeOf<RoutedCommand<DateTime>>(), "Command was not dispatched with wrong type");
                Assert.That(((RoutedCommand<DateTime>)(commandsHandler.HandledCommands[0])).Command, Is.EqualTo(command), "Routed command was not dispatched with wrong command");
                Assert.That(((RoutedCommand<DateTime>)(commandsHandler.HandledCommands[0])).OriginEndpoint, Is.EqualTo(endpoint), "Routed command was dispatched with wrong origin endpoint");
                Assert.That(((RoutedCommand<DateTime>)(commandsHandler.HandledCommands[0])).OriginRoute, Is.EqualTo("route"), "Routed command was dispatched with wrong origin route");
                Assert.That(retrydelay, Is.EqualTo(100));
                Assert.That(acknowledged, Is.EqualTo(false));
            }
        }

        [Test]
        public void FailedCommandHandlerCausesRetryTest()
        {
            using (var container = new WindsorContainer())
            {
                container
                    .Register(Component.For<IMessagingEngine>().Instance(new Mock<IMessagingEngine>().Object))
                    .AddFacility<CqrsFacility>(f => f.SetLogFactory(_logFactory).RunInMemory().Contexts(Register.BoundedContext("bc").FailedCommandRetryDelay(100)))
                    .Register(Component.For<CommandsHandler>().AsCommandsHandler("bc"))
                    .Resolve<ICqrsEngineBootstrapper>().Start();
                var cqrsEngine = (CqrsEngine)container.Resolve<ICqrsEngine>();
                var commandsHandler = container.Resolve<CommandsHandler>();

                bool acknowledged = false;
                long retrydelay = 0;
                cqrsEngine.Contexts.First(c => c.Name == "bc").CommandDispatcher.Dispatch((long)1, (delay, acknowledge) =>
                {
                    retrydelay = delay;
                    acknowledged = acknowledge;
                }, new Endpoint(), "route");
                Thread.Sleep(200);
                Assert.That(retrydelay, Is.EqualTo(100));
                Assert.That(acknowledged, Is.EqualTo(false));
            }
        }

        // todo: test is temporarily disabled due to unwanted dependency of Lykke.Messaging.Castle
        //[Test]
        //public async Task SagaTest()
        //{
        //    using (var container = new WindsorContainer())
        //    {
        //        container.AddFacility<MessagingFacility>(f => f.WithTransport("rmq", new TransportInfo("amqp://localhost/LKK", "guest", "guest", "None", "RabbitMq")).WithTransportFactory<RabbitMqTransportFactory>());

        //        container.AddFacility<CqrsFacility>(f => f.CreateMissingEndpoints().Contexts(
        //            Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver("rmq", "json", environment: "dev")),
        //            Register.BoundedContext("operations")
        //                .PublishingCommands(typeof(CreateCashOutCommand)).To("lykke-wallet").With("operations-commands")
        //                .ListeningEvents(typeof(CashOutCreatedEvent)).From("lykke-wallet").On("lykke-wallet-events"),

        //            Register.BoundedContext("lykke-wallet")
        //                .FailedCommandRetryDelay((long)TimeSpan.FromSeconds(2).TotalMilliseconds)
        //                .ListeningCommands(typeof(CreateCashOutCommand)).On("operations-commands")
        //                .PublishingEvents(typeof(CashOutCreatedEvent)).With("lykke-wallet-events")
        //                .WithCommandsHandler<CommandHandler>(),

        //            Register.Saga<TestSaga>("swift-cashout")
        //                .ListeningEvents(typeof(CashOutCreatedEvent)).From("lykke-wallet").On("lykke-wallet-events"),

        //            Register.DefaultRouting.PublishingCommands(typeof(CreateCashOutCommand)).To("lykke-wallet").With("operations-commands")
        //           ));

        //        container.Register(
        //            Component.For<CommandHandler>(),
        //            Component.For<TestSaga>()
        //            );

        //        container.Resolve<ICqrsEngineBootstrapper>().Start();

        //        var commandSender = container.Resolve<ICommandSender>();

        //        commandSender.SendCommand(new CreateCashOutCommand { Payload = "test data" }, "lykke-wallet");

        //        await Task.Delay(TimeSpan.FromSeconds(10));

        //        Assert.That(TestSaga.Complete.WaitOne(1000), Is.True, "Saga has not got events or failed to send command");
        //    }
        //}
    }

    [ProtoContract]
    public class CashOutCreatedEvent
    {
    }

    [ProtoContract]
    public class CreateCashOutCommand
    {
        [ProtoMember(1)]
        public string Payload { get; set; }
    }

    public class TestSaga
    {
        public static List<string> Messages = new List<string>();
        public static ManualResetEvent Complete = new ManualResetEvent(false);
        private void Handle(CashOutCreatedEvent @event, ICommandSender sender, string boundedContext)
        {
            var message = string.Format("Event from {0} is caught by saga:{1}", boundedContext, @event);
            Messages.Add(message);

            Complete.Set();

            Console.WriteLine(message);
        }
    }

    public class CommandSenderDependentComponent
    {
        public ICommandSender CommandSender { get; private set; }

        public CommandSenderDependentComponent(ICommandSender commandSender)
        {
            CommandSender = commandSender;
        }
    }
}