using KpSocket.Core;
using KpSocket.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace KpSocket.Tcp
{
    public class TcpServer : SessionContainer
    {
        private readonly ISessionHandler m_Handler;
        private readonly SemaphoreSlim m_MaxWaiter;
        private readonly Socket m_Socket;

        public TcpServer(ISessionHandler handler, int maxSessionLimit = 10000)
            : base(maxSessionLimit)
        {
            m_Handler = handler;
            m_MaxWaiter = new SemaphoreSlim(maxSessionLimit);
            m_Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        public void Listen(EndPoint endPoint, int backlog = 1000)
        {
            if (isDisposed) throw new ObjectDisposedException(this.GetType().FullName);
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));

            try
            {
                m_Socket.Bind(endPoint);
                m_Socket.Listen(backlog);

                var e = new SocketAsyncEventArgs();
                e.UserToken = this;
                e.Completed += CompleteHandler;
                ThreadPool.QueueUserWorkItem(AsyncAccept, e);
                SocketRuntime.Instance.Logger.LogDebug("listen endpoint:{0} backlog:{1}.",
                    endPoint, backlog);
            }
            catch (System.Exception e) { throw new Exception("listen is error.", e); }
        }

        public void Listen(string host, int port, int backlog = 1000)
        {
            Listen(new IPEndPoint(IPAddress.Parse(host), port), backlog);
        }

        protected virtual TcpSession GetSession(Socket socket, ISessionHandler handler, TcpServer parent)
        {
            return new TcpSession(socket, handler, parent);
        }

        private void AsyncAccept(object state)
        {
            var flag = true;
            while (flag && !isDisposed)
            {
                var args = (SocketAsyncEventArgs)state;
                try
                {
                    m_MaxWaiter.Wait();
                    args.AcceptSocket = null;
                    flag = !m_Socket.AcceptAsync(args);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    m_Handler.OnError(this, e);
                    break;
                }
                if (flag) OnAccept(args);
            }
        }

        private void OnAccept(SocketAsyncEventArgs e)
        {
            TcpSession newSession = null;

            if (!isDisposed && this.SessionCount < this.MaxSessionLimit
                && e.SocketError == SocketError.Success)
            {
                newSession = GetSession(e.AcceptSocket, m_Handler, this);
                AddSession(newSession);
            }
            else
            {
                if (e.AcceptSocket != null)
                    e.AcceptSocket.Dispose();

                if (isDisposed) return;
            }

            if (newSession != null)
            {
                m_Handler.OnBegin(newSession, true);
                if (SocketRuntime.Instance.LRUDetect != null) SocketRuntime.Instance.LRUDetect.Update(newSession);

                if (!newSession.IsLeave)
                {
                    if (newSession.IsInWorker) newSession.m_ReceiveWorker.AddItem(newSession);
                    else newSession.AsyncReceive();
                }
                else
                {
                    newSession.OnSessionEnd(false);
                }
            }
        }

        internal void CompleteHandler(object sender, SocketAsyncEventArgs e)
        {
            OnAccept(e);
            AsyncAccept(e);
        }

        internal void SessionEnd(TcpSession session)
        {
            m_MaxWaiter.Release();
            this.RemoveSession(session);

            if (isDisposed && m_MaxWaiter.CurrentCount == this.MaxSessionLimit)
            {
                m_MaxWaiter.Dispose();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (m_MaxWaiter.CurrentCount == this.MaxSessionLimit)
                {
                    m_MaxWaiter.Dispose();
                }

                m_Socket.Dispose();
            }
        }
    }
}