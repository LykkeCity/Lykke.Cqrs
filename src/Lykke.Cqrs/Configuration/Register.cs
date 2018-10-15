using System;
using JetBrains.Annotations;
using Lykke.Cqrs.Abstractions.Middleware;
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
        /// Creates default endpoint resolver registration using provided resolver type.
        /// </summary>
        /// <typeparam name="TResolver">Endpoint resolver type.</typeparam>
        /// <returns>IRegistration interface for endpoint resolver registration.</returns>
        public static IRegistration DefaultEndpointResolver<TResolver>()
            where TResolver : IEndpointResolver
        {
            return new DefaultEndpointResolverRegistration(typeof(TResolver));
        }

        /// <summary>
        /// Creates default endpoint resolver registration using provided resolver instance.
        /// </summary>
        /// <param name="resolver"><see cref="IEndpointResolver"/> implementation.</param>
        /// <returns>IRegistration interface for endpoint resolver registration.</returns>
        public static IRegistration DefaultEndpointResolver(IEndpointResolver resolver)
        {
            return new DefaultEndpointResolverRegistration(resolver);
        }

        /// <summary>
        /// Creates command interceptors middleware registration.
        /// </summary>
        /// <param name="commandInterceptors">Array of <see cref="ICommandInterceptor"/> implementations.</param>
        /// <returns>IRegistration interface for command interceptors middleware registration.</returns>
        public static IRegistration CommandInterceptors(params ICommandInterceptor[] commandInterceptors)
        {
            return new CommandsInterceptorsRegistration(commandInterceptors);
        }

        /// <summary>
        /// Creates command interceptors middleware registration.
        /// </summary>
        /// <param name="commandInterceptors">Array of <see cref="ICommandInterceptor"/> implementation types.</param>
        /// <returns>IRegistration interface for command interceptors middleware registration.</returns>
        public static IRegistration CommandInterceptors(params Type[] commandInterceptors)
        {
            return new CommandsInterceptorsRegistration(commandInterceptors);
        }

        /// <summary>
        /// Creates command interceptor middleware registration.
        /// </summary>
        /// <typeparam name="T">Type of <see cref="ICommandInterceptor"/> implementation</typeparam>
        /// <returns>IRegistration interface for command interceptors middleware registration.</returns>
        public static IRegistration CommandInterceptor<T>()
            where T : ICommandInterceptor
        {
            return new CommandsInterceptorsRegistration(new [] {typeof(T)});
        }

        /// <summary>
        /// Creates event interceptors middleware registration.
        /// </summary>
        /// <param name="eventInterceptors">Array of <see cref="IEventInterceptor"/> implementations.</param>
        /// <returns>IRegistration interface for event interceptors middleware registration.</returns>
        public static IRegistration EventInterceptors(params IEventInterceptor[] eventInterceptors)
        {
            return new EventInterceptorsRegistration(eventInterceptors);
        }

        /// <summary>
        /// Creates event interceptors middleware registration.
        /// </summary>
        /// <param name="eventInterceptors">Array of <see cref="IEventInterceptor"/> implementation types.</param>
        /// <returns>IRegistration interface for event interceptors middleware registration.</returns>
        public static IRegistration EventInterceptors(params Type[] eventInterceptors)
        {
            return new EventInterceptorsRegistration(eventInterceptors);
        }

        /// <summary>
        /// Creates event interceptor middleware registration.
        /// </summary>
        /// <typeparam name="T">Type of <see cref="IEventInterceptor"/> implementation</typeparam>
        /// <returns>IRegistration interface for event interceptors middleware registration.</returns>
        public static IRegistration EventInterceptor<T>()
            where T : IEventInterceptor
        {
            return new EventInterceptorsRegistration(new[] { typeof(T) });
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