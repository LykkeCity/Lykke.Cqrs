using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Lykke.Cqrs.Tests
{
    internal class EventHandler
    {
        private readonly bool _fail;

        internal readonly List<object> HandledEvents = new List<object>();

        internal bool FailOnce { get; set; }

        internal EventHandler(bool fail = false)
        {
            _fail = fail;
        }

        [UsedImplicitly]
        internal void Handle(string e)
        {
            HandledEvents.Add(e);
            if (_fail || FailOnce)
            {
                FailOnce = false;
                throw new Exception();
            }
        }

        [UsedImplicitly]
        internal void Handle(DateTime e)
        {
            HandledEvents.Add(e);
            if (_fail || FailOnce)
            {
                FailOnce = false;
                throw new Exception();
            }
        }

        [UsedImplicitly]
        internal void Handle(int e)
        {
            HandledEvents.Add(e);
            if (_fail || FailOnce)
            {
                FailOnce = false;
                throw new Exception();
            }
        }
    }
}