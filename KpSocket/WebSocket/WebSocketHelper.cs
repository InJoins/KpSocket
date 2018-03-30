using System;
using System.Security.Cryptography;
using System.Text;

namespace KpSocket.WebSocket
{
    public enum WebSocketSessionState
    {
        Connecting,
        Open,
        Closing,
        Closed
    }

    internal enum Opcode
    {
        ContinueFrame,
        TextFrame,
        BinaryFrame,
        CloseFrame = 0x08,
        PingFrame = 0x09,
        PongFrame = 0x0A,
    }

    internal static class WebSocketHelper
    {
        static readonly RandomNumberGenerator m_RandomNumber = RandomNumberGenerator.Create();
        static readonly SHA1 m_SHA1 = SHA1.Create();
        static readonly string CRLF = "\r\n";
        internal static readonly byte[] HTTP_EndOf = new byte[] { 0x0D, 0x0A, 0x0D, 0x0A };

        public static string CreateBase64Key()
        {
            var src = new byte[16];
            m_RandomNumber.GetBytes(src);
            return Convert.ToBase64String(src);
        }

        public static string CreateAcceptBase64Key(string base64Key)
        {
            return Convert.ToBase64String(m_SHA1.ComputeHash(Encoding.UTF8.GetBytes(
                $"{base64Key}258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }

        public static string CreateRequest(Uri uri, string origin, string protocol)
        {
            var sb = new StringBuilder(256);

            sb.Append("GET ").Append(uri.AbsolutePath).Append(" HTTP/1.1").Append(CRLF);
            sb.Append("Host: ").Append(uri.DnsSafeHost).Append(CRLF);
            sb.Append("Upgrade: websocket").Append(CRLF);
            sb.Append("Connection: Upgrade").Append(CRLF);
            sb.Append("Sec-WebSocket-Key: ").Append(CreateBase64Key()).Append(CRLF);

            if (!string.IsNullOrWhiteSpace(origin))
            {
                sb.Append("Origin: ").Append(origin).Append(CRLF);
            }

            if (!string.IsNullOrWhiteSpace(protocol))
            {
                sb.Append("Sec-WebSocket-Protocol: ").Append(protocol).Append(CRLF);
            }
            sb.Append("Sec-WebSocket-Version: 13").Append(CRLF).Append(CRLF);
            return sb.ToString();
        }

        public static string CreateOkResponse(string base64Key, string protocol)
        {
            var sb = new StringBuilder(256);

            sb.Append("HTTP/1.1 101 Switching Protocols").Append(CRLF);
            sb.Append("Upgrade: websocket").Append(CRLF);
            sb.Append("Connection: Upgrade").Append(CRLF);

            if (!string.IsNullOrWhiteSpace(protocol))
            {
                sb.Append("Sec-WebSocket-Protocol: ").Append(protocol.Trim()).Append(CRLF);
            }
            sb.Append("Sec-WebSocket-Accept: ").Append(CreateAcceptBase64Key(base64Key.Trim()))
                .Append(CRLF).Append(CRLF);
            return sb.ToString();
        }

        public static byte[] CreateMaskingKey(int length)
        {
            var src = new byte[length];
            m_RandomNumber.GetBytes(src);
            return src;
        }

        public static string GetSubstring(string str, string left, string right, int startIndex = 0)
        {
            if (string.IsNullOrEmpty(str)) return null;

            int lIdx = 0, rIdx = 0;

            if (!string.IsNullOrEmpty(left))
            {
                lIdx = str.IndexOf(left, startIndex);
                if (lIdx == -1) return null;
                else lIdx += left.Length;
            }
            if (!string.IsNullOrEmpty(right))
            {
                rIdx = str.IndexOf(right, lIdx);
                if (rIdx == -1) return null;
            }
            else
            {
                rIdx = str.Length;
            }
            return str.Substring(lIdx, rIdx - lIdx);
        }

        public static short SwapInt16(this short n)
        {
            return (short)(((n & 0xff) << 8) | ((n >> 8) & 0xff));
        }

        public static ushort SwapUInt16(this ushort n)
        {
            return (ushort)(((n & 0xff) << 8) | ((n >> 8) & 0xff));
        }

        public static int SwapInt32(this int n)
        {
            return (int)(((SwapInt16((short)n) & 0xffff) << 0x10) |
                (SwapInt16((short)(n >> 0x10)) & 0xffff));
        }

        public static uint SwapUInt32(this uint n)
        {
            return (uint)(((SwapUInt16((ushort)n) & 0xffff) << 0x10) |
                (SwapUInt16((ushort)(n >> 0x10)) & 0xffff));
        }

        public static long SwapInt64(this long n)
        {
            return (long)(((SwapInt32((int)n) & 0xffffffffL) << 0x20) |
                (SwapInt32((int)(n >> 0x20)) & 0xffffffffL));
        }

        public static ulong SwapUInt64(this ulong n)
        {
            return (ulong)(((SwapUInt32((uint)n) & 0xffffffffL) << 0x20) |
                (SwapUInt32((uint)(n >> 0x20)) & 0xffffffffL));
        }
    }
}