using System;
using System.Collections.Generic;

namespace KpSocket.Core
{
    public class Node
    {
        public DateTime LastTime { get; set; }

        public ISession Session { get; set; }

        public bool Detect(DateTime curTime, int timeout)
        {
            return Math.Abs((curTime - LastTime).TotalMilliseconds) > timeout;
        }
    }

    public interface ISession
    {
        object this[object key] { get; set; }

        LinkedListNode<Node> Node { get; set; }

        bool ActorEnable { get; set; }

        bool IsLeave { get; }

        bool TrySend(IMessage message);

        void SaveMsg(IMessage message);

        void TakeMsg(Queue<IMessage> queue);

        void SkipSave(Queue<IMessage> queue);

        void TimeOut();

        void Flush();
    }
}