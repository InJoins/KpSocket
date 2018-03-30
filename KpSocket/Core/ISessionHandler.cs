namespace KpSocket.Core
{
    public interface ISessionHandler
    {
        void OnBegin(ISession session, bool state);

        void OnRead(ISession session, object state);

        void OnError(object sender, object state);

        void OnEnd(ISession session, bool state);
    }
}