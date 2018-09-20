using System;
using System.Linq;
using System.Threading;
using Lykke.Common.Log;
using Lykke.Messaging.Contract;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    [TestFixture]
    public class EventDispatcherTests : IDisposable
    {
        private readonly ILogFactory _logFactory;

        public EventDispatcherTests()
        {
            _logFactory = LogFactory.Create().AddUnbufferedConsole();
        }

        public void Dispose()
        {
            _logFactory?.Dispose();
        }

        [Test]
        public void WireTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandler();
            var now = DateTime.UtcNow;
            bool ack1 = false;
            bool ack2 = false;
            bool ack3 = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack1 = acknowledge; });
            dispatcher.Dispatch("testBC", 1, (delay, acknowledge) => { ack2 = acknowledge; });
            dispatcher.Dispatch("testBC", now, (delay, acknowledge) => { ack3 = acknowledge; });

            Assert.That(handler.HandledEvents, Is.EquivalentTo(new object[] { "test", 1, now}), "Some events were not dispatched");
            Assert.True(ack1, "Handled string command was not acknowledged");
            Assert.True(ack2, "Handled int command was not acknowledged");
            Assert.True(ack3, "Handled datetime command was not acknowledged");
        }

        [Test]
        public void MultipleHandlersDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler();
            bool ack = false;

            dispatcher.Wire("testBC", handler1);
            dispatcher.Wire("testBC", handler2);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.True(ack, "Handled command was not acknowledged");
        }

        [Test]
        public void FailingHandlersDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler(true);
            Tuple<long, bool> result = null;

            dispatcher.Wire("testBC", handler1);
            dispatcher.Wire("testBC", handler2);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });

            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched to first handler");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched to second handler");
            Assert.NotNull(result, "fail was not reported");
            Assert.AreEqual(EventDispatcher.FailedEventRetryDelay, result.Item1, "fail was not reported");
            Assert.False(result.Item2, "fail was not reported");
        }

        [Test]
        public void RetryingHandlersDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new ResultEventHandler(true, 100);
            Tuple<long, bool> result = null;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });

            Assert.NotNull(result, "fail was not reported");
            Assert.AreEqual(100, result.Item1, "fail was not reported");
            Assert.False(result.Item2, "fail was not reported");
        }

        // Note: Strange logic in EventDispatcher - might need to be revised in the future.
        [Test]
        public void EventWithNoHandlerIsAcknowledgedTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            bool ack = false;

            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.True(ack);
        }

        [Test]
        public void AsyncEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var asyncHandler = new EventAsyncHandler(false);
            bool ack = false;

            dispatcher.Wire("testBC", asyncHandler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.AreEqual(1, asyncHandler.HandledEvents.Count);
            Assert.True(ack, "Event handler was not processed properly");
        }

        [Test]
        public void ExceptionForAsyncEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventAsyncHandler(true);
            int failedCount = 0;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) =>
            {
                if (!acknowledge)
                    ++failedCount;
            });

            Assert.AreEqual(0, handler.HandledEvents.Count);
            Assert.AreEqual(1, failedCount, "Event handler was not processed properly");
        }

        [Test]
        public void AsyncResultEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventAsyncResultHandler(false);
            bool ack = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.AreEqual(1, handler.HandledEvents.Count);
            Assert.True(ack, "Event handler was not processed properly");
        }

        [Test]
        public void ExceptionForAsyncResultEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventAsyncResultHandler(true);
            int failedCount = 0;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) =>
            {
                if (!acknowledge)
                    ++failedCount;
            });

            Assert.AreEqual(0, handler.HandledEvents.Count);
            Assert.AreEqual(1, failedCount, "Event handler was not processed properly");
        }

        [Test]
        public void BatchDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandler {FailOnce = true};
            Tuple<long, bool> result = null;
            bool ack2 = false;
            bool ack3 = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", new []
            {
                Tuple.Create<object, AcknowledgeDelegate>("a", (delay, acknowledge) => {result = Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>("b", (delay, acknowledge) => { ack2 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("с", (delay, acknowledge) => { ack3 = acknowledge; })
            });

            Assert.NotNull(result, "fail was not reported");
            Assert.False(result.Item2, "fail was not reported");
            Assert.AreEqual(3, handler.HandledEvents.Count, "not all events were handled (exception in first event handling prevented following events processing?)");
            Assert.True(ack2);
            Assert.True(ack3);
        }

        [Test]
        public void BatchDispatchWithBatchHandlerOkTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventBatchHandler(false);
            bool ack1 = false;
            bool ack2 = false;
            bool ack3 = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>("a", (delay, acknowledge) => { ack1 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("b", (delay, acknowledge) => { ack2 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("с", (delay, acknowledge) => { ack3 = acknowledge; })
            });

            Assert.AreEqual(3, handler.HandledEvents.Count, "not all events were handled (exception in first event handling prevented following events processing?)");
            Assert.True(ack1);
            Assert.True(ack2);
            Assert.True(ack3);
        }

        [Test]
        public void BatchDispatchWithBatchHandlerFailTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventBatchHandler(true);
            bool ack1 = true;
            bool ack2 = true;
            bool ack3 = true;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>("a", (delay, acknowledge) => { ack1 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("b", (delay, acknowledge) => { ack2 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("с", (delay, acknowledge) => { ack3 = acknowledge; })
            });

            Assert.AreEqual(0, handler.HandledEvents.Count, "Some events were handled");
            Assert.False(ack1);
            Assert.False(ack2);
            Assert.False(ack3);
        }

        [Test]
        public void BatchDispatchTriggeringBySizeTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport();

            dispatcher.Wire(
                "testBC",
                handler,
                3,
                0,
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => { Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { }),
            });

            Assert.AreEqual(0, handler.HandledEvents.Count, "Events were delivered before batch is filled");

            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,3), (delay, acknowledge) => { })
            });

            Assert.AreEqual(3, handler.HandledEvents.Count, "Not all events were delivered");
            Assert.True(handler.BatchStartReported, "Batch start callback was not called");
            Assert.True(handler.BatchFinishReported, "Batch after apply  callback was not called");
            Assert.That(
                handler.HandledEvents.Select(t=>t.Item2),
                Is.EqualTo(new object[]{handler.LastCreatedBatchContext,handler.LastCreatedBatchContext,handler.LastCreatedBatchContext}),
                "Batch context was not the same for all evants in the batch");
        }

        [Test]
        public void BatchDispatchTriggeringByTimeoutTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport();

            dispatcher.Wire(
                "testBC",
                handler,
                3,
                1,
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => { Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { })
            });

            Assert.AreEqual(0, handler.HandledEvents.Count, "Events were delivered before batch apply timeoout");

            Thread.Sleep(2000);

            Assert.AreEqual(2, handler.HandledEvents.Count, "Not all events were delivered");
            Assert.True(handler.BatchStartReported, "Batch start callback was not called");
            Assert.True(handler.BatchFinishReported, "Batch after apply  callback was not called");
            Assert.That(
                handler.HandledEvents.Select(t => t.Item2),
                Is.EqualTo(new object[] { handler.LastCreatedBatchContext, handler.LastCreatedBatchContext }),
                "Batch context was not the same for all evants in the batch");
        }

        [Test]
        public void BatchDispatchUnackTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport(1);
            Tuple<long, bool> result = null;

            dispatcher.Wire(
                "testBC",
                handler,
                3,
                0,
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { }),
            });

            Assert.AreEqual(0, handler.HandledEvents.Count, "Events were delivered before batch is filled");

            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,3), (delay, acknowledge) => { })
            });

            Assert.AreEqual(3, handler.HandledEvents.Count, "Not all events were delivered");
            Assert.True(handler.BatchStartReported, "Batch start callback was not called");
            Assert.True(handler.BatchFinishReported, "Batch after apply  callback was not called");
            Assert.That(
                handler.HandledEvents.Select(t => t.Item2),
                Is.EqualTo(new object[] { handler.LastCreatedBatchContext, handler.LastCreatedBatchContext, handler.LastCreatedBatchContext }),
                "Batch context was not the same for all evants in the batch");
            Assert.False(result.Item2,"failed event was acked");
            Assert.AreEqual(10, result.Item1,"failed event retry timeout was wrong");
        }
    }
}
