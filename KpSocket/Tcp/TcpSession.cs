using KpSocket.Core;
using KpSocket.IO;
using KpSocket.Packet;
using KpSocket.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;

namespace KpSocket.Tcp
{
    public class TcpSession : Session
    {
        private readonly ISessionHandler m_Handler;
        private readonly SaeaManager m_SaeaManager;
        private readonly DataReader m_Reader;
        private readonly DataWriter m_Writer;
        private readonly TcpServer m_Parent;
        private readonly Socket m_Socket;

        private volatile bool m_IsInWorker;
        internal ReceiveWorker m_ReceiveWorker;
        private SendWorker m_SendWorker;
        private DataWorker m_DataWorker;

        public SessionContainer Parent
        {
            get { return m_Parent; }
        }

        public Socket Socket
        {
            get;
            private set;
        }

        public IPacket Packet
        {
            get;
            set;
        }

        public bool IsInWorker
        {
            get
            {
                return m_IsInWorker;
            }
            set
            {
                if (m_IsInWorker && !value)
                {
                    m_ReceiveWorker = null;
                    m_SendWorker = null;
                    m_DataWorker = null;
                }
                else if (!m_IsInWorker && value)
                {
                    m_ReceiveWorker = (ReceiveWorker)SocketRuntime.Instance.GetWorker(0);
                    m_SendWorker = (SendWorker)SocketRuntime.Instance.GetWorker(1);
                    m_DataWorker = (DataWorker)SocketRuntime.Instance.GetWorker(2);
                }

                m_IsInWorker = value;
            }
        }

        internal bool IsClientSendMask
        {
            get;
            set;
        }                                           //客户端发送过来的都需要Mask，服务端发送的不能Mask

        public TcpSession(ISessionHandler handler)
            : this(new Socket(SocketType.Stream, ProtocolType.Tcp), handler, null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
        }

        internal TcpSession(Socket socket, ISessionHandler handler, TcpServer parent)
        {
            IsClientSendMask = (handler == WebSocket.WebSocketSession.WebSocketSessionHandler.Handler);
            m_SaeaManager = SocketRuntime.Instance.SaeaManager;
            m_Reader = new DataReader(new ReceiveStream());
            m_Writer = new DataWriter(new SendStream(this));
            m_Parent = parent;
            m_Socket = socket;
            Socket = socket;
            m_Handler = handler;
        }

        public void Connect(EndPoint remoteEndPoint)
        {
            if (isDisposed) throw new ObjectDisposedException(this.GetType().FullName);

            var e = m_SaeaManager.Pop();
            var isAsync = false;

            try
            {
                e.RemoteEndPoint = remoteEndPoint;
                e.SetBuffer(e.Offset, 0);
                e.UserToken = this;
                isAsync = m_Socket.ConnectAsync(e);
            }
            catch (System.Exception)
            {
                m_SaeaManager.Push(e);
                m_Handler.OnBegin(this, false);
                isAsync = true;
            }
            finally
            {
                if (!isAsync) ProcessConnect(e);
            }
        }

        public void Connect(string host, int port)
        {
            var ips = Dns.GetHostAddressesAsync(host).Result;

            if (ips == null || ips.Length == 0)
            {
                throw new Exception("the host is empty ipaddress");
            }
            Connect(new IPEndPoint(ips[0], port));
        }

        public void Close()
        {
            if (IsLeave) return;

            try
            {
                m_Socket.Shutdown(SocketShutdown.Both);
            }
            catch (System.Exception) { }
            finally
            {
                IsLeave = true;
            }
        }

        protected override void CheckAddWorker()
        {
            if (m_SendMessages.Count == 1 && m_IsInWorker)
            {
                m_SendWorker.AddItem(this);
            }
        }

        public override void Flush()
        {
            try
            {
                IMessage message;
                lock (m_SyncRoot)
                {
                    if (IsLeave) return;

                    while (m_SendMessages.Count > 0)
                    {
                        message = m_SendMessages.Dequeue();

                        if (Packet == null || message is FreeMessage)
                        {
                            message.Write(m_Writer);
                        }
                        else
                        {
                            Packet.InMessage(m_Writer, message);
                        }
                    }
                    m_Writer.Flush();	                                            //flush to socket
                }
            }
            catch (System.Exception e)
            {
                m_Handler.OnError(this, e);
            }
        }

