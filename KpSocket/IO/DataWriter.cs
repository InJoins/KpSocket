using System.IO;
using System.Text;

namespace KpSocket.IO
{
    public sealed class DataWriter : BinaryWriter
    {
        private static readonly UTF8Encoding UTF8NoBOM = new UTF8Encoding(false, true);

        public override Stream BaseStream
        {
            get { return OutStream; }
        }

        public DataWriter(Stream output)
            : this(output, UTF8NoBOM, false)
        {

        }

        public DataWriter(Stream output, Encoding encoding)
            : this(output, encoding, false)
        {

        }

        public DataWriter(Stream output, Encoding encoding, bool leaveOpen)
            : base(output, encoding, leaveOpen)
        {

        }
    }
}