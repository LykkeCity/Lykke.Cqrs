using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Castle.Core.Internal;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs.Configuration;
using Lykke.Cqrs.Middleware;
using Lykke.Cqrs.Tests.HelperClasses;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Lykke.Messaging;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Serialization;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    [TestFixture]
    public class MiddlewareTests : IDisposable
    {
        private readonly ILogFactory _logFactory;

        public MiddlewareTests()
        {
            _logFactory = LogFactory.Create().AddUnbufferedConsole();
        }

        public void Dispose()
        {
            _logFactory?.Dispose();
        }

        [Test]
        public void OneSimpleEventInterceptorTest()
        {
            var simpleEventInterceptor = new EventSimpleInterceptor();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.EventInterceptors(simpleEventInterceptor),
                    Register.Saga<TestSaga>("test1")
                        .ListeningEvents(typeof(string)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.StartSubscribers();
                    messagingEngine.Send("1", new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(simpleEventInterceptor.Intercepted);
                    Assert.NotNull(simpleEventInterceptor.InterceptionTimestamp);
                    Assert.True(TestSaga.Messages.Contains("1"));
                }
            }
        }

        [Test]
        public void TwoSimpleEventInterceptorsTest()
        {
            var simpleEventInterceptorOne = new EventSimpleInterceptor();
            var simpleEventInterceptorTwo = new EventSimpleInterceptor();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.EventInterceptors(simpleEventInterceptorOne),
                    Register.EventInterceptors(simpleEventInterceptorTwo),
                    Register.Saga<TestSaga>("test2")
                        .ListeningEvents(typeof(string)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.StartSubscribers();
                    messagingEngine.Send("2", new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(simpleEventInterceptorOne.Intercepted);
                    Assert.True(simpleEventInterceptorTwo.Intercepted);
                    Assert.NotNull(simpleEventInterceptorOne.InterceptionTimestamp);
                    Assert.NotNull(simpleEventInterceptorTwo.InterceptionTimestamp);
                    Assert.True(simpleEventInterceptorOne.InterceptionTimestamp < simpleEventInterceptorTwo.InterceptionTimestamp);
                    Assert.True(TestSaga.Messages.Contains("2"));
                }
            }
        }

        [Test]
        public void OneSimpleCommandInterceptorTest()
        {
            var commandSimpleInterceptor = new CommandSimpleInterceptor();
            var commandsHandler = new CommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.CommandInterceptors(commandSimpleInterceptor),
                    Register.BoundedContext("test1")
                        .ListeningCommands(typeof(int)).On("lykke-wallet-events")
                        .WithCommandsHandler(commandsHandler)))
                {
                    engine.StartSubscribers();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(commandSimpleInterceptor.Intercepted);
                    Assert.NotNull(commandSimpleInterceptor.InterceptionTimestamp);
                    Assert.True(commandsHandler.HandledCommands.Count > 0);
                }
            }
        }

        [Test]
        public void TwoSimpleCommandInterceptorsTest()
        {
            var commandSimpleInterceptorOne = new CommandSimpleInterceptor();
            var commandSimpleInterceptorTwo = new CommandSimpleInterceptor();
            var commandsHandler = new CommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.CommandInterceptors(commandSimpleInterceptorOne, commandSimpleInterceptorTwo),
                    Register.BoundedContext("swift-cashout")
                        .ListeningCommands(typeof(int)).On("lykke-wallet-events")
                        .WithCommandsHandler(commandsHandler)))
                {
                    engine.StartSubscribers();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(commandSimpleInterceptorOne.Intercepted);
                    Assert.True(commandSimpleInterceptorTwo.Intercepted);
                    Assert.NotNull(commandSimpleInterceptorOne.InterceptionTimestamp);
                    Assert.NotNull(commandSimpleInterceptorTwo.InterceptionTimestamp);
                    Assert.True(commandSimpleInterceptorOne.InterceptionTimestamp < commandSimpleInterceptorTwo.InterceptionTimestamp);
                    Assert.True(commandsHandler.HandledCommands.Count > 0);
                }
            }
        }

        [Test]
        public void EventLoggingInterceptorTest()
        {
            int eventLoggedCount = 0;

            var eventLoggingInterceptor = new EventLoggingInterceptor(
                _logFactory,
                new Dictionary<Type, Action<ILog, object, object>>
                {
                    { typeof(string), (l, h, e) => ++eventLoggedCount }
                });

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.EventInterceptors(eventLoggingInterceptor),
                    Register.Saga<TestSaga>("test1")
                        .ListeningEvents(typeof(string)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.StartSubscribers();
                    messagingEngine.Send("1", new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(eventLoggedCount > 0, "Event was not logged");
                    Assert.True(eventLoggedCount == 1, "Event was logged more than once");
                }
            }
        }

        [Test]
        public void EventLoggingInterceptorTestForNoLogging()
        {
            var eventLoggingInterceptor = new EventLoggingInterceptor(
                _logFactory,
                new Dictionary<Type, Action<ILog, object, object>>
                {
                    { typeof(string), null }
                });

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.EventInterceptors(eventLoggingInterceptor),
                    Register.Saga<TestSaga>("test1")
                        .ListeningEvents(typeof(string)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.StartSubscribers();
                    using (var writer = new StringWriter())
                    {
                        var prevOut = Console.Out;
                        Console.SetOut(writer);
                        messagingEngine.Send("1", new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                        Thread.Sleep(1000);
                        Console.SetOut(prevOut);

                        var output = writer.ToString();
                        Assert.True(output.IsNullOrEmpty(), "Event was logged");
                    }
                }
            }
        }

        [Test]
        public void EventLoggingInterceptorTestForDefaultLogging()
        {
            var eventLoggingInterceptor = new EventLoggingInterceptor(_logFactory);

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.EventInterceptors(eventLoggingInterceptor),
                    Register.Saga<TestSaga>("test1")
                        .ListeningEvents(typeof(string)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.StartSubscribers();
                    using (var writer = new StringWriter())
                    {
                        var prevOut = Console.Out;
                        Console.SetOut(writer);
                        messagingEngine.Send("1", new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                        Thread.Sleep(1000);
                        Console.SetOut(prevOut);

                        var output = writer.ToString();
                        Assert.IsFalse(output.IsNullOrEmpty(), "Event was not logged");
                    }
                }
            }
        }

        [Test]
        public void CommandLoggingInterceptorTest()
        {
            int commandLoggedCount = 0;
            var commandLoggingInterceptor = new CommandLoggingInterceptor(
                _logFactory,
                new Dictionary<Type, Action<ILog, object, object>>
                {
                    {  typeof(int), (l, h, c) => ++commandLoggedCount }
                });
            var commandsHandler = new CommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.CommandInterceptors(commandLoggingInterceptor),
                    Register.BoundedContext("test1")
                        .ListeningCommands(typeof(int)).On("lykke-wallet-events")
                        .WithCommandsHandler(commandsHandler)))
                {
                    engine.StartSubscribers();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(commandLoggedCount > 0, "Command was not logged");
                    Assert.True(commandLoggedCount == 1, "Command was logged more than once");
                }
            }
        }

        [Test]
        public void CommandLoggingInterceptorTestForNoLogging()
        {
            var commandLoggingInterceptor = new CommandLoggingInterceptor(
                _logFactory,
                new Dictionary<Type, Action<ILog, object, object>>
                {
                    { typeof(int), null }
                });
            var commandsHandler = new CommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.CommandInterceptors(commandLoggingInterceptor),
                    Register.BoundedContext("test1")
                        .ListeningCommands(typeof(int)).On("lykke-wallet-events")
                        .WithCommandsHandler(commandsHandler)))
                {
                    engine.StartSubscribers();
                    using (var writer = new StringWriter())
                    {
                        var prevOut = Console.Out;
                        Console.SetOut(writer);
                        messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                        Thread.Sleep(1000);
                        Console.SetOut(prevOut);

                        var output = writer.ToString();
                        Assert.True(output.IsNullOrEmpty(), "Command was logged");
                    }
                }
            }
        }

        [Test]
        public void CommandLoggingInterceptorTestForDefaultLogging()
        {
            var commandLoggingInterceptor = new CommandLoggingInterceptor(_logFactory);
            var commandsHandler = new CommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _logFactory,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null)}
                })))
            {
                using (var engine = new CqrsEngine(
                    _logFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.CommandInterceptors(commandLoggingInterceptor),
                    Register.BoundedContext("test1")
                        .ListeningCommands(typeof(int)).On("lykke-wallet-events")
                        .WithCommandsHandler(commandsHandler)))
                {
                    engine.StartSubscribers();
                    using (var writer = new StringWriter())
                    {
                        var prevOut = Console.Out;
                        Console.SetOut(writer);
                        messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                        Thread.Sleep(1000);
                        Console.SetOut(prevOut);

                        var output = writer.ToString();
                        Assert.IsFalse(output.IsNullOrEmpty(), "Command was not logged");
                    }
                }
            }
        }
    }
}
