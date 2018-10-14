using System;
using System.Collections.Generic;
using Lykke.Cqrs.Abstractions.Middleware;

namespace Lykke.Cqrs.Configuration
{
    internal class CommandsInterceptorsRegistration : IRegistration
    {
        private ICommandInterceptor[] _commandInterceptors;

        public IEnumerable<Type> Dependencies { get; }

        internal CommandsInterceptorsRegistration(ICommandInterceptor[] commandInterceptors)
        {
            _commandInterceptors = commandInterceptors;
        }

        internal CommandsInterceptorsRegistration(Type[] commandInterceptors)
        {
            Dependencies = commandInterceptors;
        }

        public void Create(CqrsEngine cqrsEngine)
        {
            if (_commandInterceptors == null)
            {
                foreach (var commandsInterceptorType in Dependencies)
                {
                    var commandInterceptorImpl = (ICommandInterceptor)cqrsEngine.DependencyResolver.GetService(commandsInterceptorType);
                    cqrsEngine.CommandInterceptorsQueue.AddInterceptor(commandInterceptorImpl);
                }
            }
            else
            {
                foreach (var commandInterceptor in _commandInterceptors)
                {
                    cqrsEngine.CommandInterceptorsQueue.AddInterceptor(commandInterceptor);
                }
            }
        }

        public void Process(CqrsEngine cqrsEngine)
        {
        }
    }
}
