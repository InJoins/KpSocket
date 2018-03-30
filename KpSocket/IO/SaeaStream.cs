using KpSocket.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace KpSocket.IO
{
    public abstract class SaeaStream : Stream
    {
        protected readonly List<SocketAsyncEventArgs> m_Saeas;
        internal readonly SaeaManager m_SaeaManager;
        protected long m_Capacity;
        protected long m_Position;
        protected long m_Length;
        protected int m_CurPosition;
        protected int m_CurIdx;
        protected bool m_IsOpen;

        public SaeaStream()
        {
            m_Saeas = new List<SocketAsyncEventArgs>();
            m_SaeaManager = SocketRuntime.Instance.SaeaManager;
            m_IsOpen = true;
        }

        public override bool CanRead
        {
            get
            {
                if (!m_IsOpen)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                if (!m_IsOpen)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                if (!m_IsOpen)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                return true;
            }
        }

        public override long Length
        {
            get
            {
                if (!m_IsOpen)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                return m_Length;
            }
        }

        public override long Position
        {
            get
            {
                if (!m_IsOpen)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                return m_Position;
            }
            set
            {
                if (!m_IsOpen)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                if (value > m_Capacity)
                {
                    throw new ArgumentException("invalid off position.", "Position");
                }

                for (int i = 0, tmpCapacity = 0; i < m_Saeas.Count; i++)
                {
                    var tmpCount = m_Saeas[i].Count;

                    tmpCapacity += tmpCount;
                    if (tmpCapacity > value)
                    {
                        m_Position = value;

                        m_CurPosition = tmpCount - (tmpCapacity - (int)value);
                        m_CurIdx = i;
                        break;
                    }
                }
            }
        }

        public abstract override void Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("invalid off count.", nameof(count));
            }
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            var num = m_Length - m_Position;

            if (num > count)
            {
                num = count;
            }

            if (num <= 0)
            {
                return 0;
            }

            var tmpNum = num;

            while (tmpNum > 0)
            {
                var curSaea = m_Saeas[m_CurIdx];
                var curLength = curSaea.Count - m_CurPosition;

                if (curLength == 0)
                {
                    m_CurPosition = 0;
                    m_CurIdx++;
                    continue;
                }
                else
                {
                    if (curLength > tmpNum)                                         //read alive data
                    {
                        curLength = (int)tmpNum;
                    }

                    if (curLength <= 8)
                    {
                        var num2 = curLength;

                        while (--num2 >= 0)
                        {
                            buffer[offset + num2] = curSaea.Buffer[curSaea.Offset + m_CurPosition + num2];
                        }
                    }
                    else
                    {
                        Buffer.BlockCopy(curSaea.Buffer, curSaea.Offset + m_CurPosition, buffer, offset, curLength);
                    }

                    m_CurPosition += curLength;
                    m_Position += curLength;
                    offset += curLength;
                    tmpNum -= curLength;
                }
            }
            return (int)num;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            switch (origin)
            {
                case SeekOrigin.Begin:
                    this.Position = offset;
                    break;

                case SeekOrigin.Current:
                    this.Position = m_Position + offset;
                    break;

                case SeekOrigin.End:
                    this.Position = m_Length + offset;
                    break;
            }
            return m_Position;
        }

        public override void SetLength(long value)
        {
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
            if (value < 0 || value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (value > m_Capacity)
            {
                throw new ArgumentException("invalid off length.", nameof(value));
            }

            if (m_Position > (m_Length = value))
            {
                this.Position = m_Length;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("invalid off count.", nameof(count));
            }
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            if ((m_Position += count) > m_Length)
            {
                m_Length = m_Position;
            }

            while (m_Position > m_Capacity)
            {
                var saea = m_SaeaManager.Pop();

                m_Capacity += saea.Count;
                m_Saeas.Add(saea);
            }

            while (count > 0)
            {
                var curSaea = m_Saeas[m_CurIdx];
                var curCapacity = curSaea.Count - m_CurPosition;

                if (curCapacity == 0)
                {
                    m_CurPosition = 0;
                    m_CurIdx++;
                    continue;
                }
                else
                {
                    var tmpCount = count;

                    if (tmpCount > curCapacity)
                    {
                        tmpCount = curCapacity;
                    }

                    if (tmpCount <= 8)
                    {
                        var num2 = tmpCount;

                        while (--num2 >= 0)
                        {
                            curSaea.Buffer[curSaea.Offset + m_CurPosition + num2] = buffer[offset + num2];
                        }
                    }
                    else
                    {
                        Buffer.BlockCopy(buffer, offset, curSaea.Buffer, curSaea.Offset + m_CurPosition, tmpCount);
                    }

                    m_CurPosition += tmpCount;
                    offset += tmpCount;
                    count -= tmpCount;
                }
            }
        }

        internal void Masking(byte[] maskingKey, int length)
        {
            var tmpCurPosition = m_CurPosition;
            var tmpCurIdx = m_CurIdx;

            for (var i = 0; i < length; i++)
            {
                var e = m_Saeas[tmpCurIdx];
                if (e.Count == tmpCurPosition)
                {
                    tmpCurPosition = 0;
                    tmpCurIdx++;
                    continue;
                }

                e.Buffer[e.Offset + tmpCurPosition] = (byte)(e.Buffer[e.Offset + tmpCurPosition]
                    ^ maskingKey[i % maskingKey.Length]);
                tmpCurPosition++;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!m_IsOpen)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            m_Saeas.ForEach(x => m_SaeaManager.Push(x));
            m_Saeas.Clear();

            m_Capacity = 0;
            m_Position = 0;
            m_Length = 0;
            m_CurPosition = 0;
            m_CurIdx = 0;
            m_IsOpen = false;
        }
    }
}