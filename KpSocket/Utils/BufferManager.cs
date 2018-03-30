using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace KpSocket.Utils
{
    internal sealed class BufferManager : IDisposable
    {
        private readonly Stack<byte[]> m_Buffers;
        private readonly object m_SyncRoot;
        private int m_BufferSize;
        private int m_AddCount;
        private int m_CurIdx;

        public int BufferSize
        {
            get { return m_BufferSize; }
        }

        public BufferManager()
        {
            m_Buffers = new Stack<byte[]>(8);
            m_SyncRoot = new object();
        }

        public void Initial(int bufferSize, int count, int addCount = 0)
        {
            m_BufferSize = bufferSize;
            m_AddCount = (addCount == 0 ? count : addCount);
            m_Buffers.Push(new byte[bufferSize * count]);
        }

        public void SetBuffer(SocketAsyncEventArgs e)
        {
            lock (m_SyncRoot)
            {
                if (m_CurIdx * m_BufferSize >= m_Buffers.Peek().Length)
                {
                    m_Buffers.Push(new byte[m_BufferSize * m_AddCount]);
                    m_CurIdx = 0;
                }
                e.SetBuffer(m_Buffers.Peek(), m_BufferSize * m_CurIdx++, m_BufferSize);
            }
        }

        private bool isDisposed;
        public void Dispose()
        {
            if (isDisposed) return;

            lock (m_SyncRoot)
            {
                while (m_Buffers.Count > 0)
                {
                    m_Buffers.Pop();
                }
            }
            isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}