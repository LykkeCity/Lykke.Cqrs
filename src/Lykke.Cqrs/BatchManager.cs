using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lykke.Messaging.Contract;
using Common;
using Common.Log;
using Lykke.Cqrs.Utils;

namespace Lykke.Cqrs
{
    internal class BatchManager
    {
        private readonly List<Action<object>> m_Events = new List<Action<object>>();
        private readonly int m_BatchSize;
        private readonly ILog _log;
        private readonly long m_FailedEventRetryDelay;
        private readonly Stopwatch m_SinceFirstEvent = new Stopwatch();

        private long m_Counter = 0;
        private Func<object> m_BeforeBatchApply;
        private Action<object> m_AfterBatchApply;

        public long ApplyTimeout { get; private set; }

        public BatchManager(
            ILog log,
            long failedEventRetryDelay,
            int batchSize = 0,
            long applyTimeout = 0,
            Func<object> beforeBatchApply = null,
            Action<object> afterBatchApply = null)
        {
            m_AfterBatchApply = afterBatchApply??(o => {});
            m_BeforeBatchApply = beforeBatchApply ?? (() =>  null );
            _log = log;
            m_FailedEventRetryDelay = failedEventRetryDelay;
            ApplyTimeout = applyTimeout;
            m_BatchSize = batchSize;
        }

        public void BatchHandle(
            (string, Func<object[], object, CommandHandlingResult[]>)[] batchHandlerInfos,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin)
        {
            if(events.Length == 0)
                return;

            if (m_BatchSize == 0 && ApplyTimeout == 0)
            {
                DoBatchHandle(
                    batchHandlerInfos,
                    events,
                    origin,
                    null);
                return;
            }

            lock (m_Events)
            {
                m_Events.Add(batchContext => DoBatchHandle(
                    batchHandlerInfos,
                    events,
                    origin,
                    batchContext));
                if (m_Counter == 0 && ApplyTimeout != 0)
                    m_SinceFirstEvent.Start();
                m_Counter += events.Length;
                ApplyBatchIfRequired();
            }
        }

        public void Handle(
            (string, Func<object, object, CommandHandlingResult>)[] handlerInfos,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin)
        {
            if (events.Length == 0)
                return;

            if (m_BatchSize == 0 && ApplyTimeout == 0)
            {
                DoHandle(
                    handlerInfos,
                    events,
                    origin,
                    null);
                return;
            }

            lock (m_Events)
            {
                m_Events.Add(batchContext => DoHandle(
                    handlerInfos,
                    events,
                    origin,
                    batchContext));
                if (m_Counter == 0 && ApplyTimeout != 0)
                    m_SinceFirstEvent.Start();
                m_Counter += events.Length;
                ApplyBatchIfRequired();
            }
        }

        internal void ApplyBatchIfRequired(bool force = false)
        {
            Action<object>[] handles = new Action<object>[0];

            lock (m_Events)
            {
                if (m_Counter == 0)
                    return;

                if ((m_Counter >= m_BatchSize && m_BatchSize != 0)
                    || (m_SinceFirstEvent.ElapsedMilliseconds > ApplyTimeout && ApplyTimeout != 0)
                    || force)
                {
                    handles = m_Events.ToArray();
                    m_Events.Clear();
                    m_Counter = 0;
                    m_SinceFirstEvent.Reset();
                }
            }
            if (!handles.Any())
                return;

            var batchContext = m_BeforeBatchApply();
            foreach (var handle in handles)
            {
                handle(batchContext);
            }
            m_AfterBatchApply(batchContext);
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
                _log.WriteErrorAsync(
                    nameof(EventDispatcher),
                    nameof(DoHandle),
                    "Failed to handle events batch of type " + origin.EventType.Name,
                    ex);
                foreach (var result in results)
                {
                    result.Retry = true;
                    result.RetryDelay = m_FailedEventRetryDelay;
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
            (string, Func<object, object, CommandHandlingResult>)[] handlerInfos,
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
                _log.WriteErrorAsync(
                    nameof(EventDispatcher),
                    nameof(DoHandle),
                    "Failed to handle events batch of type " + origin.EventType.Name,
                    ex);
                foreach (var result in results)
                {
                    result.Retry = true;
                    result.RetryDelay = m_FailedEventRetryDelay;
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
                        _log.WriteWarningAsync(batchHandlerInfo.Item1, origin.EventType.Name, eventsArray.ToJson(), $"Number of results is not equal to number of events!")
                            .GetAwaiter().GetResult();
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
                    TelemetryHelper.SubmitException(telemtryOperation, ex);
                    foreach (var result in results)
                    {
                        result.Retry = true;
                        result.RetryDelay = m_FailedEventRetryDelay;
                    }
                    _log.WriteErrorAsync(batchHandlerInfo.Item1, origin.EventType.Name, eventsArray.ToJson(), ex)
                        .GetAwaiter().GetResult();
                    return;
                }
                finally
                {
                    TelemetryHelper.SubmitOperationResult(telemtryOperation);
                }
            }
        }

        private void ProcessHandlers(
            IEnumerable<(string, Func<object, object, CommandHandlingResult>)> handlerInfos,
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
                        handlerInfo.Item1,
                        origin.EventType.Name,
                        origin.BoundedContext);
                    try
                    {
                        var handleResult = handlerInfo.Item2(@event, batchContext);
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
                        TelemetryHelper.SubmitException(telemtryOperation, ex);
                        results[i].Retry = true;
                        results[i].RetryDelay = m_FailedEventRetryDelay;
                        _log.WriteErrorAsync(handlerInfo.Item1, origin.EventType.Name, @event.ToJson(), ex)
                            .GetAwaiter().GetResult();
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
