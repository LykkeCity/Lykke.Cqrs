using System;
using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration
{
    /// <summary>
    /// Base fluent API interface for registration internal usage.
    /// </summary>
    [PublicAPI]
    public interface IDescriptor<in TSubject> : IHideObjectMembers
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        IEnumerable<Type> GetDependencies();

        [EditorBrowsable(EditorBrowsableState.Never)]
        void Create(TSubject subject, IDependencyResolver resolver);

        [EditorBrowsable(EditorBrowsableState.Never)]
        void Process(TSubject subject, CqrsEngine cqrsEngine);
    }
}