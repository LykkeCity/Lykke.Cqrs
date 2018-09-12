using System;
using JetBrains.Annotations;

namespace Lykke.Cqrs
{
    [PublicAPI]
    public class EventsPublisher : IEventPublisher
    {
        private readonly CqrsEngine m_CqrsEngine;
        private readonly Context m_Context;

        /// <summary>
        /// C-tor.
        /// </summary>
        public EventsPublisher(CqrsEngine cqrsEngine, Context context)
        {
            m_Context = context;
            m_CqrsEngine = cqrsEngine;
        }

        /// <inheritdoc cref="IEventPublisher"/>
        public void PublishEvent(object @event)
        {
            if (@event == null)
                throw new ArgumentNullException();
            m_CqrsEngine.PublishEvent(@event, m_Context.Name);
        }
    }
}