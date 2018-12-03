using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Cqrs.Middleware;
using Lykke.Cqrs.Utils;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    internal class BatchManager
    {
        private readonly List<Action<object>> _events = new List<Action<object>>();
        private readonly int _batchSize;
        private readonly ILog _log;
        private readonly long _failedEventRetryDelay;
        private readonly Stopwatch _sinceFirstEvent = new Stopwatch();
        private readonly EventInterceptorsQueue _eventInterceptorsProcessor;
        private readonly Func<object> _beforeBatchApply;
        private readonly Action<object> _afterBatchApply;

        private long _counter;

        public long ApplyTimeout { get; }

        [Obsolete]
        internal BatchManager(
            ILog log,
            long failedEventRetryDelay,
            EventInterceptorsQueue eventInterceptorsProcessor,
            int batchSize = 0,
            long applyTimeout = 0,
            Func<object> beforeBatchApply = null,
            Action<object> afterBatchApply = null)
        {
            _afterBatchApply = afterBatchApply ?? (o => { });
            _beforeBatchApply = beforeBatchApply ?? (() => null);
            _log = log;
            _eventInterceptorsProcessor = eventInterceptorsProcessor;
            _failedEventRetryDelay = failedEventRetryDelay;
            ApplyTimeout = applyTimeout;
            _batchSize = batchSize;
        }

        internal BatchManager(
            ILogFactory logFactory,
            long failedEventRetryDelay,
            EventInterceptorsQueue eventInterceptorsProcessor,
            int batchSize = 0,
            long applyTimeout = 0,
            Func<object> beforeBatchApply = null,
            Action<object> afterBatchApply = null)
        {
            _afterBatchApply = afterBatchApply ?? (o => { });
            _beforeBatchApply = beforeBatchApply ?? (() => null);
            _log = logFactory.CreateLog(this);
            _eventInterceptorsProcessor = eventInterceptorsProcessor;
            _failedEventRetryDelay = failedEventRetryDelay;
            ApplyTimeout = applyTimeout;
            _batchSize = batchSize;
        }

        public void BatchHandle(
            (string, Func<object[], object, CommandHandlingResult[]>)[] batchHandlerInfos,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin)
        {
            if(events.Length == 0)
                return;

            if (_batchSize == 0 && ApplyTimeout == 0)
            {
                DoBatchHandle(
                    batchHandlerInfos,
                    events,
                    origin,
                    null);
                return;
            }

            lock (_events)
            {
                _events.Add(batchContext => DoBatchHandle(
                    batchHandlerInfos,
                    events,
                    origin,
                    batchContext));
                if (_counter == 0 && ApplyTimeout != 0)
                    _sinceFirstEvent.Start();
                _counter += events.Length;
                ApplyBatchIfRequired();
            }
        }

        public void Handle(
            EventHandlerInfo[] handlerInfos,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin)
        {
            if (events.Length == 0)
                return;

            if (_batchSize == 0 && ApplyTimeout == 0)
            {
                DoHandle(
                    handlerInfos,
                    events,
                    origin,
                    null);
                return;
            }

            lock (_events)
            {
                _events.Add(batchContext => DoHandle(
                    handlerInfos,
                    events,
                    origin,
                    batchContext));
                if (_counter == 0 && ApplyTimeout != 0)
                    _sinceFirstEvent.Start();
                _counter += events.Length;
                ApplyBatchIfRequired();
            }
        }

        internal void ApplyBatchIfRequired(bool force = false)
        {
            Action<object>[] handles = new Action<object>[0];

            lock (_events)
            {
                if (_counter == 0)
                    return;

                if ((_counter >= _batchSize && _batchSize != 0)
                    || (_sinceFirstEvent.ElapsedMilliseconds > ApplyTimeout && ApplyTimeout != 0)
                    || force)
                {
                    handles = _events.ToArray();
                    _events.Clear();
                    _counter = 0;
                    _sinceFirstEvent.Reset();
                }
            }
            if (!handles.Any())
                return;

            var batchContext = _beforeBatchApply();
            foreach (var handle in handles)
            {
                handle(batchContext);
            }
            _afterBatchApply(batchContext);
        }

        private void DoBatchHandle(
            (string, Func<object[], object, CommandHandlingResult[]>)[] batchHandlerInfos,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin,
            object batchContext)
        {
            CommandHandlingResult[] results = @events
                .Select(x => new CommandHandlingResult { Retry = false, RetryDelay = 0 })
                .ToArray();
            try
            {
                var eventsArray = @events.Select(e => e.Item1).ToArray();

                ProcessBatchHandlers(
                    batchHandlerInfos,
                    eventsArray,
                    results,
                    origin,
                    batchContext);
            }
            catch (Exception ex)
            {
                _log.WriteError(
                    "EventDispatcher.DoBatchHandle",
                    "Failed to handle events batch of type " + origin.EventType.Name,
                    ex);
                foreach (var result in results)
                {
                    result.Retry = true;
                    result.RetryDelay = _failedEventRetryDelay;
                }
            }

            //TODO: What if connect is broken and engine failes to aknowledge?..
            for (var i = 0; i < events.Length; i++)
            {
                var result = results[i];
                var acknowledge = events[i].Item2;
                if (result.Retry)
                    acknowledge(result.RetryDelay, false);
                else
                    acknowledge(0, true);
            }
        }

        private void DoHandle(
            EventHandlerInfo[] handlerInfos,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin,
            object batchContext)
        {
            CommandHandlingResult[] results = @events
                .Select(x => new CommandHandlingResult { Retry = false, RetryDelay = 0 })
                .ToArray();
            try
            {
                var eventsArray = @events.Select(e => e.Item1).ToArray();

                ProcessHandlers(
                    handlerInfos,
                    eventsArray,
                    results,
                    origin,
                    batchContext);
            }
            catch (Exception ex)
            {
                _log.WriteError(
                    "EventDispatcher.DoHandle",
                    "Failed to handle events batch of type " + origin.EventType.Name,
                    ex);
                foreach (var result in results)
                {
                    result.Retry = true;
                    result.RetryDelay = _failedEventRetryDelay;
                }
            }

            //TODO: What if connect is broken and engine failes to aknowledge?..
            for (var i = 0; i < events.Length; i++)
            {
                var result = results[i];
                var acknowledge = events[i].Item2;
                if (result.Retry)
                    acknowledge(result.RetryDelay, false);
                else
                    acknowledge(0, true);
            }
        }

        private void ProcessBatchHandlers(
            IEnumerable<(string, Func<object[], object, CommandHandlingResult[]>)> batchHandlerInfos,
            object[] eventsArray,
            CommandHandlingResult[] results,
            EventOrigin origin,
            object batchContext)
        {
            foreach(var batchHandlerInfo in batchHandlerInfos)
            {
                var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                    "Cqrs handle events",
                    batchHandlerInfo.Item1,
                    origin.EventType.Name,
                    origin.BoundedContext);
                try
                {
                    var handleResults = batchHandlerInfo.Item2(eventsArray, batchContext);
                    if (handleResults.Length != results.Length)
                        _log.WriteWarning(batchHandlerInfo.Item1, origin.EventType.Name, "Number of results is not equal to number of events!");
                    for (int i = 0; i < handleResults.Length; ++i)
                    {
                        if (!handleResults[i].Retry)
                            continue;

                        if (results[i].Retry)
                            results[i].RetryDelay = Math.Min(results[i].RetryDelay, handleResults[i].RetryDelay);
                        else
                            results[i] = handleResults[i];
                    }
                }
                catch (Exception ex)
                {
                    _log.WriteError(batchHandlerInfo.Item1, origin.EventType.Name, ex);

                    foreach (var result in results)
                    {
                        result.Retry = true;
                        result.RetryDelay = _failedEventRetryDelay;
                    }

                    TelemetryHelper.SubmitException(telemtryOperation, ex);
                    return;
                }
                finally
                {
                    TelemetryHelper.SubmitOperationResult(telemtryOperation);
                }
            }
        }

        private void ProcessHandlers(
            IEnumerable<EventHandlerInfo> handlerInfos,
            object[] eventsArray,
            CommandHandlingResult[] results,
            EventOrigin origin,
            object batchContext)
        {
            for(int i = 0; i < eventsArray.Length; ++i)
            {
                var @event = eventsArray[i];
                foreach (var handlerInfo in handlerInfos)
                {
                    var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                        "Cqrs handle events",
                        handlerInfo.HandlerTypeName,
                        origin.EventType.Name,
                        origin.BoundedContext);
                    try
                    {
                        var handleResult = _eventInterceptorsProcessor.RunInterceptorsAsync(
                                @event,
                                handlerInfo.HandlerObject,
                                handlerInfo.CommandSender,
                                (e, s) => handlerInfo.Handler(e, s, batchContext))
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult();
                        if (handleResult.Retry)
                        {
                            if (results[i].Retry)
                                results[i].RetryDelay = Math.Min(results[i].RetryDelay, handleResult.RetryDelay);
                            else
                                results[i] = handleResult;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.WriteError(handlerInfo.HandlerTypeName, origin.EventType.Name, ex);

                        results[i].Retry = true;
                        results[i].RetryDelay = _failedEventRetryDelay;

                        TelemetryHelper.SubmitException(telemtryOperation, ex);
                        break;
                    }
                    finally
                    {
                        TelemetryHelper.SubmitOperationResult(telemtryOperation);
                    }
                }
            }
        }
    }
}
