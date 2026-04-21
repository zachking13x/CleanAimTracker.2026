using System;
using System.IO;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Centralized error logging service.
    /// Writes timestamped log entries to %LocalAppData%/CleanAimTracker/logs/
    /// </summary>
    public static class LogService
    {
        private static readonly string _logFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "CleanAimTracker", "logs");

        private static readonly object _lock = new();

        /// <summary>Initialize the log folder on startup.</summary>
        public static void Initialize()
        {
            try
            {
                Directory.CreateDirectory(_logFolder);
                Log("INFO", "Application started");
            }
            catch { /* silently fail if folder creation fails */ }
        }

        /// <summary>Log an informational message.</summary>
        public static void Info(string message) => Log("INFO", message);

        /// <summary>Log a warning message.</summary>
        public static void Warn(string message) => Log("WARN", message);

        /// <summary>Log an error message.</summary>
        public static void Error(string message) => Log("ERROR", message);

        /// <summary>Log an exception with full stack trace.</summary>
        public static void Error(string message, Exception ex)
        {
            Log("ERROR", $"{message}: {ex.GetType().Name}: {ex.Message}");
            Log("TRACE", ex.StackTrace ?? "No stack trace available");
        }

        /// <summary>Log an unhandled exception (called from global handler).</summary>
        public static void Fatal(string message, Exception ex)
        {
            Log("FATAL", $"{message}: {ex.GetType().Name}: {ex.Message}");
            Log("TRACE", ex.StackTrace ?? "No stack trace available");
            if (ex.InnerException != null)
                Log("INNER", $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }

        // ── Core log writer ───────────────────────────────────────
        private static void Log(string level, string message)
        {
            try
            {
                string fileName = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(_logFolder, fileName);
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                lock (_lock)
                {
                    File.AppendAllText(filePath, entry + Environment.NewLine);
                }
            }
            catch { /* never throw from the logger */ }
        }

        /// <summary>Clean up log files older than 30 days.</summary>
        public static void CleanOldLogs(int keepDays = 30)
        {
            try
            {
                if (!Directory.Exists(_logFolder)) return;

                var cutoff = DateTime.Now.AddDays(-keepDays);
                foreach (var file in Directory.GetFiles(_logFolder, "log_*.txt"))
                {
                    if (File.GetCreationTime(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch { /* silently fail */ }
        }
    }
}
