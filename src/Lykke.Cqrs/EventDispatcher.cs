using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Common.Log;
using Lykke.Messaging.Contract;
using ThreadState = System.Threading.ThreadState;

namespace Lykke.Cqrs
{
    internal class EventDispatcher : IDisposable
    {
        private readonly Dictionary<EventOrigin, List<((string, Func<object[], object, CommandHandlingResult[]>), BatchManager)>> m_batchHandlerInfos =
            new Dictionary<EventOrigin, List<((string, Func<object[], object, CommandHandlingResult[]>), BatchManager)>>();
        private readonly Dictionary<EventOrigin, List<((string, Func<object, object, CommandHandlingResult>), BatchManager)>> m_handlerInfos =
            new Dictionary<EventOrigin, List<((string, Func<object, object, CommandHandlingResult>), BatchManager)>>();
        private readonly ILog _log;
        private readonly string m_BoundedContext;
        private readonly ManualResetEvent m_Stop = new ManualResetEvent(false);
        private readonly Thread m_ApplyBatchesThread;
        private readonly BatchManager m_DefaultBatchManager;
        private readonly MethodInfo _getAwaiterInfo;

        internal static long m_FailedEventRetryDelay = 60000;

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

            var taskMethods = typeof(Task<CommandHandlingResult>).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var awaiterResultType = typeof(TaskAwaiter<CommandHandlingResult>);
            _getAwaiterInfo = taskMethods.First(m => m.Name == "GetAwaiter" && m.ReturnType == awaiterResultType);
        }

        private void ApplyBatches(bool force = false)
        {
            var batchManagers = m_batchHandlerInfos.SelectMany(h => h.Value.Select(_ => _.Item2))
                .Concat(m_handlerInfos.SelectMany(h => h.Value.Select(_ => _.Item2)))
                .Distinct();

            foreach (var batchManager in batchManagers)
            {
                batchManager.ApplyBatchIfRequired(force);
            }
        }

        public void Wire(string fromBoundedContext, object o, params OptionalParameterBase[] parameters)
        {
            //TODO: decide whet to pass as context here
            Wire(
                fromBoundedContext,
                o,
                null,
                null,
                parameters);
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

            var batchContextParameter = new ExpressionParameter(null, batchContextType);
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
                    returnType = m.ReturnType,
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
                var notInjectableParameters = method.callParameters
                    .Where(p => p.optionalParameter == null)
                    .Select(p => p.parameter.ParameterType + " " + p.parameter.Name)
                    .ToArray();
                if(notInjectableParameters.Length > 0)
                    throw new InvalidOperationException(
                        $"{o.GetType().Name} type can not be registered as event handler. Method {method.method} contains non injectable parameters:{string.Join(", ", notInjectableParameters)}");

                var eventType = method.isBatch ? method.eventType.GetElementType() : method.eventType;
                var key = new EventOrigin(fromBoundedContext, eventType);
                if (method.isBatch)
                {
                    List<((string, Func<object[], object, CommandHandlingResult[]>), BatchManager)> batchHandlersList;
                    if (!m_batchHandlerInfos.TryGetValue(key, out batchHandlersList))
                    {
                        batchHandlersList = new List<((string, Func<object[], object, CommandHandlingResult[]>), BatchManager)>();
                        m_batchHandlerInfos.Add(key, batchHandlersList);
                    }
                    var batchHandler = CreateBatchHandler(
                        eventType,
                        o,
                        method.callParameters.Select(p => p.optionalParameter),
                        batchContextParameter);
                    batchHandlersList.Add(((o.GetType().Name, batchHandler), batchManager ?? m_DefaultBatchManager));
                }
                else
                {
                    List<((string, Func<object, object, CommandHandlingResult>), BatchManager)> handlersList;
                    if (!m_handlerInfos.TryGetValue(key, out handlersList))
                    {
                        handlersList = new List<((string, Func<object, object, CommandHandlingResult>), BatchManager)>();
                        m_handlerInfos.Add(key, handlersList);
                    }
                    var handler = CreateHandler(
                        eventType,
                        o,
                        method.callParameters.Select(p => p.optionalParameter),
                        method.returnType,
                        batchContextParameter);
                    handlersList.Add(((o.GetType().Name, handler), batchManager ?? m_DefaultBatchManager));
                }
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
            var callParameters = new []{ events, batchContext.Parameter };

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

        private Func<object, object, CommandHandlingResult> CreateHandler(
            Type eventType,
            object o,
            IEnumerable<OptionalParameterBase> optionalParameters,
            Type returnType,
            ExpressionParameter batchContext)
        {
            LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult()));

            var @event = Expression.Parameter(typeof(object));
            var handleParams = new Expression[] { Expression.Convert(@event, eventType) }
                .Concat(optionalParameters.Select(p => p.ValueExpression))
                .ToArray();

            var handleCall = Expression.Call(Expression.Constant(o), "Handle", null, handleParams);
            Expression resultCall;
            if (returnType == typeof(Task<CommandHandlingResult>))
            {
                var awaiterCall = Expression.Call(handleCall, _getAwaiterInfo);
                resultCall = Expression.Call(awaiterCall, "GetResult", null);
            }
            else if (returnType == typeof(CommandHandlingResult))
            {
                resultCall = handleCall;
            }
            else
            {
                var awaiterMethod = returnType.GetMethod("GetAwaiter");
                resultCall = Expression.Block(
                    awaiterMethod != null
                        ? Expression.Call(Expression.Call(handleCall, awaiterMethod), "GetResult", null)
                        : handleCall,
                    Expression.Label(
                        Expression.Label(typeof(CommandHandlingResult)),
                        Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 })));
            }

            var create = Expression.Block(
               Expression.Return(returnTarget, resultCall),
               returnLabel);

            var lambda = (Expression<Func<object, object, CommandHandlingResult>>)Expression.Lambda(create, @event, batchContext.Parameter);

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
            if (events == null)
            {
                //TODO: need to handle null deserialized from messaging
                throw new ArgumentNullException(nameof(events));
            }
            if (events.Length == 0)
                return;

            bool handlerFound = false;

            if (m_batchHandlerInfos.TryGetValue(origin, out var batchList))
            {
                var handlersByBatchManager = batchList.GroupBy(i => i.Item2);
                foreach (var grouping in handlersByBatchManager)
                {
                    var batchManager = grouping.Key;
                    var handlers = grouping.Select(h => h.Item1).ToArray();
                    batchManager.BatchHandle(handlers, events, origin);
                }
                handlerFound = true;
            }

            if (m_handlerInfos.TryGetValue(origin, out var list))
            {
                var handlersByBatchManager = list.GroupBy(i => i.Item2);
                foreach (var grouping in handlersByBatchManager)
                {
                    var batchManager = grouping.Key;
                    var handlers = grouping.Select(h => h.Item1).ToArray();
                    batchManager.Handle(handlers, events, origin);
                }
                handlerFound = true;
            }

            if (!handlerFound)
                foreach (var @event in events)
                {
                    @event.Item2(0, true);
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