using KpSocket.Tcp;
using KpSocket.WebSocket;
using System;

namespace KpSocket.IO
{
    public sealed class SendStream : SaeaStream
    {
        private readonly TcpSession m_Session;

        public override bool CanRead
        {
            get
            {
                if (!m_IsOpen)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                return false;
            }
        }

        internal bool IsMask
        {
            get;
            private set;
        }

        internal byte[] MaskingKey
        {
            get;
            private set;
        }

        public SendStream(TcpSession session)
        {
            var web = session as WebSocketSession;
            if (web != null && web.IsClientSendMask)
            {
                MaskingKey = WebSocketHelper.CreateMaskingKey(4);
                IsMask = true;
            }
            m_Session = session;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            if (m_Saeas.Count > 0)
            {
                for (var i = 0; i < m_Saeas.Count; i++)
                {
                    var e = m_Saeas[i];

                    if (i == m_CurIdx)          //curIdx is endIdx
                    {
                        e.SetBuffer(e.Offset, m_CurPosition);
                    }
                    m_Session.AsyncSend(e);
                }
                m_Saeas.Clear();

                m_Capacity = 0;
                m_Position = 0;
                m_Length = 0;
                m_CurPosition = 0;
                m_CurIdx = 0;
            }
        }

        internal void GobackMasking(int length)
        {
            var nowPosition = m_Position;
            this.Position = nowPosition - length;
            this.Masking(MaskingKey, length);
            this.Position = nowPosition;
        }
    }
}