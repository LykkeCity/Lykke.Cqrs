using System;
using System.Threading;

namespace Lykke.Cqrs.Tests
{
    internal class TestProcess : IProcess
    {
        private readonly Thread _workerThread;

        private ICommandSender m_CommandSender;

        internal ManualResetEvent Started = new ManualResetEvent(false);
        internal ManualResetEvent Disposed = new ManualResetEvent(false);

        internal TestProcess()
        {
            _workerThread = new Thread(SendCommands);
        }

        private void SendCommands(object obj)
        {

            int i = 0;
            do
            {
                m_CommandSender.SendCommand("command #" + i++, "local");
            } while (!Disposed.WaitOne(100));

        }

        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
        {
            m_CommandSender = commandSender;
            _workerThread.Start();
            Console.WriteLine("Test process started");
            Started.Set();
        }

        public void Dispose()
        {
            Console.WriteLine("Test process disposed");
            Disposed.Set();
        }
    }
}