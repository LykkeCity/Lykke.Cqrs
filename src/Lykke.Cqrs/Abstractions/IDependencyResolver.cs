using System;
using JetBrains.Annotations;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for dependency resolving from used components container.
    /// </summary>
    [PublicAPI]
    public interface IDependencyResolver
    {
        /// <summary>
        /// Resolves component by provided type.
        /// </summary>
        /// <param name="type">Component type.</param>
        /// <returns>Registered in container component instance of provided type.</returns>
        object GetService(Type type);
    }
}