using KpSocket.Core;
using KpSocket.IO;
using System;

namespace KpSocket.Packet
{
    public sealed class LengthPacket : IPacket
    {
        private readonly ushort m_MaxPacketLength;

        public ICoder Coder
        {
            get;
            set;
        }

        public LengthPacket(ushort maxPacketLength)
        {
            m_MaxPacketLength = maxPacketLength;
        }

        public bool TryOutMessage(DataReader reader, out IMessage message)
        {
            var stream = reader.BaseStream;
            var nowPosition = stream.Position;
            var aliveLength = stream.Length - nowPosition;

            if (aliveLength > 2)
            {
                var packetLength = reader.ReadUInt16();

                if (packetLength > m_MaxPacketLength)
                {
                    throw new Exception("packet length exceeds maxPacketLength.");
                }

                if (aliveLength >= packetLength)
                {
                    try
                    {
                        return (message = Coder.Decode(reader)) != null;
                    }
                    catch (System.Exception)
                    {
                        throw new Exception("decode error.");
                    }
                }
                else
                {
                    stream.Position = nowPosition;
                }
            }
            return (message = null) != null;
        }

        public void InMessage(DataWriter writer, IMessage message)
        {
            var stream = writer.BaseStream;
            var nowPosition = stream.Position;
            var newPosition = 0L;

            try
            {
                writer.Write(ushort.MinValue);
                Coder.Encode(writer, message);
                newPosition = stream.Position;
            }
            catch (System.Exception)
            {
                throw new Exception("encode error.");
            }

            var packetLength = newPosition - nowPosition;

            if (packetLength > m_MaxPacketLength)
            {
                throw new Exception("packet length exceeds maxPacketLength.");
            }

            stream.Position = nowPosition;
            writer.Write((ushort)packetLength);
            stream.Position = newPosition;
        }
    }
}