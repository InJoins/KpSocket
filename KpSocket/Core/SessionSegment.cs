namespace KpSocket.Core
{
    public class SessionSegment
    {
        public ISession[] Sessions
        {
            get;
            internal set;
        }

        public int Count
        {
            get;
            internal set;
        }

        public long SessionVersion
        {
            get;
            internal set;
        }

        public SessionSegment(int capacity)
        {
            Sessions = new ISession[capacity];
        }
    }
}