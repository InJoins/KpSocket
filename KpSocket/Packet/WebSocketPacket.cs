using KpSocket.Core;
using KpSocket.IO;
using KpSocket.WebSocket;
using System;
using System.IO;
using System.Threading;

namespace KpSocket.Packet
{
    public sealed class WebSocketPacket : IPacket
    {
        private static readonly ThreadLocal<DataWriter> s_Writers = new ThreadLocal<DataWriter>(() =>
        {
            return new DataWriter(new MemoryStream(Utils.SocketRuntime.Instance.BufferSize * 2));
        });

        public static int MaxPacketLength
        {
            get;
            set;
        } = 40960;

        public ICoder Coder
        {
            get;
            set;
        }

        public bool TryOutMessage(DataReader reader, out IMessage message)
        {
            var stream = (ReceiveStream)reader.BaseStream;
            var nowPosition = stream.Position;
            var aliveLength = stream.Length - nowPosition;

            if (aliveLength >= 2)
            {
                var headData = reader.ReadByte();
                var isEndOf = (headData >> 7) > 0;
                var opcode = headData & 0x0F;

                var payload = reader.ReadByte();
                var isMask = (payload >> 7) > 0;
                var payLength = payload & 0x7F;

                byte[] maskingKey = null;

                if (payLength == 126)
                    payLength = reader.ReadUInt16().SwapUInt16();

                else if (payLength == 127)
                    payLength = (int)reader.ReadUInt64().SwapUInt64();

                if (payLength > MaxPacketLength)
                {
                    throw new Exception("packet length exceeds maxPacketLength.");
                }

                if (isMask)                                                         //get maskingKey
                {
                    if ((stream.Length - stream.Position) >= 4)
                    {
                        maskingKey = reader.ReadBytes(4);
                    }
                    else
                    {
                        stream.Position = nowPosition;
                        return (message = null) != null;
                    }
                }

                if (payLength == 0)                                                 //command frame
                {
                    return (message = new WebSocketFrame()
                    {
                        IsFrameEndOf = isEndOf,
                        Opcode = (Opcode)opcode,
                        PayloadLength = payLength,
                        PayloadData = null
                    }) != null;
                }
                else if ((stream.Length - stream.Position) >= payLength)
                {
                    if (isMask)                                                     //unmasking
                    {
                        stream.Masking(maskingKey, payLength);
                    }

                    if ((Opcode)opcode == Opcode.BinaryFrame && Coder != null)
                    {
                        try
                        {
                            return (message = Coder.Decode(reader)) != null;
                        }
                        catch (System.Exception)
                        {
                            throw new Exception("decode error.");
                        }
                    }
                    else
                    {
                        return (message = new WebSocketFrame()	                    //sub string binary frame
                        {
                            IsFrameEndOf = isEndOf,
                            Opcode = (Opcode)opcode,
                            PayloadLength = payLength,
                            PayloadData = reader.ReadBytes(payLength)
                        }) != null;
                    }
                }
                else
                {
                    stream.Position = nowPosition;
                }
            }
            return (message = null) != null;
        }

        public void InMessage(DataWriter writer, IMessage message)
        {
            var frame = message as WebSocketFrame;

            if (frame != null)
            {
                WriteWebSocketFrame(writer, frame);
            }
            else if (Coder != null)
            {
                try
                {
                    var tmpWriter = s_Writers.Value;
                    var tmpStream = (MemoryStream)tmpWriter.BaseStream;
                    ArraySegment<byte> segment;

                    tmpStream.SetLength(0);
                    Coder.Encode(tmpWriter, message);

                    if (tmpStream.TryGetBuffer(out segment))
                    {
                        WriteArraySegment(writer, segment);
                    }
                }
                catch (System.Exception)
                {
                    throw new Exception("encode error.");
                }
            }
            else
            {
                throw new Exception("coder is empty.");	                            //code is empty
            }
        }

        private void WriteArraySegment(DataWriter writer, ArraySegment<byte> segment)
        {
            if (segment.Count > MaxPacketLength)
            {
                throw new Exception("packet length exceeds maxPacketLength.");
            }

            var stream = (SendStream)writer.BaseStream;
            writer.Write((byte)((1 << 7) | (int)Opcode.BinaryFrame));
            if (segment.Count < 126)
            {
                writer.Write((byte)(((stream.IsMask ? 1 : 0) << 7) | segment.Count));
            }
            else if (segment.Count < ushort.MaxValue)
            {
                writer.Write((byte)(((stream.IsMask ? 1 : 0) << 7) | 126));
                writer.Write(((ushort)segment.Count).SwapUInt16());
            }
            else
            {
                writer.Write((byte)(((stream.IsMask ? 1 : 0) << 7) | 127));
                writer.Write(((ulong)segment.Count).SwapUInt64());
            }

            if (!stream.IsMask)
            {
                stream.Write(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                writer.Write(stream.MaskingKey);
                stream.Write(segment.Array, segment.Offset, segment.Count);
                stream.GobackMasking(segment.Count);
            }
        }

        private void WriteWebSocketFrame(DataWriter writer, WebSocketFrame frame)
        {
            if (frame.PayloadLength > MaxPacketLength)
            {
                throw new Exception("packet length exceeds maxPacketLength.");
            }

            var stream = (SendStream)writer.BaseStream;
            writer.Write((byte)(((frame.IsFrameEndOf ? 1 : 0) << 7) | (int)frame.Opcode));
            if (frame.PayloadLength < 126)
            {
                writer.Write((byte)(((stream.IsMask ? 1 : 0) << 7) | frame.PayloadLength));
            }
            else if (frame.PayloadLength < ushort.MaxValue)
            {
                writer.Write((byte)(((stream.IsMask ? 1 : 0) << 7) | 126));
                writer.Write(((ushort)frame.PayloadLength).SwapUInt16());
            }
            else
            {
                writer.Write((byte)(((stream.IsMask ? 1 : 0) << 7) | 127));
                writer.Write(((ulong)frame.PayloadLength).SwapUInt64());
            }

            if (!stream.IsMask)
            {
                writer.Write(frame.PayloadData);
            }
            else
            {
                writer.Write(stream.MaskingKey);
                writer.Write(frame.PayloadData);
                stream.GobackMasking(frame.PayloadLength);
            }
        }
    }
}