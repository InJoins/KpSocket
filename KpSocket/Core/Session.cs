using System;
using System.Collections;
using System.Collections.Generic;

namespace KpSocket.Core
{
    public abstract class Session : ISession, IDisposable
    {
        protected readonly Queue<IMessage> m_SendMessages;
        protected readonly Queue<IMessage> m_ReceiveMessages;
        protected readonly object m_SyncRoot;
        private readonly Hashtable m_Hashtable;
        private volatile bool m_IsLeave;

        public object this[object key]
        {
            get { return m_Hashtable[key]; }
            set { m_Hashtable[key] = value; }
        }

        public LinkedListNode<Node> Node
        {
            get;
            set;
        }

        public bool ActorEnable
        {
            get;
            set;
        }

        public bool IsLeave
        {
            get { return m_IsLeave; }
            protected set { m_IsLeave = value; }
        }

        public Session()
        {
            m_SendMessages = new Queue<IMessage>(16);
            m_ReceiveMessages = new Queue<IMessage>(16);
            m_Hashtable = new Hashtable(16);
            m_SyncRoot = new object();
        }

        public bool TrySend(IMessage message)
        {
            lock (m_SyncRoot)
            {
                if (IsLeave) return false;

                m_SendMessages.Enqueue(message);
                CheckAddWorker();
                return true;
            }
        }

        protected abstract void CheckAddWorker();

        public void SaveMsg(IMessage message)
        {
            lock (m_ReceiveMessages)
            {
                m_ReceiveMessages.Enqueue(message);
            }
        }

        public void TakeMsg(Queue<IMessage> queue)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));

            lock (m_ReceiveMessages)
            {
                while (m_ReceiveMessages.Count > 0)
                {
                    queue.Enqueue(m_ReceiveMessages.Dequeue());
                }
            }
        }

        public void SkipSave(Queue<IMessage> queue)
        {
            lock (m_ReceiveMessages)
            {
                while (m_ReceiveMessages.Count > 0)
                {
                    queue.Enqueue(m_ReceiveMessages.Dequeue());
                }
                while (queue.Count > 0)
                {
                    m_ReceiveMessages.Enqueue(queue.Dequeue());
                }
            }
        }

        public abstract void TimeOut();

        public abstract void Flush();

        protected bool isDisposed;
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposed) return;

            if (isDisposing)
            {
                lock (m_SyncRoot)
                {
                    m_SendMessages.Clear();
                }
                lock (m_ReceiveMessages)
                {
                    m_ReceiveMessages.Clear();
                }
                Node = null;
                m_Hashtable.Clear();
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