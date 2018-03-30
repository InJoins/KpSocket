using KpSocket.IO;
using System;

namespace KpSocket.WebSocket
{
    internal sealed class WebSocketFrame : KpSocket.Core.IMessage
    {
        public bool IsFrameEndOf
        {
            get;
            set;
        }

        public KpSocket.WebSocket.Opcode Opcode
        {
            get;
            set;
        }

        public int PayloadLength
        {
            get;
            set;
        }

        public byte[] PayloadData
        {
            get;
            set;
        }

        public void Read(DataReader reader)
        {
            throw new NotImplementedException();
        }

        public void Write(DataWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}