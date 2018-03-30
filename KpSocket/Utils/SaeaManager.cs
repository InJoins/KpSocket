using KpSocket.Tcp;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace KpSocket.Utils
{
    internal sealed class SaeaManager : IDisposable
    {
        private readonly ConcurrentStack<SocketAsyncEventArgs> m_SaeaPool;
        private readonly BufferManager m_BufferManager;
        private int m_AddCount;

        public SaeaManager()
        {
            m_SaeaPool = new ConcurrentStack<SocketAsyncEventArgs>();
            m_BufferManager = new BufferManager();
        }

        public void Initial(int bufferSize, int count, int addCount = 0)
        {
            m_AddCount = (addCount == 0 ? count : addCount);
            m_BufferManager.Initial(bufferSize, count, addCount);

            for (var i = 0; i < count; i++)
            {
                var e = new SocketAsyncEventArgs();
                e.Completed += TcpSession.CompleteHandler;
                m_BufferManager.SetBuffer(e);
                m_SaeaPool.Push(e);
            }
        }

        public SocketAsyncEventArgs Pop()
        {
            SocketAsyncEventArgs e;

            if (m_SaeaPool.TryPop(out e))
            {
                e.SetBuffer(e.Offset, m_BufferManager.BufferSize);
                return e;
            }
            else
            {
                e = new SocketAsyncEventArgs();
                e.Completed += TcpSession.CompleteHandler;
                m_BufferManager.SetBuffer(e);
                return e;
            }
        }

        public void Push(SocketAsyncEventArgs e)
        {
            m_SaeaPool.Push(e);
        }

        private bool isDisposed;
        public void Dispose()
        {
            if (isDisposed) return;

            SocketAsyncEventArgs saea;

            while (m_SaeaPool.TryPop(out saea))
            {
                saea.Completed -= TcpSession.CompleteHandler;
                saea.SetBuffer(null, 0, 0);
                saea.Dispose();
            }
            m_BufferManager.Dispose();
            isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}