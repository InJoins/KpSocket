using KpSocket.Tcp;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace KpSocket.Utils
{
    sealed class DataWorker
    {
        private readonly ConcurrentQueue<SocketAsyncEventArgs> m_Items;
        private readonly Thread m_Thread;
        private volatile bool m_IsRuning;

        public DataWorker()
        {
            m_Items = new ConcurrentQueue<SocketAsyncEventArgs>();
            m_Thread = new Thread(Dispatcher);
            m_Thread.IsBackground = true;
            m_IsRuning = true;
            m_Thread.Start();
        }

        public void AddItem(SocketAsyncEventArgs args)
        {
            if (m_IsRuning == false) return;

            m_Items.Enqueue(args);
        }

        public void WaitShutdown()
        {
            m_IsRuning = false;
            m_Thread.Join();
        }

        private void Dispatcher()
        {
            SocketAsyncEventArgs args;
            while (m_IsRuning || !m_Items.IsEmpty)
            {
                if (m_Items.TryDequeue(out args))
                {
                    ((TcpSession)args.UserToken).ProcessReceive(args);
                    continue;
                }
                Thread.Sleep(1);
            }
        }
    }
}