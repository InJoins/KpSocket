using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using KpSocket.Logger;

namespace KpSocket.Utils
{
    public sealed class SocketRuntime
    {
        private List<ReceiveWorker> m_ReceiveWorkers;
        private List<SendWorker> m_SendWorkers;
        private List<DataWorker> m_DataWorkers;
        private int m_RIdx, m_SIdx, m_DIdx;
        private SaeaManager m_SaeaManager;
        private LRUDetect m_LRUDetect;

        public static SocketRuntime Instance
        {
            get;
            private set;
        }

        public int BufferSize
        {
            get;
            private set;
        }

        public int SendSleep
        {
            get;
            set;
        }

        internal ILogger Logger
        {
            get;
            private set;
        }

        internal SaeaManager SaeaManager
        {
            get { return m_SaeaManager; }
        }

        internal LRUDetect LRUDetect
        {
            get { return m_LRUDetect; }
        }

        public SocketRuntime()
        {
            if (Instance != null) throw new Exception("SocketRuntime is created.");

            Instance = this;
            SendSleep = 1;
            this.Logger = new LoggerFactory().AddConsole()
                .AddFile().CreateLogger("Net");
        }

        public SocketRuntime InitialPool(int bufferSize = 4096, int initialCount = 20000)
        {
            if (m_SaeaManager != null) throw new Exception("this initial complete.");

            BufferSize = bufferSize;
            m_SaeaManager = new SaeaManager();
            m_SaeaManager.Initial(bufferSize, initialCount);
            Logger.LogDebug("initial pool buffsize:{0} initialcount:{1}.", bufferSize, initialCount);
            return this;
        }

        public SocketRuntime InitialWoker(int receiveWork = 0, int sendWorker = 0, int dataWorker = 0)
        {
            if (m_ReceiveWorkers != null) throw new Exception("this initial complete.");
            if (receiveWork <= 0) receiveWork = Environment.ProcessorCount;
            if (sendWorker <= 0) sendWorker = Environment.ProcessorCount;
            if (dataWorker <= 0) dataWorker = Environment.ProcessorCount;

            m_ReceiveWorkers = new List<ReceiveWorker>(receiveWork);
            for (var i = 0; i < receiveWork; i++)
            {
                m_ReceiveWorkers.Add(new ReceiveWorker());
            }

            m_SendWorkers = new List<SendWorker>(sendWorker);
            for (var i = 0; i < sendWorker; i++)
            {
                m_SendWorkers.Add(new SendWorker());
            }

            m_DataWorkers = new List<DataWorker>(dataWorker);
            for (var i = 0; i < dataWorker; i++)
            {
                m_DataWorkers.Add(new DataWorker());
            }
            Logger.LogDebug("initial worker receivework:{0} sendworker:{1} dataworker:{2}.",
                receiveWork, sendWorker, dataWorker);
            return this;
        }

        public SocketRuntime SetLRUDetect(int timeout, int interval)
        {
            if (m_LRUDetect != null) throw new Exception("SetLRUDetect complete.");

            if (m_LRUDetect == null)
            {
                m_LRUDetect = new LRUDetect(timeout, interval);
            }
            Logger.LogDebug("use LRUDelete timeout:{0} interval:{1}.", timeout, interval);
            return this;
        }

        internal object GetWorker(int type)
        {
            if (m_ReceiveWorkers == null) throw new Exception("not initial workers.");

            switch (type)
            {
                case 0:
                    return m_ReceiveWorkers[m_RIdx++ % m_ReceiveWorkers.Count];

                case 1:
                    return m_SendWorkers[m_SIdx++ % m_SendWorkers.Count];

                case 2:
                    return m_DataWorkers[m_DIdx++ % m_DataWorkers.Count];

                default:
                    throw new ArgumentException("type not found.", nameof(type));
            }
        }

        public void Shutdown()
        {
            if (m_LRUDetect != null)
                m_LRUDetect.Dispose();

            if (m_ReceiveWorkers != null)
            {
                m_ReceiveWorkers.ForEach(x => x.WaitShutdown());
                m_ReceiveWorkers.Clear();
            }

            if (m_SendWorkers != null)
            {
                m_SendWorkers.ForEach(x => x.WaitShutdown());
                m_SendWorkers.Clear();
            }

            if (m_DataWorkers != null)
            {
                m_DataWorkers.ForEach(x => x.WaitShutdown());
                m_DataWorkers.Clear();
            }

            SaeaManager.Dispose();
            Logger.LogDebug("socketruntime shutdown.");
        }
    }
}