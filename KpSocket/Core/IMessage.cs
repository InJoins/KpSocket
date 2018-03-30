using KpSocket.IO;

namespace KpSocket.Core
{
    public interface IMessage
    {
        void Read(DataReader reader);

        void Write(DataWriter writer);
    }
}