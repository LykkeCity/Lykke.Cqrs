using System;
using JetBrains.Annotations;
using Lykke.Cqrs.Configuration.BoundedContext;
using Lykke.Cqrs.Configuration.Routing;
using Lykke.Cqrs.Configuration.Saga;

namespace Lykke.Cqrs.Configuration
{
    [PublicAPI]
    public static class RegistrationWrapperExtensions
    {
        #region  Default Routing
        public static IPublishingCommandsDescriptor<IDefaultRoutingRegistration> PublishingCommands(this IRegistrationWrapper<IDefaultRoutingRegistration> wrapper,
            params Type[] commandsTypes)
        {
            return wrapper.Registration.PublishingCommands(commandsTypes);
        }
        #endregion

        #region Saga
        public static IPublishingCommandsDescriptor<ISagaRegistration> PublishingCommands(this IRegistrationWrapper<ISagaRegistration> wrapper, params Type[] commandsTypes)
        {
            return wrapper.Registration.PublishingCommands(commandsTypes);
        }

        public static IListeningEventsDescriptor<ISagaRegistration> ListeningEvents(this IRegistrationWrapper<ISagaRegistration> wrapper, params Type[] types)
        {
            return wrapper.Registration.ListeningEvents(types);
        }

        public static ProcessingOptionsDescriptor<ISagaRegistration> ProcessingOptions(this IRegistrationWrapper<ISagaRegistration> wrapper, string route)
        {
            return wrapper.Registration.ProcessingOptions(route);
        }

        #endregion

        #region BoundedContext
        public static IPublishingCommandsDescriptor<IBoundedContextRegistration> PublishingCommands(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] commandsTypes)
        {
            return wrapper.Registration.PublishingCommands(commandsTypes);
        }

        public static IListeningEventsDescriptor<IBoundedContextRegistration> ListeningEvents(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] types)
        {
            return wrapper.Registration.ListeningEvents(types);
        }

        public static IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] types)
        {
            return wrapper.Registration.ListeningCommands(types);
        }

        public static IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] types)
        {
            return wrapper.Registration.PublishingEvents(types);
        }

        public static ProcessingOptionsDescriptor<IBoundedContextRegistration> ProcessingOptions(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, string route)
        {
            return wrapper.Registration.ProcessingOptions(route);
        }

        public static IBoundedContextRegistration WithCommandsHandler(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, object handler)
        {
            return wrapper.Registration.WithCommandsHandler(handler);
        }

        public static IBoundedContextRegistration WithCommandsHandler<T>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper)
        {
            return wrapper.Registration.WithCommandsHandler<T>();
        }

        public static IBoundedContextRegistration WithCommandsHandlers(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] handlers)
        {
            return wrapper.Registration.WithCommandsHandlers(handlers);
        }

        public static IBoundedContextRegistration WithCommandsHandler(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Type handler)
        {
            return wrapper.Registration.WithCommandsHandler(handler);
        }
        
        public static IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, TProjection projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0, Func<TProjection, TBatchContext> beforeBatchApply = null, Action<TProjection, TBatchContext> afterBatchApply = null)
        {
            return wrapper.Registration.WithProjection(projection, fromBoundContext, batchSize, applyTimeoutInSeconds, beforeBatchApply, afterBatchApply);
        }

        public static IBoundedContextRegistration WithProjection(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Type projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0, Type batchContextType=null,Func<object,object> beforeBatchApply = null, Action<object,object> afterBatchApply = null)
        {
            return wrapper.Registration.WithProjection(projection, fromBoundContext, batchSize, applyTimeoutInSeconds, batchContextType, beforeBatchApply, afterBatchApply);
        }

        public static IBoundedContextRegistration WithProjection(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, object projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0)
        {
            return wrapper.Registration.WithProjection<object,object>(projection, fromBoundContext, batchSize, applyTimeoutInSeconds);
        }

        public static IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0, Func<TProjection, TBatchContext> beforeBatchApply = null, Action<TProjection, TBatchContext> afterBatchApply = null)
        {
            return wrapper.Registration.WithProjection(fromBoundContext, batchSize, applyTimeoutInSeconds, beforeBatchApply, afterBatchApply);
        }

        public static IBoundedContextRegistration WithProcess(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, object process)
        {
            return wrapper.Registration.WithProcess(process);
        }

        public static IBoundedContextRegistration WithProcess(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Type process)
        {
            return wrapper.Registration.WithProcess(process);
        }

        public static IBoundedContextRegistration WithProcess<TProcess>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper) where TProcess : IProcess
        {
            return wrapper.Registration.WithProcess<TProcess>();
        }

        #endregion
    }
}