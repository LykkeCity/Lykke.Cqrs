using JetBrains.Annotations;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for cqrs engine bootstrapping.
    /// </summary>
    [PublicAPI]
    public interface ICqrsEngineBootstrapper
    {
        /// <summary>
        /// Starts bootstrapping.
        /// </summary>
        void Start();
    }
}