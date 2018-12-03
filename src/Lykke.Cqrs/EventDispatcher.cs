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
using Lykke.Common.Log;
using Lykke.Cqrs.Middleware;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    internal class EventDispatcher : IDisposable
    {
        internal const long FailedEventRetryDelay = 60000;

        private readonly Dictionary<EventOrigin, List<((string, Func<object[], object, CommandHandlingResult[]>), BatchManager)>> _batchHandlerInfos =
            new Dictionary<EventOrigin, List<((string, Func<object[], object, CommandHandlingResult[]>), BatchManager)>>();
        private readonly Dictionary<EventOrigin, List<(EventHandlerInfo, BatchManager)>> _handlerInfos =
            new Dictionary<EventOrigin, List<(EventHandlerInfo, BatchManager)>>();
        private readonly ILog _log;
        private readonly EventInterceptorsQueue _eventInterceptorsProcessor;
        private readonly string _boundedContext;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private readonly Thread _applyBatchesThread;
        private readonly BatchManager _defaultBatchManager;
        private readonly MethodInfo _getAwaiterInfo;
        private readonly ILogFactory _logFactory;

        [Obsolete]
        public EventDispatcher(
            ILog log,
            string boundedContext,
            EventInterceptorsQueue eventInterceptorsProcessor)
        {
            _eventInterceptorsProcessor = eventInterceptorsProcessor ?? new EventInterceptorsQueue();
            _defaultBatchManager = new BatchManager(
                log,
                FailedEventRetryDelay,
                _eventInterceptorsProcessor);
            _log = log;
            _boundedContext = boundedContext;
            _applyBatchesThread = new Thread(() =>
            {
                while (!_stop.WaitOne(1000))
                {
                    ApplyBatches();
                }
            })
            {
                Name = $"'{boundedContext}' bounded context batch event processing thread"
            };

            var taskMethods = typeof(Task<CommandHandlingResult>).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var awaiterResultType = typeof(TaskAwaiter<CommandHandlingResult>);
            _getAwaiterInfo = taskMethods.First(m => m.Name == "GetAwaiter" && m.ReturnType == awaiterResultType);
        }

        public EventDispatcher(
            ILogFactory logFactory,
            string boundedContext,
            EventInterceptorsQueue eventInterceptorsProcessor = null)
        {
            _eventInterceptorsProcessor = eventInterceptorsProcessor ?? new EventInterceptorsQueue();
            _defaultBatchManager = new BatchManager(
                logFactory,
                FailedEventRetryDelay,
                _eventInterceptorsProcessor);
            _log = logFactory.CreateLog(this);
            _logFactory = logFactory;
            _boundedContext = boundedContext;
            _applyBatchesThread = new Thread(() =>
            {
                while (!_stop.WaitOne(1000))
                {
                    ApplyBatches();
                }
            })
            {
                Name = $"'{boundedContext}' bounded context batch event processing thread"
            };

            var taskMethods = typeof(Task<CommandHandlingResult>).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var awaiterResultType = typeof(TaskAwaiter<CommandHandlingResult>);
            _getAwaiterInfo = taskMethods.First(m => m.Name == "GetAwaiter" && m.ReturnType == awaiterResultType);
        }

        private void ApplyBatches(bool force = false)
        {
            var batchManagers = _batchHandlerInfos.SelectMany(h => h.Value.Select(_ => _.Item2))
                .Concat(_handlerInfos.SelectMany(h => h.Value.Select(_ => _.Item2)))
                .Distinct();

            foreach (var batchManager in batchManagers)
            {
                batchManager.ApplyBatchIfRequired(force);
            }
        }

        internal List<Type> GetUnhandledEventTypes(string boundedContext, Type[] eventTypes)
        {
            var notHandledEventTypeNames = new List<Type>();
            foreach (var eventType in eventTypes)
            {
                var eventOrigin = new EventOrigin(boundedContext, eventType);
                if (!_batchHandlerInfos.ContainsKey(eventOrigin) && !_handlerInfos.ContainsKey(eventOrigin))
                    notHandledEventTypeNames.Add(eventType);
            }

            return notHandledEventTypeNames;
        }

        internal void Wire(string fromBoundedContext, object o, params OptionalParameterBase[] parameters)
        {
            //TODO: decide whet to pass as context here
            Wire(
                fromBoundedContext,
                o,
                null,
                null,
                parameters);
        }

        internal void Wire(
            string fromBoundedContext,
            object o,
            int batchSize,
            int applyTimeoutInSeconds,
            Type batchContextType,
            Func<object, object> beforeBatchApply,
            Action<object, object> afterBatchApply,
            params OptionalParameterBase[] parameters)
        {
            var beforeBatchApplyWrap = beforeBatchApply == null ? (Func<object>)null : () => beforeBatchApply(o);
            var afterBatchApplyWrap = afterBatchApply == null ? (Action<object>)null : c => afterBatchApply(o, c);
            var batchManager = batchSize == 0 && applyTimeoutInSeconds == 0
                ? null
                : _logFactory != null
                    ? new BatchManager(
                        _logFactory,
                        FailedEventRetryDelay,
                        _eventInterceptorsProcessor,
                        batchSize,
                        applyTimeoutInSeconds * 1000,
                        beforeBatchApplyWrap,
                        afterBatchApplyWrap)
                    : new BatchManager(
                        _log,
                        FailedEventRetryDelay,
                        _eventInterceptorsProcessor,
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
            if (batchManager != null && _applyBatchesThread.ThreadState == ThreadState.Unstarted && batchManager.ApplyTimeout != 0)
                _applyBatchesThread.Start();

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
                    if (!_batchHandlerInfos.TryGetValue(key, out var batchHandlersList))
                    {
                        batchHandlersList = new List<((string, Func<object[], object, CommandHandlingResult[]>), BatchManager)>();
                        _batchHandlerInfos.Add(key, batchHandlersList);
                    }
                    var batchHandler = CreateBatchHandler(
                        eventType,
                        o,
                        method.callParameters.Select(p => p.optionalParameter),
                        batchContextParameter);
                    batchHandlersList.Add(((o.GetType().Name, batchHandler), batchManager ?? _defaultBatchManager));
                }
                else
                {
                    if (!_handlerInfos.TryGetValue(key, out var handlersList))
                    {
                        handlersList = new List<(EventHandlerInfo, BatchManager)>();
                        _handlerInfos.Add(key, handlersList);
                    }
                    var handler = CreateHandler(
                        eventType,
                        o,
                        method.callParameters.Select(p => p.optionalParameter).ToArray(),
                        method.returnType,
                        batchContextParameter);
                    var commandSender = (ICommandSender)parameters?.FirstOrDefault(p => p.Type == typeof(ICommandSender))?.Value;
                    handlersList.Add(
                        (new EventHandlerInfo(o, commandSender, handler), batchManager ?? _defaultBatchManager));
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

        private Func<object, ICommandSender, object, CommandHandlingResult> CreateHandler(
            Type eventType,
            object o,
            IEnumerable<OptionalParameterBase> optionalParameters,
            Type returnType,
            ExpressionParameter batchContext)
        {
            LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult()));

            var @event = Expression.Parameter(typeof(object));
            var commandSenderParam = Expression.Parameter(typeof(ICommandSender));

            var handleParams = new Expression[] { Expression.Convert(@event, eventType) }
                .Concat(optionalParameters.Select(p => p.Type == typeof(ICommandSender) ? commandSenderParam : p.ValueExpression))
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

            var lambda = (Expression<Func<object, ICommandSender, object, CommandHandlingResult>>)Expression.Lambda(
                create,
                @event,
                commandSenderParam,
                batchContext.Parameter);

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

            if (_batchHandlerInfos.TryGetValue(origin, out var batchList))
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

            if (_handlerInfos.TryGetValue(origin, out var list))
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
            {
                _log.WriteWarning(nameof(Dispatch), origin, $"Event handler not found for type {origin.EventType} from context {origin.BoundedContext}");
                foreach (var @event in events)
                {
                    @event.Item2(0, true);
                }
            }
        }

        public void Dispose()
        {
            if (_applyBatchesThread.ThreadState == ThreadState.Unstarted) 
                return;
            _stop.Set();
            _applyBatchesThread.Join();
            ApplyBatches(true);
        }
    }
}