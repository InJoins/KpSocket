using System;
using KpSocket.Tcp;
using KpSocket.Utils;

namespace TestSvr
{
    class Program
    {
        static void Main(string[] args)
        {
            var runtime = new SocketRuntime().InitialPool();
            var svr = new TcpServer(new Handler());
            svr.Listen("0.0.0.0", 8088);

            Console.WriteLine("test svr is runing...");
            Console.Read();
        }
    }
}
