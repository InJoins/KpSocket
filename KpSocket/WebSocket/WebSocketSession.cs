using KpSocket.Core;
using KpSocket.IO;
using KpSocket.Packet;
using KpSocket.Tcp;
using System;
using System.Net.Sockets;
using System.Text;

namespace KpSocket.WebSocket
{
    public sealed class WebSocketSession : TcpSession
    {
        private volatile WebSocketSessionState m_State;
        private readonly ISessionHandler m_Handler;

        public WebSocketSessionState State
        {
            get { return m_State; }
            private set { m_State = value; }
        }

        public Uri Uri
        {
            get;
            private set;
        }

        public string Origin
        {
            get;
            set;
        }

        public string Protocol
        {
            get;
            set;
        }

        internal class WebSocketSessionHandler : ISessionHandler
        {
            public static WebSocketSessionHandler Handler
            {
                get;
                private set;
            } = new WebSocketSessionHandler();

            public void OnBegin(ISession session, bool state)
            {
                var web = (WebSocketSession)session;
                if (!state) //tcp cant connect,is begin false.
                {
                    web.State = WebSocketSessionState.Closed;
                    web.Close();
                    web.m_Handler.OnBegin(web, false);
                }
                else
                {
                    web.TrySend(new StringMessage()
                    {
                        Content = WebSocketHelper.CreateRequest(web.Uri, web.Origin, web.Protocol),
                    });
                    if (!web.IsInWorker) web.Flush();
                }
            }

            public void OnEnd(ISession session, bool state)
            {
                var web = (WebSocketSession)session;
                if (web.State == WebSocketSessionState.Open)
                {
                    web.State = WebSocketSessionState.Closed;
                    web.m_Handler.OnEnd(session, state);
                }
            }

            public void OnError(object sender, object state)
            {
                var web = sender as WebSocketSession;
                if (web.State == WebSocketSessionState.Open)
                {
                    web.m_Handler.OnError(sender, state);
                }
                else
                {
                    web.State = WebSocketSessionState.Closed;
                    web.Close();
                }
            }

            public void OnRead(ISession session, object state)
            {
                var web = (WebSocketSession)session;
                if (web.State == WebSocketSessionState.Connecting)
                {
                    web.ProcessResponse((DataReader)state);
                }
                else if (web.State == WebSocketSessionState.Open)
                {
                    web.ProcessMessage((IMessage)state);
                }
            }
        }

        internal class WebSocketServerHandler : ISessionHandler
        {
            public static WebSocketServerHandler Handler
            {
                get;
                private set;
            } = new WebSocketServerHandler();

            public void OnBegin(ISession session, bool state)
            {
                //empty code
            }

            public void OnEnd(ISession session, bool state)
            {
                var web = (WebSocketSession)session;
                if (web.State == WebSocketSessionState.Open)
                {
                    web.State = WebSocketSessionState.Closed;
                    web.m_Handler.OnEnd(session, state);
                }
            }

            public void OnError(object sender, object state)
            {
                var web = sender as WebSocketSession;
                if (web != null)
                {
                    if (web.State == WebSocketSessionState.Open)
                    {
                        web.m_Handler.OnError(sender, state);
                    }
                    else
                    {
                        web.State = WebSocketSessionState.Closed;
                        web.Close();
                    }
                }
                else
                {
                    var svr = sender as WebSocketServer;
                    if (svr != null)
                    {
                        svr.m_Handler.OnError(sender, state);
                    }
                }
            }

            public void OnRead(ISession session, object state)
            {
                var web = (WebSocketSession)session;
                if (web.State == WebSocketSessionState.Connecting)
                {
                    web.ProcessRequest((DataReader)state);
                }
                else if (web.State == WebSocketSessionState.Open)
                {
                    web.ProcessMessage((IMessage)state);
                }
            }
        }

        public WebSocketSession(string uri, ISessionHandler handler)
            : base(WebSocketSessionHandler.Handler)
        {
            Uri = new Uri(uri);
            m_Handler = handler;
        }

        internal WebSocketSession(Socket socket, ISessionHandler handler, TcpServer parent)
            : base(socket, WebSocketServerHandler.Handler, parent)
        {
            m_Handler = handler;
        }

