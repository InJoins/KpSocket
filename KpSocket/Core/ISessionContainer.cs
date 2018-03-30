namespace KpSocket.Core
{
    public interface ISessionContainer
    {
        long SessionVersion { get; }

        int SessionCount { get; }

        int MaxSessionLimit { get; }

        ISession[] GetSessions();

        void GetSessions(SessionSegment segment);

        void AddSession(ISession session);

        void RemoveSession(ISession session);
    }
}