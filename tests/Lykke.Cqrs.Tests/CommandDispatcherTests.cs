using System;
using Lykke.Common.Log;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Lykke.Messaging.Contract;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    [TestFixture]
    public class CommandDispatcherTests : IDisposable
    {
        private readonly ILogFactory _logFactory;

        public CommandDispatcherTests()
        {
            _logFactory = LogFactory.Create().AddUnbufferedConsole();
        }

        public void Dispose()
        {
            _logFactory?.Dispose();
        }

        [OneTimeSetUp]
        public void Setup()
        {
        }

        [Test]
        public void HandleTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsHandler();
            var now = DateTime.UtcNow;
            bool ack1 = false;
            bool ack2 = false;
            bool ack3 = false;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { ack1 = acknowledge; },new Endpoint(), "route");
            dispatcher.Dispatch(1, (delay, acknowledge) => { ack2 = acknowledge; }, new Endpoint(), "route");
            dispatcher.Dispatch(now, (delay, acknowledge) => { ack3 = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { "test", 1, now }), "Some commands were not dispatched");
            Assert.True(ack1, "String command was not acked");
            Assert.True(ack2, "Int command was not acked");
            Assert.True(ack3, "DateTime command was not acked");
        }

        [Test]
        public void HandleOkResultTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsResultHandler();
            bool ack = false;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { "test" }), "Some commands were not dispatched");
            Assert.True(ack, "Command was not acked");
        }

        [Test]
        public void HandleFailResultTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsResultHandler(true, 500);
            bool ack = false;
            long retryDelay = 0;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { retryDelay = delay; ack = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { "test" }), "Some commands were not dispatched");
            Assert.False(ack, "Command was not acked");
            Assert.AreEqual(500, retryDelay);
        }

        [Test]
        public void HandleAsyncTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsAsyncHandler(false);
            bool ack = false;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { "test" }), "Some commands were not dispatched");
            Assert.True(ack, "Command was not acked");
        }

        [Test]
        public void ExceptionOnHandleAsyncTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsAsyncHandler(true);
            bool ack = false;
            long retryDelay = 0;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { retryDelay = delay; ack = acknowledge; }, new Endpoint(), "route");

            Assert.AreEqual(0, handler.HandledCommands.Count);
            Assert.False(ack, "Command was not acked");
            Assert.AreEqual(CommandDispatcher.FailedCommandRetryDelay, retryDelay);
        }

        [Test]
        public void HandleAsyncResultTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsAsyncResultHandler(false);
            bool ack = false;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { "test" }), "Some commands were not dispatched");
            Assert.True(ack, "Command was not acked");
        }

        [Test]
        public void ExceptionOnHandleAsyncResultTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsAsyncResultHandler(true);
            bool ack = false;
            long retryDelay = 0;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { retryDelay = delay; ack = acknowledge; }, new Endpoint(), "route");

            Assert.AreEqual(0, handler.HandledCommands.Count);
            Assert.False(ack, "Command was not acked");
            Assert.AreEqual(CommandDispatcher.FailedCommandRetryDelay, retryDelay);
        }

        [Test]
        public void WireWithOptionalParameterTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandRepoHandler();
            var int64Repo = new Int64Repo();
            bool ack = false;

            dispatcher.Wire(handler, new OptionalParameter<IInt64Repo>(int64Repo));
            dispatcher.Dispatch((Int64)1, (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { (Int64)1 }), "Some commands were not dispatched");
            Assert.IsFalse(int64Repo.IsDisposed, "Optional parameter should NOT be disposed");
            Assert.True(ack, "Command was not acked");
        }

        [Test]
        public void WireWithFactoryOptionalParameterTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandRepoHandler();
            var int64Repo = new Int64Repo();
            bool ack = false;

            dispatcher.Wire(handler, new FactoryParameter<IInt64Repo>(() => int64Repo));
            dispatcher.Dispatch((Int64)1, (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { (Int64)1 }), "Some commands were not dispatched");
            Assert.IsTrue(int64Repo.IsDisposed, "Factory parameter should be disposed");
            Assert.True(ack, "Command was not acked");
        }

        [Test]
        public void WireWithFactoryOptionalParameterNullTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandRepoHandler();
            bool ack = false;

            dispatcher.Wire(handler, new FactoryParameter<IInt64Repo>(() => null));
            dispatcher.Dispatch((Int64)1, (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { (Int64)1 }), "Some commands were not dispatched");
            Assert.True(ack, "Command was not acked");
        }

        [Test]
        public void MultipleHandlersAreNotAllowedDispatchTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler1 = new CommandsHandler();
            var handler2 = new CommandsHandler();

            Assert.Throws<InvalidOperationException>(() =>
            {
                dispatcher.Wire(handler1);
                dispatcher.Wire(handler2);
            });
        }

        [Test]
        public void FailingCommandTest()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var handler = new CommandsHandler(true);
            bool ack = true;

            dispatcher.Wire(handler);
            dispatcher.Dispatch("testCommand", (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.False(ack,"Failed command was not unacked");
        }

        [Test]
        public void NoHandlerCommandMustBeUnacked()
        {
            var dispatcher = new CommandDispatcher(_logFactory, "testBC");
            var ack = true;

            dispatcher.Dispatch("testCommand", (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");

            Assert.False(ack, "Command with no handler was acked");
        }
    }
}
