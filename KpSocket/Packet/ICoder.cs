using KpSocket.Core;
using KpSocket.IO;

namespace KpSocket.Packet
{
    public interface ICoder
    {
        IMessage Decode(DataReader reader);

        void Encode(DataWriter writer, IMessage message);
    }
}