using System;

namespace Lykke.Cqrs
{
    internal class DefaultDependencyResolver : IDependencyResolver
    {
        public object GetService(Type type)
        {
            return Activator.CreateInstance(type);
        }
    }
}