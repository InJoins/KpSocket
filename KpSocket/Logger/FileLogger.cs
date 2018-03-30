using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace KpSocket.Logger
{
    public class FileLogger : ILogger
    {
        private class DisposableScope : IDisposable
        {
            public void Dispose()
            {
                s_ScopeName = null;
            }
        }

        private readonly string m_CategoryName;
        private readonly string m_Directory;
        private readonly string m_FileTemplate;
        private readonly Func<string, LogLevel, bool> m_Filter;
        private readonly ConcurrentQueue<string> m_LogQueue;

        [ThreadStatic]
        private StringBuilder m_StringBuilder;

        [ThreadStatic]
        private static string s_ScopeName;
        private static List<FileLogger> s_Loggers;

        private StreamWriter WriteStream
        {
            get;
            set;
        }

        private FileInfo WriteFile
        {
            get;
            set;
        }

        static FileLogger()
        {
            s_Loggers = new List<FileLogger>();

            ThreadPool.QueueUserWorkItem((s) =>
            {
                while (true)
                {
                    lock (s_Loggers)
                    {
                        for (var i = 0; i < s_Loggers.Count; i++)
                        {
                            s_Loggers[i].LogWriteToFile();
                        }
                    }
                    Thread.Sleep(1000);
                }
            });
        }

        public FileLogger(string categoryName, Func<string, LogLevel, bool> filter,
            string dir, string template)
        {
            this.m_CategoryName = categoryName;
            this.m_Filter = filter;
            this.m_Directory = dir;
            this.m_FileTemplate = template;
            this.m_LogQueue = new ConcurrentQueue<string>();
            lock (s_Loggers) { s_Loggers.Add(this); }
        }

        private void LogWriteToFile()
        {
            if (WriteFile != null) WriteFile.Refresh();

            if (WriteFile == null || WriteFile.Length >= 1024 * 1024)
            {
                if (WriteStream != null)
                    WriteStream.Dispose();

                if (!Directory.Exists(m_Directory))
                    Directory.CreateDirectory(m_Directory);

                var fileName = $"{m_Directory}\\{m_CategoryName}_{DateTime.Now.ToString(m_FileTemplate)}.txt";
                WriteStream = new StreamWriter(File.OpenWrite(fileName), Encoding.Unicode);
                WriteFile = new FileInfo(fileName);
            }

            while (m_LogQueue.TryDequeue(out string log))
            {
                WriteStream.Write(log);
            }
            WriteStream.Flush();
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            s_ScopeName = state.ToString();

            return new DisposableScope();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return m_Filter == null || m_Filter(m_CategoryName, logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            if (formatter == null) throw new ArgumentNullException(nameof(formatter));

            var message = formatter(state, exception);

            if (!string.IsNullOrEmpty(message) || exception != null)
            {
                if (m_StringBuilder == null)
                    m_StringBuilder = new StringBuilder(1024);

                var loggerBuilder = m_StringBuilder;

                loggerBuilder.Append(m_CategoryName)
                    .Append(" [")
                    .Append(logLevel.ToString())
                    .Append("]");

                if (!string.IsNullOrEmpty(s_ScopeName))
                {
                    loggerBuilder.Append(" [")
                        .Append(s_ScopeName)
                        .Append("]");
                }

                if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
                {
                    loggerBuilder.Append(" [")
                        .Append(eventId)
                        .Append("]");
                }

                loggerBuilder.Append(" [")
                    .Append(DateTime.Now.ToString("yyy-MM-dd HH:mm:ss"))
                    .AppendLine("]");

                if (!string.IsNullOrEmpty(message))
                    loggerBuilder.AppendLine(message);

                if (exception != null)
                    loggerBuilder.AppendLine(exception.ToString());

                m_LogQueue.Enqueue(loggerBuilder.ToString());

                loggerBuilder.Clear();
                if (loggerBuilder.Capacity > 1024)
                    loggerBuilder.Capacity = 1024;
            }
        }
    }
}