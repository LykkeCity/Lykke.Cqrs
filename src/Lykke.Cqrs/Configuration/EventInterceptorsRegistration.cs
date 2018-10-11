using System;
using System.Collections.Generic;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Configuration
{
    internal class EventInterceptorsRegistration : IRegistration
    {
        private readonly IEventInterceptor[] _eventInterceptors;

        public IEnumerable<Type> Dependencies { get; }

        internal EventInterceptorsRegistration(IEventInterceptor[] eventInterceptors)
        {
            _eventInterceptors = eventInterceptors;
        }

        internal EventInterceptorsRegistration(Type[] eventInterceptors)
        {
            Dependencies = eventInterceptors;
        }

        public void Create(CqrsEngine cqrsEngine)
        {
            if (_eventInterceptors == null)
            {
                foreach (var eventInterceptorType in Dependencies)
                {
                    var eventInterceptorImpl = (IEventInterceptor)cqrsEngine.DependencyResolver.GetService(eventInterceptorType);
                    cqrsEngine.EventInterceptorsProcessor.AddInterceptor(eventInterceptorImpl);
                }
            }
            else
            {
                foreach (var eventInterceptor in _eventInterceptors)
                {
                    cqrsEngine.EventInterceptorsProcessor.AddInterceptor(eventInterceptor);
                }
            }
        }

        public void Process(CqrsEngine cqrsEngine)
        {
        }
    }
}
