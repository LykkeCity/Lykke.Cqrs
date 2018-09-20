using System;
using System.Collections.Generic;

namespace Lykke.Cqrs.Configuration
{
    public abstract class RegistrationWrapper<T> : IRegistrationWrapper<T>
        where T : IRegistration
    {
        private readonly T m_Registration;

        T IRegistrationWrapper<T>.Registration { get { return m_Registration; } }

        protected RegistrationWrapper(T registration)
        {
            m_Registration = registration;
        }

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return m_Registration.Dependencies; }
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