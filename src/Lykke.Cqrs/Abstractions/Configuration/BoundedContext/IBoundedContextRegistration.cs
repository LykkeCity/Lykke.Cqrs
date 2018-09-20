using System;
using Lykke.Cqrs.Configuration.Routing;

namespace Lykke.Cqrs.Configuration.BoundedContext
{
    public interface IBoundedContextRegistration : IRegistration, IHideObjectMembers
    {
        string Name { get; }
        bool HasEventStore { get; }
 
        IBoundedContextRegistration FailedCommandRetryDelay(long delay);
        IPublishingCommandsDescriptor<IBoundedContextRegistration> PublishingCommands(params Type[] commandsTypes);

        IListeningEventsDescriptor<IBoundedContextRegistration> ListeningEvents(params Type[] type);

        IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(params Type[] type);
        IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(params Type[] type);

        ProcessingOptionsDescriptor<IBoundedContextRegistration> ProcessingOptions(string route);

        IBoundedContextRegistration WithCommandsHandler(object handler);
        IBoundedContextRegistration WithCommandsHandler<T>();
        IBoundedContextRegistration WithCommandsHandlers(params Type[] handlers);
        IBoundedContextRegistration WithCommandsHandler(Type handler);

        IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(
            TProjection projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Func<TProjection, TBatchContext> beforeBatchApply = null,
            Action<TProjection, TBatchContext> afterBatchApply = null);
        IBoundedContextRegistration WithProjection(
            Type projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Type batchContextType = null,
            Func<object, object> beforeBatchApply = null,
            Action<object, object> afterBatchApply = null);
        IBoundedContextRegistration WithProjection(
            object projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Type batchContextType = null,
            Func<object, object> beforeBatchApply = null,
            Action<object, object> afterBatchApply = null);
        IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Func<TProjection, TBatchContext> beforeBatchApply = null,
            Action<TProjection, TBatchContext> afterBatchApply = null);

        IBoundedContextRegistration WithProcess(object process);
        IBoundedContextRegistration WithProcess(Type process);
        IBoundedContextRegistration WithProcess<TProcess>() where TProcess : IProcess;
    }
}