using KpSocket.Actor.Module;
using KpSocket.Core;
using KpSocket.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KpSocket.Actor
{
    public abstract class NetActor<T1> : Actor where T1 : ISession
    {
        private readonly Dictionary<string, Action<T1, IMessage>> m_Handlers;
        private readonly Queue<IMessage> m_Receives;
        private readonly List<IModule> m_NetModules;
        private readonly LinkedList<T1> m_Sessions;

        public IReadOnlyCollection<T1> Sessions
        {
            get { return m_Sessions; }
        }

        public IReadOnlyCollection<IModule> NetModules
        {
            get { return m_NetModules; }
        }

        public NetActor(int sleep = 16)
            : base(sleep)
        {
            m_Handlers = new Dictionary<string, Action<T1, IMessage>>();
            m_Receives = new Queue<IMessage>();
            m_NetModules = new List<IModule>();
            m_Sessions = new LinkedList<T1>();

            this.InitNetCall(this);
        }

        private void InitNetCall(IModule obj)
        {
            var methods = obj.GetType().GetMethods();
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == typeof(T1)
                    && ps[1].ParameterType == typeof(IMessage)
                    && m.IsPublic && m.DeclaringType != typeof(Actor)
                    && m.DeclaringType != typeof(object)
                    && !m.IsAbstract && !m.IsStatic)
                {
                    m_Handlers[m.Name] = (Action<T1, IMessage>)m.CreateDelegate(
                        typeof(Action<T1, IMessage>), obj);
                }
            }
        }

        private void NetCall(T1 obj1, IMessage obj2)
        {
            var handler = obj2.GetType().Name;
            if (m_Handlers.ContainsKey(handler))
            {
                Action<T1, IMessage> action = m_Handlers[handler];
                IModule module = action.Target as IModule;

                if (module.Enable && this.Enable)
                {
                    m_Handlers[handler](obj1, obj2);
                }
            }
            else
            {
                var logger = SocketRuntime.Instance.Logger;
                logger.LogError($"netcall in not find. name:{handler}.");
            }
        }

        private void NetCallAll()
        {
            LinkedListNode<T1> node = m_Sessions.First;

            while (node != null)
            {
                var s = node.Value;
                if (s != null)
                {
                    s.TakeMsg(m_Receives);
                    while (m_Receives.Count > 0)
                    {
                        this.NetCall(s, m_Receives.Dequeue());

                        if (node.Value == null)
                        {
                            s.SkipSave(m_Receives);
                            break;
                        }
                    }
                }
                var nextNode = node.Next;
                if (node.Value == null)
                {
                    m_Sessions.Remove(node);
                }
                node = nextNode;
            }
        }

        protected void AddSession(T1 session)
        {
            m_Sessions.AddLast(session);
        }

        protected void SessionLeave(T1 session)
        {
            var node = m_Sessions.Find(session);
            node.Value = default(T1);
        }

        protected bool IsExistsSession(T1 session)
        {
            var node = m_Sessions.Find(session);
            return node != null && node.Value != null;
        }

        protected void RegistNetModule(IModule obj)
        {
            this.InitNetCall(obj);
            m_NetModules.Add(obj);
        }

        protected override void ProcessFrame(DateTime now)
        {
            base.ProcessFrame(now);
            this.NetCallAll();
        }
    }
}