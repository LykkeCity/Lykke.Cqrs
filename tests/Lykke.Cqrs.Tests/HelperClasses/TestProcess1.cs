using System.Threading;

namespace Lykke.Cqrs.Tests
{
    internal class TestProcess1 : IProcess
    {
        private readonly ManualResetEvent m_Disposed = new ManualResetEvent(false);
        private readonly Thread m_WorkerThread;
        private ICommandSender m_CommandSender;

        internal TestProcess1()
        {
            m_WorkerThread = new Thread(SendCommands);
        }

        private void SendCommands(object obj)
        {

            int i = 0;
            while (!m_Disposed.WaitOne(100))
            {
                m_CommandSender.SendCommand("command #" + i++, "local");
            }
        }

        public void Start(ICommandSender commandSender, IEventPublisher eventPublisher)
        {
            m_CommandSender = commandSender;
            m_WorkerThread.Start();
        }

        public void Dispose()
        {
            m_Disposed.Set();
            m_WorkerThread.Join();
        }
    }
}