        private void ProcessRequest(DataReader reader)
        {
            var stream = (ReceiveStream)reader.BaseStream;
            var str = stream.SubstringByEndOf(WebSocketHelper.HTTP_EndOf,
                Encoding.UTF8);

            if (!string.IsNullOrEmpty(str))
            {
                var base64Key = WebSocketHelper.GetSubstring(str, "Sec-WebSocket-Key:", "\r\n");
                var protocol = WebSocketHelper.GetSubstring(str, "Sec-WebSocket-Protocol:", "\r\n");
                var origin = WebSocketHelper.GetSubstring(str, "Origin:", "\r\n");
                IMessage message;

                if (!string.IsNullOrEmpty(base64Key))
                {
                    this.TrySend(new StringMessage()
                    {
                        Content = WebSocketHelper.CreateOkResponse(base64Key, protocol),
                    });
                    this.Flush();           //need now flush,this not set in worker

                    if (!string.IsNullOrWhiteSpace(origin))         //防止origin有问题的
                    {
                        Uri tmpUri = null;
                        if (Uri.TryCreate(origin.Trim(), UriKind.Absolute, out tmpUri))
                        {
                            this.Uri = tmpUri;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(origin)) this.Origin = origin.Trim();
                    if (!string.IsNullOrWhiteSpace(protocol)) this.Protocol = protocol.Trim();

                    this.State = WebSocketSessionState.Open;    //下移防止Uri(origin.Trim())，异常
                    this.Packet = new WebSocketPacket();

                    m_Handler.OnBegin(this, true);
                    while (!IsLeave && this.Packet.TryOutMessage(reader, out message))
                    {
                        this.ProcessMessage(message);
                    }
                }
                else
                {
                    this.Close();	        //not is open websocket
                }
            }
        }

        private void ProcessResponse(DataReader reader)
        {
            var stream = (ReceiveStream)reader.BaseStream;
            var str = stream.SubstringByEndOf(WebSocketHelper.HTTP_EndOf,
                Encoding.UTF8);
            IMessage message;

            if (!string.IsNullOrEmpty(str))
            {
                if (str.IndexOf("Sec-WebSocket-Accept") != -1)
                {
                    this.State = WebSocketSessionState.Open;
                    this.Packet = new WebSocketPacket();

                    m_Handler.OnBegin(this, true);
                    while (!IsLeave && this.Packet.TryOutMessage(reader, out message))
                    {
                        this.ProcessMessage(message);
                    }
                }
                else
                {
                    this.State = WebSocketSessionState.Closed;
                    this.Close();

                    m_Handler.OnBegin(this, false);
                }
            }
        }

        private void ProcessMessage(IMessage message)
        {
            WebSocketFrame frame = message as WebSocketFrame;

            if (frame != null)
            {
                switch (frame.Opcode)
                {
                    case Opcode.ContinueFrame:
                        {
                            //
                        }
                        break;

                    case Opcode.TextFrame:
                        {
                            m_Handler.OnRead(this, Encoding.UTF8.
                                GetString(frame.PayloadData));
                        }
                        break;

                    case Opcode.BinaryFrame:
                        {
                            m_Handler.OnRead(this, frame.PayloadData);
                        }
                        break;

                    case Opcode.CloseFrame:
                        {
                            this.Close();	    //is open websocket
                        }
                        break;

                    case Opcode.PingFrame:
                        {
                            TryPong(frame.PayloadData);
                        }
                        break;

                    case Opcode.PongFrame:
                        {
                            //
                        }
                        break;
                }
            }
            else
            {
                m_Handler.OnRead(this, message);
            }
        }

        public bool TryPing(byte[] payloadData = null)
        {
            return base.TrySend(new WebSocketFrame()
            {
                IsFrameEndOf = true,
                Opcode = Opcode.PingFrame,
                PayloadLength = payloadData != null ? payloadData.Length : 0,
                PayloadData = payloadData
            });
        }

        public bool TryPong(byte[] payloadData = null)
        {
            return base.TrySend(new WebSocketFrame()
            {
                IsFrameEndOf = true,
                Opcode = Opcode.PongFrame,
                PayloadLength = payloadData != null ? payloadData.Length : 0,
                PayloadData = payloadData
            });
        }

        public bool TrySend(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return base.TrySend(new WebSocketFrame()
            {
                IsFrameEndOf = true,
                Opcode = Opcode.TextFrame,
                PayloadLength = bytes.Length,
                PayloadData = bytes
            });
        }

        public bool TrySend(byte[] bytes)
        {
            return base.TrySend(new WebSocketFrame()
            {
                IsFrameEndOf = true,
                Opcode = Opcode.BinaryFrame,
                PayloadLength = bytes.Length,
                PayloadData = bytes
            });
        }

        public bool TryClose(string code)
        {
            State = WebSocketSessionState.Closing;
            return base.TrySend(new WebSocketFrame()
            {
                IsFrameEndOf = true,
                Opcode = Opcode.CloseFrame,
                PayloadLength = 4,
                PayloadData = Encoding.UTF8.GetBytes(code)
            });
        }

        public void Connect()
        {
            if (State == WebSocketSessionState.Open) throw new Exception("this session is open.");

            State = WebSocketSessionState.Connecting;
            Connect(Uri.DnsSafeHost, Uri.Port);
        }
    }
}