using System.Collections.Generic;

namespace KpSocket.Actor
{
    interface IActor
    {
        void Post(object obj);
        void PostAll(Queue<object> objs);
        object Receive();
        void ReceiveAll(Queue<object> objs);
    }
}