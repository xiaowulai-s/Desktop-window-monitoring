using System;
using System.Diagnostics;
using System.IO;

namespace WindowMonitor.Business
{
    public static class DebugLogger
    {
        private static readonly string LogFilePath;
        private static readonly object LockObj = new object();

        static DebugLogger()
        {
            LogFilePath = Path.Combine(Path.GetTempPath(), "WindowMonitor_debug.log");
        }

        public static void Log(string message)
        {
            lock (LockObj)
            {
                try
                {
                    string logLine = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                    File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
                    Debug.WriteLine(message);
                }
                catch
                {
                    // Ignore logging errors
                }
            }
        }

        public static void Clear()
        {
            lock (LockObj)
            {
                try
                {
                    if (File.Exists(LogFilePath))
                        File.Delete(LogFilePath);
                }
                catch { }
            }
        }

        public static string GetLogPath() => LogFilePath;
    }
}