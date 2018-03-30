using KpSocket.Core;
using KpSocket.Tcp;
using System.Net;
using System.Net.Sockets;

namespace KpSocket.Utils
{
    public static class UtilsExtensions
    {
        public static string GetIp(this ISession session)
        {
            var tcpSession = session as TcpSession;
            if (tcpSession != null)
            {
                var remote = (IPEndPoint)tcpSession.Socket.RemoteEndPoint;
                if (remote.Address.IsIPv4MappedToIPv6)
                {
                    return remote.Address.MapToIPv4().ToString();
                }
                return remote.Address.MapToIPv6().ToString();
            }
            return null;
        }

        public static string GetPort(this ISession session)
        {
            var tcpSession = session as TcpSession;
            if (tcpSession != null)
            {
                return ((IPEndPoint)tcpSession.Socket.RemoteEndPoint).Port.ToString();
            }
            return null;
        }

        public static string GetEndPoint(this ISession session)
        {
            return session.GetIp() + ":" + session.GetPort();
        }
    }
}