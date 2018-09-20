using Castle.MicroKernel;

namespace Lykke.Cqrs.Castle
{
    //TODO: should be injected by cqrs with preset local BC
    internal class CommandSender : ICommandSender
    {
        private ICqrsEngine m_Engine;
        private IKernel m_Kernel;
        private object m_SyncRoot = new object();

        public CommandSender(IKernel kernel)
        {
            m_Kernel = kernel;
        }

        private ICqrsEngine CqrsEngine
        {
            get
            {
                if (m_Engine == null)
                {
                    lock (m_SyncRoot)
                    {
                        if (m_Engine == null)
                        {
                            m_Engine = m_Kernel.Resolve<ICqrsEngine>();
                        }
                    }
                }
                return m_Engine;
            }
        }

        public void Dispose()
        {
        }

        public void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0)
        {
            CqrsEngine.SendCommand(command, boundedContext, remoteBoundedContext, priority);
        }

        public void SendCommand<T>(T command, string remoteBoundedContext, uint priority = 0)
        {
            CqrsEngine.SendCommand(command, null, remoteBoundedContext, priority);
        }
    }
}