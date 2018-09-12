using System;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Configuration
{
    /// <summary>
    /// Interface for base object methods hiding.
    /// </summary>
    [PublicAPI]
    public interface IHideObjectMembers
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        string ToString();

        [EditorBrowsable(EditorBrowsableState.Never)]
        Type GetType();

        [EditorBrowsable(EditorBrowsableState.Never)]
        int GetHashCode();

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool Equals(object obj);
    }
}