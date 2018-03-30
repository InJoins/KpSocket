using KpSocket.Actor.Module;
using KpSocket.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KpSocket.Actor
{
    internal sealed class CallInfo
    {
        public object State { get; set; }

        public SendOrPostCallback Callback { get; set; }

        public ManualResetEventSlim Wait { get; set; }
    }

    internal sealed class RpcCall
    {
        public object Request { get; set; }

        public object Response { get; set; }

        public ManualResetEventSlim Wait { get; set; }
    }

    internal sealed class ActorSynchronizationContext : SynchronizationContext
    {
        private readonly Actor m_CurrentActor;

        public ActorSynchronizationContext(Actor actor)
        {
            m_CurrentActor = actor;
        }

        public override SynchronizationContext CreateCopy()
        {
            return new ActorSynchronizationContext(m_CurrentActor);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            m_CurrentActor.Post(new CallInfo()
            {
                State = state,
                Callback = d,
                Wait = null
            });
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            using (var waitHandle = new ManualResetEventSlim(false))
            {
                m_CurrentActor.Post(new CallInfo()
                {
                    State = state,
                    Callback = d,
                    Wait = waitHandle
                });
                waitHandle.Wait();
            }
        }
    }

    public abstract class Actor : IActor, IModule
    {
        private readonly Dictionary<string, Action<object>> m_Handlers;
        private readonly ConcurrentQueue<object> m_Queue;
        private readonly Queue<object> m_Objects;
        private readonly List<IModule> m_Modules;
        private volatile bool m_IsClosed;
        private readonly int m_Sleep;
        private object m_Response;

        public bool IsClosed
        {
            get { return m_IsClosed; }
            protected set { m_IsClosed = value; }
        }

        public int Sleep
        {
            get { return m_Sleep; }
        }

        public bool Enable
        {
            get;
            set;
        } = true;

        public string Name
        {
            get { return this.GetType().Name; }
        }

        public IReadOnlyCollection<IModule> Modules
        {
            get { return m_Modules; }
        }

        protected Actor(int sleep = 16)
        {
            m_Handlers = new Dictionary<string, Action<object>>(32);
            m_Queue = new ConcurrentQueue<object>();
            m_Objects = new Queue<object>();
            m_Modules = new List<IModule>();
            m_Sleep = sleep;

            this.InitCall(this);
        }

        public void Post(object obj)
        {
            m_Queue.Enqueue(obj);
        }

        public void PostAll(Queue<object> objs)
        {
            if (objs == null) throw new ArgumentNullException(nameof(objs));

            while (objs.Count > 0)
            {
                m_Queue.Enqueue(objs.Dequeue());
            }
        }

        public void PostAll(IEnumerable<object> objs)
        {
            if (objs == null) throw new ArgumentNullException(nameof(objs));

            foreach (var o in objs)
            {
                m_Queue.Enqueue(o);
            }
        }

        public object Receive()
        {
            if (m_Queue.TryDequeue(out object obj))
                return obj;
            else
                return null;
        }

        public void ReceiveAll(Queue<object> objs)
        {
            if (objs == null) throw new ArgumentNullException(nameof(objs));

            while (m_Queue.TryDequeue(out object obj))
            {
                objs.Enqueue(obj);
            }
        }

        public void Act() => ThreadPool.QueueUserWorkItem(ActorProcess);

        public Task<T> RequestAsync<T>(object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            return Task.Run(() =>
            {
                using (var wait = new ManualResetEventSlim(false))
                {
                    var rpcCall = new RpcCall()
                    {
                        Request = obj,
                        Wait = wait
                    };
                    this.Post(rpcCall); wait.Wait();
                    return (T)rpcCall.Response;
                }
            });
        }

        private void InitCall(IModule obj)
        {
            var methods = obj.GetType().GetMethods();
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(object)
                    && m.IsPublic && m.DeclaringType != typeof(Actor)
                    && m.DeclaringType != typeof(object)
                    && !m.IsAbstract && !m.IsStatic)
                {
                    m_Handlers[m.Name] = (Action<object>)m.CreateDelegate(
                        typeof(Action<object>), obj);
                }
            }
        }

        private void CallInfo(object obj)
        {
            CallInfo callInfo = (CallInfo)obj;
            callInfo.Callback(callInfo.State);
            callInfo.Wait?.Set();
        }

        private void RpcCall(object obj)
        {
            var rpcCall = (RpcCall)obj;
            this.Call(rpcCall.Request);
            if ((rpcCall.Response = this.m_Response) != null)
                this.m_Response = null;
            rpcCall.Wait.Set();
        }

        private void Call(object obj)
        {
            var handler = obj.GetType().Name;
            if (m_Handlers.ContainsKey(handler))
            {
                Action<object> action = m_Handlers[handler];
                IModule module = action.Target as IModule;

                if (module.Enable && this.Enable)
                {
                    m_Handlers[handler](obj);
                }
            }
            else
            {
                SocketRuntime.Instance.Logger.LogError($"objcall in not find. name:{handler}.");
            }
        }

        private void CallAll()
        {
            this.ReceiveAll(m_Objects);
            while (m_Objects.Count > 0)
            {
                this.Call(m_Objects.Dequeue());
            }
        }

        protected void SetResponse(object obj)
        {
            this.m_Response = obj;
        }

        protected void RegistModule(IModule obj)
        {
            this.InitCall(obj);
            m_Modules.Add(obj);
        }

        protected void ActorProcess(object state)
        {
            //await后的代码继续逻辑线程执行
            SynchronizationContext.SetSynchronizationContext(
                new ActorSynchronizationContext(this));
            m_Handlers.Add(typeof(CallInfo).Name, CallInfo);
            m_Handlers.Add(typeof(RpcCall).Name, RpcCall);

            while (!m_IsClosed) //可能还需要继续处理，所以这里为true
            {
                try
                {
                    ProcessFrame(DateTime.Now);
                }
                catch (Exception e)
                {
                    SocketRuntime.Instance.Logger.LogError(this.GetHashCode(),
                        e, "GoFrame Error.");
                }
                finally
                {
                    System.Threading.Thread.Sleep(m_Sleep);
                }
            }
        }

        protected virtual void ProcessFrame(DateTime now)
        {
            this.CallAll();
        }
    }
}