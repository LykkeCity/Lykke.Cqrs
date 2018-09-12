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

        /// <summary>
        /// Checks whether provided type is registered on components container.
        /// </summary>
        /// <param name="type">Component type.</param>
        /// <returns>True if specified type is registered, false - otherwise.</returns>
        bool HasService(Type type);
    }
}