using System;
using System.Diagnostics;

namespace UndercutAnalyser.Infrastructure
{
    /// <summary>
    /// Simple logging wrapper to ensure messages appear in the Visual Studio Output window.
    /// Writes to Debug, Trace and Debugger output when attached.
    /// </summary>
    internal static class Logger
    {
        /// <summary>
        /// Writes an informational diagnostic message to trace/debug outputs.
        /// </summary>
        public static void Info(string message)
        {
            var m = FormatMessage("INFO", message);
            Trace.WriteLine(m);
            Debug.WriteLine(m);
            if (Debugger.IsAttached)
            {
                Debugger.Log(0, "UndercutAnalyser", m + "\n");
            }
        }

        /// <summary>
        /// Writes an error diagnostic message to trace/debug outputs.
        /// </summary>
        public static void Error(string message)
        {
            var m = FormatMessage("ERROR", message);
            Trace.WriteLine(m);
            Debug.WriteLine(m);
            if (Debugger.IsAttached)
            {
                Debugger.Log(0, "UndercutAnalyser", m + "\n");
            }
        }

        /// <summary>
        /// Writes an exception diagnostic message, including stack trace and optional context.
        /// </summary>
        public static void Error(Exception ex, string? context = null)
        {
            var ctx = string.IsNullOrEmpty(context) ? string.Empty : $" [{context}]";
            var m = FormatMessage("ERROR", $"{ctx} {ex.GetType()}: {ex.Message}\n{ex.StackTrace}");
            Trace.WriteLine(m);
            Debug.WriteLine(m);
            if (Debugger.IsAttached)
            {
                Debugger.Log(0, "UndercutAnalyser", m + "\n");
            }
        }

        /// <summary>
        /// Formats a UTC timestamped log line with severity level.
        /// </summary>
        private static string FormatMessage(string level, string message)
        {
            return $"[{DateTime.UtcNow:O}] {level}: {message}";
        }
    }
}
