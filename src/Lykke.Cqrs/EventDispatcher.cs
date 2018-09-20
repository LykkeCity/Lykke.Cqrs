using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Messaging.Contract;
using ThreadState = System.Threading.ThreadState;

namespace Lykke.Cqrs
{
    internal class EventDispatcher : IDisposable
    {
        readonly Dictionary<EventOrigin, List<Tuple<Func<object[],object, CommandHandlingResult[]>,BatchManager>>> m_Handlers =
            new Dictionary<EventOrigin, List<Tuple<Func<object[],object, CommandHandlingResult[]>, BatchManager>>>();
        private readonly ILog _log;
        private readonly string m_BoundedContext;
        internal static long m_FailedEventRetryDelay = 60000;
        readonly ManualResetEvent m_Stop = new ManualResetEvent(false);
        private readonly Thread m_ApplyBatchesThread;
        private readonly BatchManager m_DefaultBatchManager;

        public EventDispatcher(ILog log, string boundedContext)
        {
            m_DefaultBatchManager = new BatchManager(log, m_FailedEventRetryDelay);
            _log = log;
            m_BoundedContext = boundedContext;
            m_ApplyBatchesThread = new Thread(() =>
            {
                while (!m_Stop.WaitOne(1000))
                {
                    ApplyBatches();
                }
            });
            m_ApplyBatchesThread.Name = string.Format("'{0}' bounded context batch event processing thread",boundedContext);
        }

        private void ApplyBatches(bool force = false)
        {
            foreach (var batchManager in m_Handlers.SelectMany(h => h.Value.Select(_ => _.Item2)))
            {
                batchManager.ApplyBatchIfRequired(force);
            }
        }

        public void Wire(string fromBoundedContext,object o, params OptionalParameterBase[] parameters)
        {
            //TODO: decide whet to pass as context here
            Wire(fromBoundedContext, o, null, null, parameters);
        }

        public void Wire(
            string fromBoundedContext,
            object o,
            int batchSize,
            int applyTimeoutInSeconds,
            Type batchContextType,
            Func<object, object> beforeBatchApply,
            Action<object, object> afterBatchApply,
            params OptionalParameterBase[] parameters)
        {
            Func<object> beforeBatchApplyWrap = beforeBatchApply == null ? (Func<object>)null : () => beforeBatchApply(o);
            Action<object> afterBatchApplyWrap = afterBatchApply == null ? (Action<object>)null : c => afterBatchApply(o, c);
            var batchManager = batchSize == 0 && applyTimeoutInSeconds == 0
                ? null
                : new BatchManager(
                    _log,
                    m_FailedEventRetryDelay,
                    batchSize,
                    applyTimeoutInSeconds * 1000,
                    beforeBatchApplyWrap,
                    afterBatchApplyWrap);
            Wire(
                fromBoundedContext,
                o,
                batchManager,
                batchContextType,
                parameters);
        }

        private void Wire(
            string fromBoundedContext,
            object o,
            BatchManager batchManager,
            Type batchContextType,
            params OptionalParameterBase[] parameters)
        {
            if (batchManager != null && m_ApplyBatchesThread.ThreadState == ThreadState.Unstarted && batchManager.ApplyTimeout != 0)
                m_ApplyBatchesThread.Start();

            var batchContextParameter = new ExpressionParameter(null,batchContextType);
            parameters = parameters.Concat(new OptionalParameterBase[]
            {
                new OptionalParameter<string>("boundedContext", fromBoundedContext)
            }).ToArray();

            if (batchContextType != null)
            {
                parameters = parameters.Concat(new[] {batchContextParameter}).ToArray();
            }

            var handleMethods = o.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && 
                    !m.IsGenericMethod && 
                    m.GetParameters().Length>0 && 
                    !m.GetParameters().First().ParameterType.IsInterface &&
                    !(m.GetParameters().First().ParameterType.IsArray && m.GetParameters().First().ParameterType.GetElementType().IsInterface)
                    )
                .Select(m => new {
                    method = m,
                    eventType = m.GetParameters().First().ParameterType,
                    returnsResult = m.ReturnType == typeof (Task<CommandHandlingResult>),
                    isBatch = m.ReturnType == typeof (CommandHandlingResult[]) && m.GetParameters().First().ParameterType.IsArray,
                    callParameters = m.GetParameters().Skip(1).Select(p => new
                    {
                        parameter = p,
                        optionalParameter = parameters.FirstOrDefault(par => par.Name == p.Name || par.Name == null   && p.ParameterType == par.Type)
                    })
                })
                .Where(m => m.callParameters.All(p => p.parameter != null));

