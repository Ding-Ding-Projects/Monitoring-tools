using System;
using System.IO;

namespace MonitoringTools.Core
{
    public static class Log
    {
        private static readonly object Gate = new object();

        public static string Folder
        {
            get
            {
                var d = Path.Combine(Settings.DataDir, "logs");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static void Info(string m) => Write("INFO", m);
        public static void Warn(string m) => Write("WARN", m);
        public static void Error(string m, Exception ex = null) => Write("FAIL", ex == null ? m : m + " :: " + ex);

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    var file = Path.Combine(Folder, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                    File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
