using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Lykke.Cqrs.Utils
{
    internal static class TelemetryHelper
    {
        private static readonly TelemetryClient _telemetry = new TelemetryClient();

        internal static IOperationHolder<DependencyTelemetry> InitTelemetryOperation(
            string type,
            string target,
            string name,
            string data)
        {
            var operation = _telemetry.StartOperation<DependencyTelemetry>(name);
            operation.Telemetry.Type = type;
            operation.Telemetry.Target = target;
            operation.Telemetry.Name = name;
            operation.Telemetry.Data = data;

            return operation;
        }

        internal static void SubmitException(IOperationHolder<DependencyTelemetry> telemtryOperation, Exception e)
        {
            telemtryOperation.Telemetry.Success = false;
            _telemetry.TrackException(e);
        }

        internal static void SubmitOperationResult(IOperationHolder<DependencyTelemetry> telemtryOperation)
        {
            _telemetry.StopOperation(telemtryOperation);
        }
    }
}