            foreach (var method in handleMethods)
            {
                var eventType = method.isBatch ? method.eventType.GetElementType() : method.eventType;
                var key = new EventOrigin(fromBoundedContext, eventType);
                List<Tuple<Func<object[],object, CommandHandlingResult[]>, BatchManager>> handlersList;
                if (!m_Handlers.TryGetValue(key, out handlersList))
                {
                    handlersList = new List<Tuple<Func<object[], object, CommandHandlingResult[]>, BatchManager>>();
                    m_Handlers.Add(key, handlersList);
                }

                var notInjectableParameters = method.callParameters.Where(p => p.optionalParameter == null).Select(p =>p.parameter.ParameterType+" "+p.parameter.Name).ToArray();
                if(notInjectableParameters.Length>0)
                    throw new InvalidOperationException(string.Format("{0} type can not be registered as event handler. Method {1} contains non injectable parameters:{2}",
                        o.GetType().Name,
                        method.method,
                        string.Join(", ", notInjectableParameters)));

                var handler = method.isBatch
                    ? CreateBatchHandler(eventType, o, method.callParameters.Select(p => p.optionalParameter), batchContextParameter)
                    : CreateHandler(eventType, o, method.callParameters.Select(p => p.optionalParameter), method.returnsResult, batchContextParameter);

                handlersList.Add(Tuple.Create(handler, batchManager ?? m_DefaultBatchManager));
            }
        }

