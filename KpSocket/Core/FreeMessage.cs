using KpSocket.IO;

namespace KpSocket.Core
{
    public abstract class FreeMessage : IMessage
    {
        public abstract void Read(DataReader reader);

        public abstract void Write(DataWriter writer);
    }
}