using System;
using Castle.MicroKernel;

namespace Lykke.Cqrs.Castle
{
    internal class CastleDependencyResolver : IDependencyResolver
    {
        private readonly IKernel m_Kernel;

        public CastleDependencyResolver(IKernel kernel)
        {
            m_Kernel = kernel ?? throw new ArgumentNullException("kernel");
        }

        public object GetService(Type type)
        {
            return m_Kernel.Resolve(type);
        }

        public bool HasService(Type type)
        {
            return m_Kernel.HasComponent(type);
        }
    }
}