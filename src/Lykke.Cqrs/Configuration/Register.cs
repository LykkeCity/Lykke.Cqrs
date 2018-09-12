using JetBrains.Annotations;
using Lykke.Cqrs.Configuration.BoundedContext;
using Lykke.Cqrs.Configuration.Routing;
using Lykke.Cqrs.Configuration.Saga;

namespace Lykke.Cqrs.Configuration
{
    [PublicAPI]
    public static class Register
    {
        /// <summary>
        /// Provides fluent API for default routing registration.
        /// </summary>
        public static IDefaultRoutingRegistration DefaultRouting => new DefaultRoutingRegistration();

        /// <summary>
        /// Provides fluent API for default endpoint resolver registration using provided resolver type.
        /// </summary>
        /// <typeparam name="TResolver">Endpoint resolver type.</typeparam>
        /// <returns>Fluent API interface for endpoint resolver registration.</returns>
        public static DefaultEndpointResolverRegistration DefaultEndpointResolver<TResolver>()
            where TResolver : IEndpointResolver
        {
            return new DefaultEndpointResolverRegistration(typeof(TResolver));
        }

        /// <summary>
        /// Provides fluent API for default endpoint resolver registration usin provided resolver instance.
        /// </summary>
        /// <param name="resolver"><see cref="IEndpointResolver"/> implementation.</param>
        /// <returns>Fluent API interface for endpoint resolver registration.</returns>
        public static DefaultEndpointResolverRegistration DefaultEndpointResolver(IEndpointResolver resolver)
        {
            return new DefaultEndpointResolverRegistration(resolver);
        }

        /// <summary>
        /// Provides fluent API for saga registration.
        /// </summary>
        /// <typeparam name="TSaga">Saga type.</typeparam>
        /// <param name="name">Bounded context name.</param>
        /// <returns>Fluent API interface for saga registration.</returns>
        public static ISagaRegistration Saga<TSaga>(string name)
        {
            return new SagaRegistration(name, typeof(TSaga));
        }

        /// <summary>
        /// Provides fluent API for bounded context registration.
        /// </summary>
        /// <param name="name">Bounded context name.</param>
        /// <returns>Fluent API interface for bounded context registration.</returns>
        public static IBoundedContextRegistration BoundedContext(string name)
        {
            return new BoundedContextRegistration(name);
        }
    }
}