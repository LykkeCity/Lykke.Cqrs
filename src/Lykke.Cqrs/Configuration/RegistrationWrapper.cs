using System;
using System.Collections.Generic;

namespace Lykke.Cqrs.Configuration
{
    public abstract class RegistrationWrapper<T> : IRegistrationWrapper<T>
        where T : IRegistration
    {
        private readonly T m_Registration;

        T IRegistrationWrapper<T>.Registration => m_Registration;

        IEnumerable<Type> IRegistration.Dependencies => m_Registration.Dependencies;

        protected RegistrationWrapper(T registration)
        {
            m_Registration = registration;
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            m_Registration.Create(cqrsEngine);
        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            m_Registration.Process(cqrsEngine);
        }
    }
}