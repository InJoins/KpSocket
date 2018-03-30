using KpSocket.Core;
using KpSocket.IO;
using System;

namespace KpSocket.Packet
{
    public sealed class BinaryCoder : Coder<string>
    {
        public override Type Convert(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }
            return Type.GetType(tag);
        }

        public override string Convert(IMessage message)
        {
            return message.GetType().AssemblyQualifiedName;
        }

        public override IMessage Decode(DataReader reader)
        {
            var type = Convert(reader.ReadString());
            var message = Activator.CreateInstance(type) as IMessage;

            message?.Read(reader);
            return message;
        }

        public override void Encode(DataWriter writer, IMessage message)
        {
            writer.Write(Convert(message));
            message.Write(writer);
        }
    }
}