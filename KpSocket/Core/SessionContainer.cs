using System;
using System.Collections.Generic;

namespace KpSocket.Core
{
    public abstract class SessionContainer : ISessionContainer, IDisposable
    {
        private readonly LinkedList<ISession> m_Sessions;
        protected readonly object m_SyncRoot;

        public int MaxSessionLimit
        {
            get;
            private set;
        }

        public int SessionCount
        {
            get;
            private set;
        }

        public long SessionVersion
        {
            get;
            private set;
        }

        public SessionContainer(int maxSessionLimit = 0)
        {
            MaxSessionLimit = maxSessionLimit > 0 ? maxSessionLimit : int.MaxValue;
            m_Sessions = new LinkedList<ISession>();
            m_SyncRoot = new object();
        }

        public ISession[] GetSessions()
        {
            lock (m_SyncRoot)
            {
                var result = new ISession[m_Sessions.Count];

                m_Sessions.CopyTo(result, 0);
                return result;
            }
        }

        public void GetSessions(SessionSegment segment)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));

            lock (m_SyncRoot)
            {
                if (segment.SessionVersion != this.SessionVersion)
                {
                    m_Sessions.CopyTo(segment.Sessions, 0);
                    segment.Count = m_Sessions.Count;
                    segment.SessionVersion = this.SessionVersion;
                }
            }
        }

        public void AddSession(ISession session)
        {
            lock (m_SyncRoot)
            {
                m_Sessions.AddLast(session);
                SessionCount++; SessionVersion++;
            }
        }

        public void RemoveSession(ISession session)
        {
            lock (m_SyncRoot)
            {
                m_Sessions.Remove(session);
                SessionCount--; SessionVersion++;
            }
        }

        protected bool isDisposed;
        protected abstract void Dispose(bool isDisposing);

        public void Dispose()
        {
            Dispose(true);
            isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}