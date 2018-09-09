using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
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
            var handler = new Handler();
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
            var handler = new ResultHandler();
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
            var handler = new ResultHandler(true, 500);
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
            var handler = new AsyncHandler(false);
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
            var handler = new AsyncHandler(true);
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
            var handler = new AsyncResultHandler(false);
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
            var handler = new AsyncResultHandler(true);
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
            var handler = new RepoHandler();
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
            var handler = new RepoHandler();
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
            var handler = new RepoHandler();
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
            var handler1 = new Handler();
            var handler2 = new Handler();

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
            var handler = new Handler(true);
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

    internal interface IInt64Repo
    {
    }

    internal class Int64Repo : IInt64Repo, IDisposable
    {
        public void Dispose()
        {
            IsDisposed = true;
        }

        public bool IsDisposed { get; set; }
    }

    internal class RepoHandler : Handler
    {
        [UsedImplicitly]
        public void Handle(Int64 command, IInt64Repo repo)
        {
            HandledCommands.Add(command);
        }
    }

    internal class Handler
    {
        internal readonly List<object> HandledCommands = new List<object>();
        private readonly bool _shouldThrow;

        internal Handler(bool shouldThrow = false)
        {
            _shouldThrow = shouldThrow;
        }

        [UsedImplicitly]
        internal void Handle(string command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            HandledCommands.Add(command);
        }

        [UsedImplicitly]
        internal void Handle(int command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            HandledCommands.Add(command);
        }

        [UsedImplicitly]
        internal void Handle(DateTime command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            HandledCommands.Add(command);
        }
    }

    internal class ResultHandler
    {
        private readonly bool _shouldFail;
        private readonly long _retryDelay;

        internal readonly List<object> HandledCommands = new List<object>();

        internal ResultHandler(bool shouldFail = false, long retryDelay = 600)
        {
            _shouldFail = shouldFail;
            _retryDelay = retryDelay;
        }

        [UsedImplicitly]
        internal CommandHandlingResult Handle(string command)
        {
            HandledCommands.Add(command);

            return new CommandHandlingResult{ Retry = _shouldFail, RetryDelay = _retryDelay };
        }
    }

    internal class AsyncHandler
    {
        private readonly bool _shouldThrow;

        internal readonly List<object> HandledCommands = new List<object>();

        internal AsyncHandler(bool shouldThrow)
        {
            _shouldThrow = shouldThrow;
        }

        [UsedImplicitly]
        internal async Task Handle(string command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            HandledCommands.Add(command);

            await Task.Delay(1);
        }
    }

    internal class AsyncResultHandler
    {
        private readonly bool _shouldThrow;

        internal readonly List<object> HandledCommands = new List<object>();

        internal AsyncResultHandler(bool shouldThrow)
        {
            _shouldThrow = shouldThrow;
        }

        [UsedImplicitly]
        internal async Task<CommandHandlingResult> Handle(string command)
        {
            if (_shouldThrow)
                throw new InvalidOperationException();

            HandledCommands.Add(command);

            await Task.Delay(1);

            return CommandHandlingResult.Ok();
        }
    }
}
