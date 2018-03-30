using KpSocket.Tcp;
using System.Collections.Concurrent;
using System.Threading;

namespace KpSocket.Utils
{
    sealed class ReceiveWorker
    {
        private readonly ConcurrentQueue<TcpSession> m_Items;
        private readonly Thread m_Thread;
        private volatile bool m_IsRuning;

        public ReceiveWorker()
        {
            m_Items = new ConcurrentQueue<TcpSession>();
            m_Thread = new Thread(Dispatcher);
            m_Thread.IsBackground = true;
            m_IsRuning = true;
            m_Thread.Start();
        }

        public void AddItem(TcpSession session)
        {
            if (m_IsRuning == false) return;

            m_Items.Enqueue(session);
        }

        public void WaitShutdown()
        {
            m_IsRuning = false;
            m_Thread.Join();
        }

        private void Dispatcher()
        {
            TcpSession session;

            while (m_IsRuning || !m_Items.IsEmpty)
            {
                if (m_Items.TryDequeue(out session))
                {
                    session.AsyncReceive();
                    continue;
                }
                Thread.Sleep(1);
            }
        }
    }
}