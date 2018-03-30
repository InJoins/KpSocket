using KpSocket.Core;
using System.Collections.Concurrent;
using System.Threading;

namespace KpSocket.Utils
{
    sealed class SendWorker
    {
        private readonly ConcurrentQueue<ISession> m_Items;
        private readonly Thread m_Thread;
        private volatile bool m_IsRuning;

        public SendWorker()
        {
            m_Items = new ConcurrentQueue<ISession>();
            m_Thread = new Thread(Dispatcher);
            m_Thread.IsBackground = true;
            m_IsRuning = true;
            m_Thread.Start();
        }

        public void AddItem(ISession session)
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
            ISession session;

            while (m_IsRuning || !m_Items.IsEmpty)
            {
                if (m_Items.TryDequeue(out session))
                {
                    session.Flush();
                    continue;
                }
                Thread.Sleep(SocketRuntime.Instance.SendSleep);
            }
        }
    }
}