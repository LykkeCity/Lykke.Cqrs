using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lykke.Messaging.Contract;
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

        public void Handle(
            Func<object[], object, CommandHandlingResult[]>[] handlers,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin)
        {
            if(!events.Any())
                return;

            if (m_BatchSize == 0 && ApplyTimeout == 0)
            {
                DoHandle(handlers, events, origin, null);
                return;
            }

            lock (m_Events)
            {
                m_Events.Add(batchContext => DoHandle(handlers, events, origin, batchContext));
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

        private void DoHandle(
            Func<object[], object, CommandHandlingResult[]>[] handlers,
            Tuple<object, AcknowledgeDelegate>[] events,
            EventOrigin origin,
            object batchContext)
        {
            //TODO: What if connect is broken and engine failes to aknowledge?..
            CommandHandlingResult[] results;
            try
            {
                var eventsArray = @events.Select(e => e.Item1).ToArray();
                var handleResults = new CommandHandlingResult[handlers.Length][];
                for(int i = 0; i < handlers.Length; ++i)
                {
                    var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                        "Cqrs handle events",
                        origin.EventType.Name,
                        origin.BoundedContext,
                        batchContext?.ToString());
                    try
                    {
                        handleResults[i] = handlers[i](eventsArray, batchContext);
                    }
                    catch (Exception ex)
                    {
                        TelemetryHelper.SubmitException(telemtryOperation, ex);
                        throw;
                    }
                    finally
                    {
                        TelemetryHelper.SubmitOperationResult(telemtryOperation);
                    }
                }

                results = Enumerable
                    .Range(0, eventsArray.Length)
                    .Select(i =>
                    {
                        var r = handleResults.Select(h => h[i]).ToArray();
                        var retry = r.Any(res => res.Retry);
                        return new CommandHandlingResult()
                        {
                            Retry = retry,
                            RetryDelay = r.Where(res => !retry || res.Retry).Min(res => res.RetryDelay)
                        };
                    })
                    .ToArray();

                //TODO: verify number of results matches number of events
            }
            catch (Exception e)
            {
                _log.WriteErrorAsync(
                    nameof(EventDispatcher),
                    nameof(DoHandle),
                    "Failed to handle events batch of type " + origin.EventType.Name,
                    e);
                results = @events
                    .Select(x => new CommandHandlingResult { Retry = true, RetryDelay = m_FailedEventRetryDelay })
                    .ToArray();
            }

            for (var i = 0; i < events.Length; i++)
            {
                var result = results[i];
                var acknowledge = events[i].Item2;
                if (result.Retry)
                    acknowledge(result.RetryDelay, !result.Retry);
                else
                    acknowledge(0, true);
            }
        }
    }
}
