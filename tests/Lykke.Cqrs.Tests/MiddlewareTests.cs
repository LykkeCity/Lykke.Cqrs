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
                    Assert.NotNull(simpleEventInterceptor.Next);
                    Assert.AreNotEqual(simpleEventInterceptor.Next, simpleEventInterceptor);
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
                    Register.Saga<TestSaga>("swift-cashout")
                        .ListeningEvents(typeof(int)).From("lykke-wallet").On("lykke-wallet-events")))
                {
                    engine.Start();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(simpleEventInterceptorOne.Intercepted);
                    Assert.True(simpleEventInterceptorTwo.Intercepted);
                    Assert.AreEqual(simpleEventInterceptorOne.Next, simpleEventInterceptorTwo);
                    Assert.NotNull(simpleEventInterceptorTwo.Next);
                    Assert.AreNotEqual(simpleEventInterceptorTwo.Next, simpleEventInterceptorOne);
                    Assert.AreNotEqual(simpleEventInterceptorTwo.Next, simpleEventInterceptorTwo);
                }
            }
        }

        [Test]
        public void OneSimpleCommandInterceptorTest()
        {
            var commandSimpleInterceptor = new CommandSimpleInterceptor();

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
                        .WithCommandsHandler<CommandsHandler>()))
                {
                    engine.Start();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(commandSimpleInterceptor.Intercepted);
                    Assert.NotNull(commandSimpleInterceptor.Next);
                    Assert.AreNotEqual(commandSimpleInterceptor.Next, commandSimpleInterceptor);
                }
            }
        }

        [Test]
        public void TwoSimpleCommandInterceptorsTest()
        {
            var commandSimpleInterceptorOne = new CommandSimpleInterceptor();
            var commandSimpleInterceptorTwo = new CommandSimpleInterceptor();

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
                        .WithCommandsHandler<CommandsHandler>()))
                {
                    engine.Start();
                    messagingEngine.Send(1, new Endpoint("InMemory", "lykke-wallet-events", serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(1000);

                    Assert.True(commandSimpleInterceptorOne.Intercepted);
                    Assert.True(commandSimpleInterceptorTwo.Intercepted);
                    Assert.AreEqual(commandSimpleInterceptorOne.Next, commandSimpleInterceptorTwo);
                    Assert.NotNull(commandSimpleInterceptorTwo.Next);
                    Assert.AreNotEqual(commandSimpleInterceptorTwo.Next, commandSimpleInterceptorOne);
                    Assert.AreNotEqual(commandSimpleInterceptorTwo.Next, commandSimpleInterceptorTwo);
                }
            }
        }
    }
}
