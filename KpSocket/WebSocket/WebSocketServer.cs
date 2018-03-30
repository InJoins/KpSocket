using KpSocket.Core;
using KpSocket.Tcp;
using System.Net.Sockets;

namespace KpSocket.WebSocket
{
    public sealed class WebSocketServer : TcpServer
    {
        internal readonly ISessionHandler m_Handler;

        public WebSocketServer(ISessionHandler handler, int maxSessionLimit = 10000)
            : base(WebSocketSession.WebSocketServerHandler.Handler, maxSessionLimit)
        {
            m_Handler = handler;
        }

        protected override TcpSession GetSession(Socket socket, ISessionHandler handler, TcpServer parent)
        {
            return new WebSocketSession(socket, m_Handler, parent);
        }
    }
}