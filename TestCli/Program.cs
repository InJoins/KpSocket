using KpSocket.Utils;
using System;
using KpSocket.Tcp;
using System.Threading;

namespace TestCli
{
    class Program
    {
        public static long SendCount;
        public static long ReceiveCount;

        static void Main(string[] args)
        {
            var runtime = new SocketRuntime().InitialPool();
            var cli = new TcpSession(new Handler());
            cli.Connect("127.0.0.1", 8088);

            ThreadPool.QueueUserWorkItem(s =>
            {
                for (; ; )
                {
                    Console.WriteLine("send count:" + Interlocked.Read(ref SendCount));
                    Console.WriteLine("receive count:" + Interlocked.Read(ref ReceiveCount));

                    Thread.Sleep(1000);
                }
            });

            Console.WriteLine("test cli is runing...");
            Console.Read();
        }
    }
}
