using System;
using System.IO;

namespace XeroxGo.PrinterAgent.Services
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XeroxGo",
            "logs"
        );
        private static readonly string LogFilePath = Path.Combine(LogDir, "agent.log");

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }
            }
            catch { }
        }

        public static void Info(string message) => Log("INFO", message);
        public static void Warn(string message) => Log("WARN", message);
        public static void Error(string message, Exception? ex = null)
        {
            string fullMsg = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
            Log("ERROR", fullMsg);
        }

        public static string GetLogFilePath() => LogFilePath;

        private static void Log(string level, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string entry = $"[{timestamp}] [{level}] {message}";

            System.Diagnostics.Debug.WriteLine(entry);

            lock (_lock)
            {
                try
                {
                    // Rotate if log exceeds 5 MB
                    if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > 5 * 1024 * 1024)
                    {
                        string oldLog = Path.Combine(LogDir, "agent.old.log");
                        if (File.Exists(oldLog)) File.Delete(oldLog);
                        File.Move(LogFilePath, oldLog);
                    }

                    File.AppendAllText(LogFilePath, entry + Environment.NewLine);
                }
                catch
                {
                    // Avoid crashing on log write failures
                }
            }
        }
    }
}
