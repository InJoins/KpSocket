using System;
using System.Collections.Generic;
using System.Text;
using KpSocket.Core;
using KpSocket.Utils;
using KpSocket.Tcp;
using KpSocket.IO;

namespace TestSvr
{
    class Handler : ISessionHandler
    {
        public void OnBegin(ISession session, bool state)
        {
            Console.WriteLine("connected:" + session.GetEndPoint());
        }

        public void OnEnd(ISession session, bool state)
        {
            Console.WriteLine("deconnected:" + session.GetEndPoint());
        }

        public void OnError(object sender, object state)
        {
            if (sender is TcpSession session)
            {
                session.Close();
            }
        }

        public void OnRead(ISession session, object state)
        {
            if (state is DataReader reader)
            {
                var stream = reader.BaseStream;
                var buffer = new byte[stream.Length];
                var msg = new BytesMessage()
                {
                    Buffer = buffer,
                    Count = buffer.Length,
                    Offset = 0
                };
                stream.Read(buffer, 0, buffer.Length);
                stream.Flush();
                session.TrySend(msg);
                session.Flush();
            }
        }
    }
}
