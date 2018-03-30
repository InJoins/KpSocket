using System.IO;
using System.Text;

namespace KpSocket.IO
{
    public sealed class DataReader : BinaryReader
    {
        public DataReader(Stream input)
            : this(input, Encoding.UTF8, false)
        {

        }

        public DataReader(Stream input, Encoding encoding)
            : this(input, encoding, false)
        {

        }

        public DataReader(Stream input, Encoding encoding, bool leaveOpen)
            : base(input, encoding, leaveOpen)
        {

        }
    }
}