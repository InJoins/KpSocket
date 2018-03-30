using System;
using System.Collections.Generic;
using System.Text;
using KpSocket.Core;
using KpSocket.Utils;
using KpSocket.Tcp;
using KpSocket.IO;
using System.Threading;

namespace TestCli
{
    class Handler : ISessionHandler
    {
        public void OnBegin(ISession session, bool state)
        {
            Console.WriteLine("connected:" + session.GetEndPoint());
            session.TrySend(new BytesMessage()
            {
                Buffer = new byte[1024],
                Count = 1024,
                Offset = 0
            });
            session.Flush();
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
                reader.Read(buffer, 0, buffer.Length);
                stream.Flush();
                session.TrySend(msg);
                session.Flush();
                Interlocked.Increment(ref Program.SendCount);
                Interlocked.Increment(ref Program.ReceiveCount);
            }
        }
    }
}
