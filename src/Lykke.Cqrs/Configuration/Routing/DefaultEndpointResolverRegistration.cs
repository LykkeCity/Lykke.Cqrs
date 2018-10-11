using System;
using System.Collections.Generic;
using System.Linq;

namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Default endpoint resolver registration class.
    /// </summary>
    internal class DefaultEndpointResolverRegistration : IRegistration, IHideObjectMembers
    {
        private IEndpointResolver m_Resolver;

        /// <inheritdoc cref="IRegistration"/>
        public IEnumerable<Type> Dependencies { get; private set; }

        /// <summary>
        /// C-tor.
        /// </summary>
        /// <param name="resolver"><see cref="IEndpointResolver"/> implementation.</param>
        public DefaultEndpointResolverRegistration(IEndpointResolver resolver)
        {
            m_Resolver = resolver;
            Dependencies = new Type[0];
        }

        /// <summary>
        /// C-tor.
        /// </summary>
        /// <param name="resolverType"><see cref="IEndpointResolver"/> implementation type.</param>
        public DefaultEndpointResolverRegistration(Type resolverType)
        {
            Dependencies = new []{resolverType};
        }

        /// <inheritdoc cref="IRegistration"/>
        public void Create(CqrsEngine cqrsEngine)
        {
            if (m_Resolver == null)
            {
                m_Resolver = cqrsEngine.DependencyResolver.GetService(Dependencies.First()) as IEndpointResolver;
            }

            cqrsEngine.EndpointResolver = m_Resolver;
        }

        /// <inheritdoc cref="IRegistration"/>
        public void Process(CqrsEngine cqrsEngine)
        {
        }
    }
}