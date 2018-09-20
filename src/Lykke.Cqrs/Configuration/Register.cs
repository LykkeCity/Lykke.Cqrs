using Lykke.Cqrs.Configuration.BoundedContext;
using Lykke.Cqrs.Configuration.Routing;
using Lykke.Cqrs.Configuration.Saga;

namespace Lykke.Cqrs.Configuration
{
    public static class Register
    {
        public static ISagaRegistration Saga<TSaga>(string name)
        {
            return new SagaRegistration(name, typeof(TSaga));
        }

        public static IBoundedContextRegistration BoundedContext(string name)
        {
            return new BoundedContextRegistration(name);
        }
        
        public static IDefaultRoutingRegistration DefaultRouting
        {
            get { return new DefaultRoutingRegistration(); }
        }

        public static DefaultEndpointResolverRegistration DefaultEndpointResolver(IEndpointResolver resolver)
        {
            return new DefaultEndpointResolverRegistration(resolver);
        }

        public static DefaultEndpointResolverRegistration DefaultEndpointResolver<TResolver>() 
            where TResolver: IEndpointResolver 
        {
            return new DefaultEndpointResolverRegistration(typeof(TResolver));
        }
    }
}