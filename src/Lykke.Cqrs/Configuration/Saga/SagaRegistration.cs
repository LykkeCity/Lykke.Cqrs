using System;

namespace Lykke.Cqrs.Configuration.Saga
{
    public class SagaRegistration : ContextRegistrationBase<ISagaRegistration>, ISagaRegistration
    {
        public SagaRegistration(string name, Type type) : base(name)
        {
            AddHandlerDescriptor(new SagaDescriptor(type));
        }

        protected override Context CreateContext(CqrsEngine cqrsEngine)
        {
            return new Context(cqrsEngine, Name, 0);
        }
    }
}