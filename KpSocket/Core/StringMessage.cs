using KpSocket.IO;
using System.Text;

namespace KpSocket.Core
{
    public sealed class StringMessage : FreeMessage
    {
        public Encoding Encoding
        {
            get;
            set;
        } = Encoding.UTF8;

        public string Content
        {
            get;
            set;
        }

        public override void Read(DataReader reader)
        {
            Content = Encoding.GetString(reader.ReadBytes((int)(reader.BaseStream.Length
                - reader.BaseStream.Position)));
        }

        public override void Write(DataWriter writer)
        {
            writer.Write(Encoding.GetBytes(Content));
        }
    }
}