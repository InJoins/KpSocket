using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace KpSocket.Logger
{
    public static class FileLoggerExtensions
    {
        public static ILoggerFactory AddFile(this ILoggerFactory factory)
        {
            factory.AddProvider(new FileLoggerProvider(null, "NetLog", "yyyy_MM_dd_HH_mm_ss"));
            return factory;
        }

        public static ILoggerFactory AddFile(this ILoggerFactory factory, Func<string, LogLevel, bool> filter,
            string dir, string template)
        {
            factory.AddProvider(new FileLoggerProvider(filter, dir, template));
            return factory;
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private static ConcurrentDictionary<string, ILogger> s_Loggers =
            new ConcurrentDictionary<string, ILogger>();

        private readonly Func<string, LogLevel, bool> m_Filter;
        private readonly string m_Directory;
        private readonly string m_FileTemplate;

        public FileLoggerProvider(Func<string, LogLevel, bool> filter,
              string dir, string template)
        {
            m_Directory = dir;
            m_FileTemplate = template;
            m_Filter = filter;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return s_Loggers.GetOrAdd(categoryName, s => new FileLogger(categoryName,
                m_Filter, m_Directory, m_FileTemplate));
        }

        bool isDiposed;
        public void Dispose()
        {
            if (isDiposed) return;

            GC.SuppressFinalize(this);
            isDiposed = true;
        }
    }
}