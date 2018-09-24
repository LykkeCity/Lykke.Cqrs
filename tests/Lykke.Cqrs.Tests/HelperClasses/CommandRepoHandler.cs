using System;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal interface IInt64Repo
    {
    }

    internal class Int64Repo : IInt64Repo, IDisposable
    {
        public void Dispose()
        {
            IsDisposed = true;
        }

        public bool IsDisposed { get; set; }
    }

    internal class CommandRepoHandler : CommandsHandler
    {
        [UsedImplicitly]
        public void Handle(Int64 command, IInt64Repo repo)
        {
            HandledCommands.Add(command);
        }
    }
}