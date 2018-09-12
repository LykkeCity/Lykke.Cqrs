using System;
using Lykke.Cqrs.Configuration.Routing;

namespace Lykke.Cqrs.Configuration.BoundedContext
{
    public class BoundedContextRegistration : ContextRegistrationBase<IBoundedContextRegistration>, IBoundedContextRegistration
    {
        public long FailedCommandRetryDelayInternal { get; set; }

        public bool HasEventStore { get; set; }

        public BoundedContextRegistration(string name):base(name)
        {
            FailedCommandRetryDelayInternal = 60000;
        }

        protected override Context CreateContext(CqrsEngine cqrsEngine)
        {
            return new Context(cqrsEngine, Name, FailedCommandRetryDelayInternal);
        }

        public IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(params Type[] commandTypes)
        {
            return AddDescriptor(new ListeningCommandsDescriptor<IBoundedContextRegistration>(this, commandTypes));
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(params Type[] eventTypes)
        {
            return AddDescriptor(new PublishingEventsDescriptor<IBoundedContextRegistration>(this, eventTypes));
        }
 
        public IBoundedContextRegistration FailedCommandRetryDelay(long delay)
        {
            if (delay < 0)
                throw new ArgumentException("Delay value must be greater or equal to 0", nameof(delay));
            FailedCommandRetryDelayInternal = delay;
            return this;
        }

        public IBoundedContextRegistration FailedCommandRetryDelay(TimeSpan delay)
        {
            if (delay.Ticks < 0)
                throw new ArgumentException("Delay value must be non-negative", nameof(delay));
            FailedCommandRetryDelayInternal = (long)delay.TotalMilliseconds;
            return this;
        }

        public IBoundedContextRegistration WithCommandsHandler(object handler)
        {
            if (handler == null)
                throw new ArgumentNullException();
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }

        public IBoundedContextRegistration WithCommandsHandler<T>()
        {
            AddDescriptor(new CommandsHandlerDescriptor(typeof(T)));
            return this;
        }

        public IBoundedContextRegistration WithCommandsHandlers(params Type[] handlers)
        {
            AddDescriptor(new CommandsHandlerDescriptor(handlers));
            return this;
        }

        public IBoundedContextRegistration WithCommandsHandler(Type handler)
        {
            if (handler == null)
                throw new ArgumentNullException();
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }

        public IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(
            TProjection projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Func<TProjection, TBatchContext> beforeBatchApply = null,
            Action<TProjection, TBatchContext> afterBatchApply = null)
        {
            RegisterProjections(
                projection,
                fromBoundContext,
                batchSize,
                applyTimeoutInSeconds,
                beforeBatchApply,
                afterBatchApply);
            return this;
        }

        public IBoundedContextRegistration WithProjection(
            Type projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Type batchContextType = null,
            Func<object, object> beforeBatchApply = null,
            Action<object, object> afterBatchApply = null)
        {
            RegisterProjections(
                projection,
                fromBoundContext,
                batchSize,
                applyTimeoutInSeconds,
                batchContextType,
                beforeBatchApply,
                afterBatchApply);
            return this;
        }

        public IBoundedContextRegistration WithProjection(
            object projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Type batchContextType = null,
            Func<object, object> beforeBatchApply = null,
            Action<object, object> afterBatchApply = null)
        {
            RegisterProjections(
                projection,
                fromBoundContext,
                batchSize,
                applyTimeoutInSeconds,
                batchContextType,
                beforeBatchApply,
                afterBatchApply);
            return this;
        }

        public IBoundedContextRegistration WithProjection<TProjection, TBatchContext>(
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Func<TProjection, TBatchContext> beforeBatchApply = null,
            Action<TProjection, TBatchContext> afterBatchApply = null)
        {
            Func<object, object> beforeApply = (beforeBatchApply == null)
              ? (Func<object, object>)null
              : o => beforeBatchApply((TProjection)o);
            Action<object, object> afterApply = (afterBatchApply == null)
                ? (Action<object, object>)null
                : (o, c) => afterBatchApply((TProjection)o, (TBatchContext)c);

            RegisterProjections(
                typeof(TProjection),
                fromBoundContext,
                batchSize,
                applyTimeoutInSeconds,
                typeof(TBatchContext),
                beforeApply,
                afterApply);
            return this;
        }

        protected void RegisterProjections<TProjection, TBatchContext>(
            TProjection projection,
            string fromBoundContext,
            int batchSize,
            int applyTimeoutInSeconds,
            Func<TProjection, TBatchContext> beforeBatchApply,
            Action<TProjection, TBatchContext> afterBatchApply)
        {
            if (projection == null)
                throw new ArgumentNullException(nameof(projection));
            Func<object, object> beforeApply = (beforeBatchApply == null) 
                ? (Func<object, object>)null 
                : o => beforeBatchApply((TProjection)o);
            Action<object,object> afterApply = (afterBatchApply == null) 
                ? (Action<object,object>)null 
                : (o,c) => afterBatchApply((TProjection)o,(TBatchContext)c);

            AddDescriptor(
                new ProjectionDescriptor(
                    projection,
                    fromBoundContext,
                    batchSize,
                    applyTimeoutInSeconds,
                    beforeApply,
                    afterApply,
                    typeof(TBatchContext)));
        }

        protected void RegisterProjections(
            Type projection,
            string fromBoundContext,
            int batchSize = 0,
            int applyTimeoutInSeconds = 0,
            Type batchContextType = null,
            Func<object, object> beforeBatchApply = null,
            Action<object, object> afterBatchApply = null)
        {
            if (projection == null)
                throw new ArgumentNullException(nameof(projection));
            AddDescriptor(
                new ProjectionDescriptor(
                    projection,
                    fromBoundContext,
                    batchSize,
                    applyTimeoutInSeconds,
                    beforeBatchApply,
                    afterBatchApply,
                    batchContextType));
        }

       protected void RegisterProjections(
           object projection,
           string fromBoundContext,
           int batchSize = 0,
           int applyTimeoutInSeconds = 0,
           Type batchContextType = null,
           Func<object, object> beforeBatchApply = null,
           Action<object, object> afterBatchApply = null)
        {
            if (projection == null)
                throw new ArgumentNullException(nameof(projection));
            AddDescriptor(
                new ProjectionDescriptor(
                    projection,
                    fromBoundContext,
                    batchSize,
                    applyTimeoutInSeconds,
                    beforeBatchApply,
                    afterBatchApply,
                    batchContextType));
        }

        public IBoundedContextRegistration WithProcess(object process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public IBoundedContextRegistration WithProcess(Type process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public IBoundedContextRegistration WithProcess<TProcess>()
            where TProcess : IProcess
        {
            return WithProcess(typeof(TProcess));
        }
    }
}