using KpSocket.Core;
using KpSocket.IO;

namespace KpSocket.Packet
{
    public interface IPacket
    {
        ICoder Coder
        {
            get;
            set;
        }

        bool TryOutMessage(DataReader reader, out IMessage message);

        void InMessage(DataWriter writer, IMessage message);
    }
}