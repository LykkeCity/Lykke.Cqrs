using System;
using JetBrains.Annotations;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for sending commands.
    /// </summary>
    [PublicAPI]
    public interface ICommandSender : IDisposable
    {
        /// <summary>
        /// Sends command via messaging engine to provided context.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="command">Command instance.</param>
        /// <param name="remoteBoundedContext">Target context.</param>
        /// <param name="priority">Command priority.</param>
        void SendCommand<T>([NotNull] T command, [NotNull] string remoteBoundedContext, uint priority = 0);
    }
}