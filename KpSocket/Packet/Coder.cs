using KpSocket.Core;
using KpSocket.IO;
using System;

namespace KpSocket.Packet
{
    public abstract class Coder<T> : ICoder
    {
        public abstract Type Convert(T tag);

        public abstract T Convert(IMessage message);

        public abstract IMessage Decode(DataReader reader);

        public abstract void Encode(DataWriter writer, IMessage message);
    }
}