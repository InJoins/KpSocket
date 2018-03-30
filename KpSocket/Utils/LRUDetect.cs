using KpSocket.Core;
using System;
using System.Collections.Generic;
using System.Threading;

namespace KpSocket.Utils
{
    internal sealed class LRUDetect : IDisposable
    {
        private readonly LinkedList<Node> m_LinkedList;
        private readonly int m_Timeout;
        private readonly Timer m_Timer;
        private readonly object m_SyncRoot;

        public LRUDetect(int timeout, int interval)
        {
            m_LinkedList = new LinkedList<Node>();
            m_Timeout = timeout;
            m_Timer = new Timer(OnDetect, null, interval, interval);
            m_SyncRoot = new object();
        }

        public void Update(ISession session)
        {
            lock (m_SyncRoot)
            {
                LinkedListNode<Node> node = session.Node;

                if (node != null)
                {
                    node.Value.LastTime = DateTime.Now;
                    m_LinkedList.Remove(node);
                    m_LinkedList.AddFirst(node);
                }
                else
                {
                    node = m_LinkedList.AddFirst(new Node());
                    node.Value.LastTime = DateTime.Now;
                    node.Value.Session = session;
                    session.Node = node;
                }
            }
        }

        public void Delete(ISession session)
        {
            lock (m_SyncRoot)
            {
                LinkedListNode<Node> node = session.Node;

                if (node != null)
                {
                    node.Value.Session.Node = null;
                    node.Value.Session = null;
                    m_LinkedList.Remove(node);
                }
            }
        }

        private void OnDetect(object state)
        {
            lock (m_SyncRoot)
            {
                LinkedListNode<Node> last = m_LinkedList.Last;
                var curTime = DateTime.Now;

                while (last != null && last.Value.Detect(curTime, m_Timeout))
                {
                    last.Value.Session.Node = null;
                    last.Value.Session.TimeOut();
                    last.Value.Session = null;
                    m_LinkedList.RemoveLast();
                    last = m_LinkedList.Last;
                }
            }
        }

        bool isDisposed;
        private void Dispose(bool isDisposing)
        {
            if (isDisposed) return;

            if (isDisposing)
            {
                m_Timer.Dispose();
                lock (m_SyncRoot)
                {
                    m_LinkedList.Clear();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}