        public override void TimeOut()
        {
            SocketRuntime.Instance.Logger.LogInformation("session timeout remoteendpoint:{0}.",
                this.Socket.RemoteEndPoint);

            this.Close();
        }

        internal void AsyncSend(SocketAsyncEventArgs e)
        {
            var isAsync = false;

            try
            {
                e.UserToken = this;
                isAsync = m_Socket.SendAsync(e);
            }
            catch (System.Exception)
            {
                m_SaeaManager.Push(e);
                isAsync = true;
            }
            finally
            {
                if (!isAsync) ProcessSend(e);
            }
        }

        internal void AsyncReceive()
        {
            var e = m_SaeaManager.Pop();
            var isAsync = false;

            try
            {
                e.UserToken = this;
                isAsync = m_Socket.ReceiveAsync(e);
            }
            catch (System.Exception)
            {
                m_SaeaManager.Push(e);
                OnSessionEnd(false);
                isAsync = true;
            }
            finally
            {
                if (!isAsync) ProcessReceive(e);
            }
        }

        internal static void CompleteHandler(object sender, SocketAsyncEventArgs e)
        {
            var session = e.UserToken as TcpSession;

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    {
                        session.ProcessConnect(e);
                    }
                    break;
                case SocketAsyncOperation.Receive:
                    {
                        if (session.m_IsInWorker) session.m_DataWorker.AddItem(e);
                        else session.ProcessReceive(e);
                    }
                    break;
                case SocketAsyncOperation.Send:
                    {
                        session.ProcessSend(e);
                    }
                    break;
            }
        }

        internal void ProcessConnect(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                m_SaeaManager.Push(e);
                m_Handler.OnBegin(this, true);
                if (SocketRuntime.Instance.LRUDetect != null) SocketRuntime.Instance.LRUDetect.Update(this);

                if (!IsLeave)
                {
                    if (m_IsInWorker) m_ReceiveWorker.AddItem(this);
                    else this.AsyncReceive();
                }
                else
                {
                    OnSessionEnd(false);
                }
            }
            else
            {
                m_SaeaManager.Push(e);
                m_Handler.OnBegin(this, false);
            }
        }

        internal void ProcessReceive(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success
                && e.BytesTransferred > 0)
            {
                var stream = (ReceiveStream)m_Reader.BaseStream;

                try
                {
                    stream.Write(e);

                    if (Packet != null)
                    {
                        IMessage message;
                        while (!IsLeave && Packet.TryOutMessage(m_Reader, out message))
                        {
                            m_Handler.OnRead(this, message);
                        }
                    }
                    else
                    {
                        m_Handler.OnRead(this, m_Reader);
                    }

                    if (SocketRuntime.Instance.LRUDetect != null) SocketRuntime.Instance.LRUDetect.Update(this);
                }
                catch (System.Exception ex)
                {
                    m_Handler.OnError(this, ex);
                }
                finally
                {
                    if (!IsLeave)
                    {
                        stream.Flush();                     //push saea

                        if (m_IsInWorker) m_ReceiveWorker.AddItem(this);
                        else this.AsyncReceive();
                    }
                    else
                    {
                        OnSessionEnd(false);
                    }
                }
            }
            else
            {
                m_SaeaManager.Push(e);
                OnSessionEnd(true);
            }
        }

        internal void ProcessSend(SocketAsyncEventArgs e)
        {
            m_SaeaManager.Push(e);
        }

        internal void OnSessionEnd(bool state)
        {
            if (SocketRuntime.Instance.LRUDetect != null) SocketRuntime.Instance.LRUDetect.Delete(this);

            if (m_Parent != null) m_Parent.SessionEnd(this);
            m_Handler.OnEnd(this, state);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Close();
                m_Reader.Dispose();
                m_Writer.Dispose();
                m_Socket.Dispose();
                base.Dispose(isDisposing);
            }
        }
    }
}