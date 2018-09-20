using System;
using System.Collections.Generic;
using Common.Log;
using Lykke.Messaging.Contract;
using Lykke.Cqrs;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    [TestFixture]
    public class CommandDispatcherTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
        }

        [Test]
        public void WireTest()
        {
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            var handler = new Handler();
            dispatcher.Wire(handler);
            dispatcher.Dispatch("test", (delay, acknowledge) => { },new Endpoint(), "route");
            dispatcher.Dispatch(1, (delay, acknowledge) => { }, new Endpoint(), "route");
            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { "test", 1 }), "Some commands were not dispatched");
        }

        [Test]
        public void WireWithOptionalParameterTest()
        {
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            var handler = new RepoHandler();
            var int64Repo = new Int64Repo();

            dispatcher.Wire(handler, new[] { new OptionalParameter<IInt64Repo>(int64Repo) });
            dispatcher.Dispatch((Int64)1, (delay, acknowledge) => { }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { (Int64)1 }), "Some commands were not dispatched");
            Assert.IsFalse(int64Repo.IsDisposed, "Optional parameter should NOT be disposed");
        }

        [Test]
        public void WireWithFactoryOptionalParameterTest()
        {
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            var handler = new RepoHandler();
            var int64Repo = new Int64Repo();
            dispatcher.Wire(handler, new[] {new FactoryParameter<IInt64Repo>(() => int64Repo)});
            dispatcher.Dispatch((Int64)1, (delay, acknowledge) => { }, new Endpoint(), "route");
            
            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { (Int64)1 }), "Some commands were not dispatched");
            Assert.IsTrue(int64Repo.IsDisposed, "Factory parameter should be disposed");
        }

        [Test]
        public void WireWithFactoryOptionalParameterNullTest()
        {
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            var handler = new RepoHandler();
            dispatcher.Wire(handler, new[] { new FactoryParameter<IInt64Repo>(() => null) });
            dispatcher.Dispatch((Int64)1, (delay, acknowledge) => { }, new Endpoint(), "route");

            Assert.That(handler.HandledCommands, Is.EquivalentTo(new object[] { (Int64)1 }), "Some commands were not dispatched");
        }

        [Test]
        public void MultipleHandlersAreNotAllowedDispatchTest()
        {
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            var handler1 = new Handler();
            var handler2 = new Handler();

            Assert.That(() =>
            {
                dispatcher.Wire(handler1);
                dispatcher.Wire(handler2);
            }, Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void DispatchOfUnknownCommandShouldFailTest()
        {
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            var ack = true;
            dispatcher.Dispatch("testCommand",  (delay, acknowledge) => { ack = acknowledge; }, new Endpoint(), "route");
            Assert.That(ack,Is.False);
        }

        [Test]
        public void FailingCommandTest()
        {
            bool ack = true;
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            var handler = new Handler();
            dispatcher.Wire(handler);
            dispatcher.Dispatch(DateTime.Now,   (delay, acknowledge) => { ack = false; }, new Endpoint(), "route");
            Assert.That(ack,Is.False,"Failed command was not unacked");
        }

        [Test]
        public void UnknownCommandTest()
        {
            bool ack = true;
            var dispatcher = new CommandDispatcher(new LogToConsole(), "testBC");
            dispatcher.Dispatch(DateTime.Now,  (delay, acknowledge) => { ack = false; }, new Endpoint(), "route");
            Assert.That(ack,Is.False,"Failed command was not unacked");
        }
    }

    public interface IInt64Repo
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

    public class RepoHandler : Handler
    {
        public void Handle(Int64 command, IInt64Repo repo)
        {
            HandledCommands.Add(command);
        }
    }

    public class Handler
    {
        public readonly List<object> HandledCommands = new List<object>();

        public void Handle(string command)
        {
            HandledCommands.Add(command);
        }

        public void Handle(int command)
        {
            HandledCommands.Add(command);
        }

        public void Handle(DateTime command)
        {
            throw new Exception();
        }
    }
}
