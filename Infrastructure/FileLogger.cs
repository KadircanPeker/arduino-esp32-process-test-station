using System;
using System.IO;

namespace ProcessTestApp.Infrastructure
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public static class FileLogger
    {
        private static readonly object LockObj = new object();

        public static void Log(LogLevel level, string source, string message)
        {
            try
            {
                string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string fileName = string.Format("system_log_{0:yyyy_MM_dd}.txt", DateTime.Now);
                string filePath = Path.Combine(directoryPath, fileName);

                string formattedMessage = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] ({2}) => {3}",
                    DateTime.Now, level.ToString().ToUpper(), source, message);

                lock (LockObj)
                {
                    using (StreamWriter sw = File.AppendText(filePath))
                    {
                        sw.WriteLine(formattedMessage);
                    }
                }
            }
            catch
            {
                // Hata durumunda uygulamanın çökmesini engelle
            }
        }

        public static void Trace(string source, string message) { Log(LogLevel.Trace, source, message); }
        public static void Debug(string source, string message) { Log(LogLevel.Debug, source, message); }
        public static void Info(string source, string message) { Log(LogLevel.Info, source, message); }
        public static void Warning(string source, string message) { Log(LogLevel.Warning, source, message); }
        public static void Warn(string source, string message) { Log(LogLevel.Warning, source, message); }
        public static void Error(string source, string message) { Log(LogLevel.Error, source, message); }
        public static void Critical(string source, string message) { Log(LogLevel.Critical, source, message); }
    }
}