        private Func<object[], object, CommandHandlingResult[]> CreateBatchHandler(
            Type eventType,
            object o,
            IEnumerable<OptionalParameterBase> optionalParameters,
            ExpressionParameter batchContext)
        {
            LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult[]));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult[0]));

            var events = Expression.Parameter(typeof(object[]));
            var eventsListType = typeof(List<>).MakeGenericType(eventType);
            var list = Expression.Variable(eventsListType, "list");
            var @event = Expression.Variable(typeof(object), "@event");
            var callParameters=new []{events,batchContext.Parameter};

            var handleParams = new Expression[] { Expression.Call(list, eventsListType.GetMethod("ToArray")) }
                .Concat(optionalParameters.Select(p => p.ValueExpression))
                .ToArray();

            var callHandler = Expression.Call(Expression.Constant(o), "Handle", null, handleParams);

            Expression addConvertedEvent = Expression.Call(list, eventsListType.GetMethod("Add"), Expression.Convert(@event, eventType));

            var create = Expression.Block(
               new[] { list, @event },
               Expression.Assign(list, Expression.New(eventsListType)),
               ForEachExpr(events, @event, addConvertedEvent),
               Expression.Return(returnTarget,callHandler),
               returnLabel
               );

            var lambda = (Expression<Func<object[], object, CommandHandlingResult[]>>)Expression.Lambda(create, callParameters);

            return lambda.Compile();
        }

        private Func<object[], object, CommandHandlingResult[]> CreateHandler(
            Type eventType,
            object o,
            IEnumerable<OptionalParameterBase> optionalParameters,
            bool returnsResult,
            ExpressionParameter batchContext)
        {
            LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult[]));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult[0]));

            var events = Expression.Parameter(typeof(object[]));
            var result = Expression.Variable(typeof(List<CommandHandlingResult>), "result");
            var @event = Expression.Variable(typeof(object), "@event");

            var callParameters = new []{events,batchContext.Parameter};

            var handleParams = new Expression[] { Expression.Convert(@event, eventType) }
                .Concat(optionalParameters.Select(p => p.ValueExpression))
                .ToArray();

            var callHandler = Expression.Call(Expression.Call(Expression.Constant(o), "Handle", null, handleParams), "Wait", null);

            var okResult = Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 });
            var failResult = Expression.Constant(new CommandHandlingResult { Retry = true, RetryDelay = m_FailedEventRetryDelay });
            
            Expression registerResult  = Expression.TryCatch(
                Expression.Block(
                    typeof(void),
                    returnsResult
                        ?(Expression)Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), callHandler)
                        :(Expression)Expression.Block(callHandler, Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), okResult))
                    ),
                Expression.Catch(
                    typeof(Exception),
                     Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("Add"), failResult)
                    )
                );

            var create = Expression.Block(
               new[] { result, @event },
               Expression.Assign(result, Expression.New(typeof(List<CommandHandlingResult>))),
               ForEachExpr(events, @event, registerResult),
               Expression.Return(returnTarget, Expression.Call(result, typeof(List<CommandHandlingResult>).GetMethod("ToArray"))),
               returnLabel
               );

            var lambda = (Expression<Func<object[], object, CommandHandlingResult[]>>)Expression.Lambda(create, callParameters);

            return lambda.Compile();
        }

        private static BlockExpression ForEachExpr(ParameterExpression enumerable, ParameterExpression item, Expression expression)
        {
            var enumerator = Expression.Variable(typeof(IEnumerator), "enumerator");
            var doMoveNext = Expression.Call(enumerator, typeof(IEnumerator).GetMethod("MoveNext"));
            var assignToEnum = Expression.Assign(enumerator, Expression.Call(enumerable, typeof(IEnumerable).GetMethod("GetEnumerator")));
            var assignCurrent = Expression.Assign(item, Expression.Property(enumerator, "Current"));
            var @break = Expression.Label();

            var @foreach = Expression.Block(
                    new [] { enumerator },
                    assignToEnum,
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.NotEqual(doMoveNext, Expression.Constant(false)),
                            Expression.Block(assignCurrent, expression),
                            Expression.Break(@break)), 
                        @break)
                );

            return @foreach;
        }

        public void Dispatch(string fromBoundedContext, IEnumerable<Tuple<object, AcknowledgeDelegate>> events)
        {
            foreach (var e in events.GroupBy(e => new EventOrigin(fromBoundedContext, e.Item1.GetType())))
            {
                Dispatch(e.Key, e.ToArray());
            }
        }

        //TODO: delete
        public void Dispatch(string fromBoundedContext, object message, AcknowledgeDelegate acknowledge)
        {
            Dispatch(fromBoundedContext, new[] {Tuple.Create(message, acknowledge)});
        }

        private void Dispatch(EventOrigin origin, Tuple<object, AcknowledgeDelegate>[] events)
        {
            List<Tuple<Func<object[], object, CommandHandlingResult[]>, BatchManager>> list;

            if (events == null)
            {
                //TODO: need to handle null deserialized from messaging
                throw new ArgumentNullException("events");
            }

            if (!m_Handlers.TryGetValue(origin, out list))
            {
                foreach (var @event in events)
                {
                    @event.Item2(0, true);
                }
                return;
            }

            var handlersByBatchManager = list.GroupBy(i => i.Item2);
            foreach (var grouping in handlersByBatchManager)
            {
                var batchManager = grouping.Key;
                var handlers = grouping.Select(h => h.Item1).ToArray();
                batchManager.Handle(handlers, events, origin);
            }
        }

        public void Dispose()
        {
            if (m_ApplyBatchesThread.ThreadState == ThreadState.Unstarted) 
                return;
            m_Stop.Set();
            m_ApplyBatchesThread.Join();
            ApplyBatches(true);
        }
    }
}