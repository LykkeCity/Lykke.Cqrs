using System;
using System.Collections.Generic;
using System.Threading;
using Lykke.Common.Log;
using Lykke.Cqrs.Configuration;
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
            TestSaga.Handled = false;

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
                    Register.Saga<TestSaga>("swift-cashout")
                        .ListeningEvents(typeof(int)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.Start();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(simpleEventInterceptor.Intercepted);
                    Assert.NotNull(simpleEventInterceptor.InterceptionTimestamp);
                    Assert.NotNull(simpleEventInterceptor.Next);
                    Assert.True(TestSaga.Handled);
                }
            }
        }

        [Test]
        public void TwoSimpleEventInterceptorsTest()
        {
            var simpleEventInterceptorOne = new EventSimpleInterceptor();
            var simpleEventInterceptorTwo = new EventSimpleInterceptor();
            TestSaga.Handled = false;

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
                    Register.Saga<TestSaga>("swift-cashout")
                        .ListeningEvents(typeof(int)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.Start();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(simpleEventInterceptorOne.Intercepted);
                    Assert.True(simpleEventInterceptorTwo.Intercepted);
                    Assert.NotNull(simpleEventInterceptorOne.InterceptionTimestamp);
                    Assert.NotNull(simpleEventInterceptorTwo.InterceptionTimestamp);
                    Assert.True(simpleEventInterceptorOne.InterceptionTimestamp < simpleEventInterceptorTwo.InterceptionTimestamp);
                    Assert.NotNull(simpleEventInterceptorOne.Next);
                    Assert.NotNull(simpleEventInterceptorTwo.Next);
                    Assert.AreNotEqual(simpleEventInterceptorOne.Next, simpleEventInterceptorTwo.Next);
                    Assert.True(TestSaga.Handled);
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
                    Register.BoundedContext("swift-cashout")
                        .ListeningCommands(typeof(int)).On("lykke-wallet-events")
                        .WithCommandsHandler(commandsHandler)))
                {
                    engine.Start();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(commandSimpleInterceptor.Intercepted);
                    Assert.NotNull(commandSimpleInterceptor.InterceptionTimestamp);
                    Assert.NotNull(commandSimpleInterceptor.Next);
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
                    engine.Start();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(commandSimpleInterceptorOne.Intercepted);
                    Assert.True(commandSimpleInterceptorTwo.Intercepted);
                    Assert.NotNull(commandSimpleInterceptorOne.InterceptionTimestamp);
                    Assert.NotNull(commandSimpleInterceptorTwo.InterceptionTimestamp);
                    Assert.True(commandSimpleInterceptorOne.InterceptionTimestamp < commandSimpleInterceptorTwo.InterceptionTimestamp);
                    Assert.NotNull(commandSimpleInterceptorOne.Next);
                    Assert.NotNull(commandSimpleInterceptorTwo.Next);
                    Assert.AreNotEqual(commandSimpleInterceptorOne.Next, commandSimpleInterceptorTwo.Next);
                    Assert.True(commandsHandler.HandledCommands.Count > 0);
                }
            }
        }
    }
}
