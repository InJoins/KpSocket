using KpSocket.IO;

namespace KpSocket.Core
{
    public sealed class BytesMessage : FreeMessage
    {
        public byte[] Buffer
        {
            get;
            set;
        }

        public int Offset
        {
            get;
            set;
        }

        public int Count
        {
            get;
            set;
        }

        public override void Read(DataReader reader)
        {
            Count = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            Buffer = reader.ReadBytes(Count);
            Offset = 0;
        }

        public override void Write(DataWriter writer)
        {
            writer.Write(Buffer, Offset, Count);
        }
    }
}