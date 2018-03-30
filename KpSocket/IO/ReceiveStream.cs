using System;
using System.Net.Sockets;
using System.Text;

namespace KpSocket.IO
{
    public sealed class ReceiveStream : SaeaStream
    {
        public override bool CanWrite
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

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public void Write(SocketAsyncEventArgs e)
        {
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            e.SetBuffer(e.Offset, e.BytesTransferred);
            m_Saeas.Add(e);
            m_Capacity += e.Count;
            m_Length += e.Count;
        }

        public override void Flush()
        {
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            if (m_Position == m_Length)
            {
                m_Saeas.ForEach(x => m_SaeaManager.Push(x));
                m_Saeas.Clear();

                m_Capacity = 0;
                m_Position = 0;
                m_Length = 0;
                m_CurPosition = 0;
                m_CurIdx = 0;
            }
            else if (m_CurIdx > 0)
            {
                for (var i = 0; i < m_CurIdx; i++)
                {
                    var e = m_Saeas[i];

                    m_Capacity -= e.Count;
                    m_Position -= e.Count;
                    m_Length -= e.Count;
                    m_SaeaManager.Push(e);
                }
                m_Saeas.RemoveRange(0, m_CurIdx);
                m_CurIdx = 0;
            }
        }

        internal string SubstringByEndOf(byte[] endOf, Encoding encoding)
        {
            var aliveLength = m_Length - m_Position;

            if (aliveLength >= endOf.Length)
            {
                var nowPosition = m_Position;
                var buffer = new byte[aliveLength];

                this.Read(buffer, 0, buffer.Length);

                var idx = _IndexOf(buffer, 0, buffer.Length, endOf);
                if (idx != -1)
                {
                    this.Position = nowPosition + (idx + endOf.Length);
                    return encoding.GetString(buffer, 0, idx + endOf.Length);
                }
                else
                {
                    this.Position = nowPosition;
                }
            }
            return null;

            int _IndexOf(byte[] buffer, int offset, int count, byte[] value)
            {
                count = offset + count;
                for (var i = offset; i < count; i++)
                {
                    if (value.Length > (count - i)) break;      //后面数据不够结束符长度，直接返回

                    var isContinue = false;
                    for (var j = 0; j < value.Length; j++)
                    {
                        if (value[j] != buffer[i + j])
                        {
                            isContinue = true;
                            break;
                        }
                    }

                    if (!isContinue) return i;                  //查找到了返回索引
                }
                return -1;
            }
        }
    }
}