using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Messaging;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Messaging.RabbitMq;
using Lykke.Cqrs.Configuration;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Moq;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    class FakeBatchContext
    {
    }

    class EventHandlerWithBatchSupport 
    {
        public readonly List<Tuple<object, object>> HandledEvents = new List<Tuple<object, object>>();
        private int _failCount;

        public EventHandlerWithBatchSupport(int failCount = 0)
        {
            _failCount = failCount;
        }

        public CommandHandlingResult Handle(DateTime e, FakeBatchContext batchContext)
        {
            HandledEvents.Add(Tuple.Create<object, object>(e, batchContext));
            var retry = Interlocked.Decrement(ref _failCount) >= 0;
            Console.WriteLine("Retry {0}",retry);
            return new CommandHandlingResult() {Retry = retry, RetryDelay = 10};
        }

        public FakeBatchContext OnBatchStart()
        {
            BatchStartReported = true;
            LastCreatedBatchContext = new FakeBatchContext();
            return LastCreatedBatchContext;
        }

        public FakeBatchContext LastCreatedBatchContext { get; set; }
        public bool BatchStartReported { get; set; }
        public bool BatchFinishReported { get; set; }

        public void OnBatchFinish(FakeBatchContext context)
        {
            BatchFinishReported = true;
        }
    }

    class EventHandler
    {
        private readonly bool _fail;

        public readonly List<object> HandledEvents = new List<object>();


        public bool FailOnce { get; set; }

        public EventHandler(bool fail = false)
        {
            _fail = fail;
        }

        [UsedImplicitly]
        public void Handle(string e)
        {
            HandledEvents.Add(e);
            if (_fail || FailOnce)
            {
                FailOnce = false;
                throw new Exception();
            }
        }

        public CommandHandlingResult[] Handle(int[] e)
        {
            return e.Select(i =>
            {
                HandledEvents.Add(i);
                return new CommandHandlingResult {Retry = _fail, RetryDelay = 600};
            }).ToArray();
        }

        public CommandHandlingResult Handle(Exception e)
        {
            HandledEvents.Add(e);
            return new CommandHandlingResult(){Retry = true,RetryDelay = 100};
        }
    }

    class EventHandlerWithAsyncHandle
    {
        [UsedImplicitly]
        public Task Handle(string evt)
        {
            throw new InvalidOperationException();
        }
    }

    class EventHandlerWithSyncHandle
    {
        [UsedImplicitly]
        public Task Handle(string evt)
        {
            throw new InvalidOperationException();
        }
    }

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
            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { });
            dispatcher.Dispatch("testBC", 1, (delay, acknowledge) => { });
            Assert.That(handler.HandledEvents, Is.EquivalentTo(new object[] { "test", 1}), "Some events were not dispatched");
        }

        [Test]
        public void MultipleHandlersDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler();
            dispatcher.Wire("testBC",handler1);
            dispatcher.Wire("testBC",handler2);
            dispatcher.Dispatch("testBC","test", (delay, acknowledge) => { });
            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
        }

        [Test]
        public void FailingHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher(_logFactory, "testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler(true);
            dispatcher.Wire("testBC",handler1);
            dispatcher.Wire("testBC", handler2);
            Tuple<long, bool> result=null;
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });
            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(result,Is.Not.Null,"fail was not reported");
            Assert.That(result.Item2,Is.False,"fail was not reported");
            Assert.That(result.Item1, Is.EqualTo(EventDispatcher.FailedEventRetryDelay), "fail was not reported");
        }

        [Test]
        public void RetryingHandlersDispatchTest()
        {
            var dispatcher=new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandler();
            dispatcher.Wire("testBC", handler);
            Tuple<long, bool> result=null;
            dispatcher.Dispatch("testBC", new Exception(), (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });
            Assert.That(result,Is.Not.Null,"fail was not reported");
            Assert.That(result.Item2,Is.False,"fail was not reported");
            Assert.That(result.Item1, Is.EqualTo(100), "fail was not reported");
        }

        [Test]
        public void BatchDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandler();
            dispatcher.Wire("testBC", handler);
            Tuple<long, bool> result = null;
            handler.FailOnce = true;
            dispatcher.Dispatch("testBC",new []
            {
                Tuple.Create<object,AcknowledgeDelegate>("a", (delay, acknowledge) => {result = Tuple.Create(delay, acknowledge);  }),
                Tuple.Create<object,AcknowledgeDelegate>("b", (delay, acknowledge) => { }),
                Tuple.Create<object,AcknowledgeDelegate>("с", (delay, acknowledge) => { })
            });

            Assert.That(result, Is.Not.Null, "fail was not reported");
            Assert.That(result.Item2, Is.False, "fail was not reported");
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(3), "not all events were handled (exception in first event handling prevented following events processing?)");
        }

        [Test]
        public void BatchDispatchTriggeringBySizeTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport();
            dispatcher.Wire("testBC", handler, 3, 0, 
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => {Tuple.Create(delay, acknowledge);  }),
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { }),
            });
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(0), "Events were delivered before batch is filled");
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,3), (delay, acknowledge) => { })
            });
            Assert.That(handler.HandledEvents.Count, Is.Not.EqualTo(0), "Events were not delivered after batch is filled");
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(3), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
            Assert.That(handler.HandledEvents.Select(t=>t.Item2),Is.EqualTo(new object[]{handler.LastCreatedBatchContext,handler.LastCreatedBatchContext,handler.LastCreatedBatchContext}),"Batch context was not the same for all evants in the batch");
        }

        [Test]
        public void BatchDispatchTriggeringByTimeoutTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport();
            dispatcher.Wire("testBC", handler, 3, 1, typeof(FakeBatchContext), h => ((EventHandlerWithBatchSupport)h).OnBatchStart(), (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => {Tuple.Create(delay, acknowledge);  }),
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { })
            });
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(0), "Events were delivered before batch apply timeoout");
            Thread.Sleep(2000);
            Assert.That(handler.HandledEvents.Count, Is.Not.EqualTo(0), "Events were not delivered after batch is filled");
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(2), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
            Assert.That(handler.HandledEvents.Select(t => t.Item2), Is.EqualTo(new object[] { handler.LastCreatedBatchContext,  handler.LastCreatedBatchContext }), "Batch context was not the same for all evants in the batch");
        }

        [Test]
        public void BatchDispatchUnackTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport(1);
            dispatcher.Wire("testBC", handler, 3, 0,
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            Tuple<long, bool> result = null;
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => {result = Tuple.Create(delay, acknowledge);  }),
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { }),
            });
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(0), "Events were delivered before batch is filled");
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object,AcknowledgeDelegate>(new DateTime(2016,3,3), (delay, acknowledge) => { })
            });
            Assert.That(handler.HandledEvents.Count, Is.Not.EqualTo(0), "Events were not delivered after batch is filled");
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(3), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
            Assert.That(handler.HandledEvents.Select(t => t.Item2), Is.EqualTo(new object[] { handler.LastCreatedBatchContext, handler.LastCreatedBatchContext, handler.LastCreatedBatchContext }), "Batch context was not the same for all evants in the batch");
            Assert.That(result.Item2, Is.False,"failed event was acked");
            Assert.That(result.Item1, Is.EqualTo(10),"failed event retry timeout was wrong");
        }

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
                messagingEngine.CreateTemporaryDestination("RabbitMq",null);
           
                var endpoint = new Endpoint("RabbitMq", "testExchange" , "testQueue", true, "json");
                endpointProvider.Setup(r => r.Get("route")).Returns(endpoint);
                endpointProvider.Setup(r => r.Contains("route")).Returns(true);

                using (new CqrsEngine(_logFactory, new DefaultDependencyResolver(),messagingEngine, endpointProvider.Object,false,
                    Register.BoundedContext("bc").ListeningEvents(typeof(DateTime)).From("other").On("route")
                        .WithProjection(handler, "other",1,0,
                            h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                            (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c)
                        )
                ))
                {
                    messagingEngine.Send(DateTime.Now, endpoint);
                    Thread.Sleep(20000);
                }
            }
        }

        [Test]
        public void TestExceptionForAsyncEventHadler()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var asyncHandler = new EventHandlerWithAsyncHandle();
            dispatcher.Wire("testBC", asyncHandler);
            int failedCount = 0;
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) =>
            {
                if (!acknowledge)
                    ++failedCount;
            });
            Assert.True(1 == failedCount, "Async event handler was not processed properly");
        }

        [Test]
        public void TestExceptionForSyncEventHadler()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var syncHandler = new EventHandlerWithSyncHandle();
            dispatcher.Wire("testBC", syncHandler);
            int failedCount = 0;
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) =>
            {
                if (!acknowledge)
                    ++failedCount;
            });
            Assert.True(1 == failedCount, "Sync event handler was not processed properly");
        }
    }
}
