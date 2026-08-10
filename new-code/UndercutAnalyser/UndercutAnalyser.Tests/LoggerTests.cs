using System.Diagnostics;
using UndercutAnalyser.Infrastructure;

namespace UndercutAnalyser.Tests;

public sealed class LoggerTests
{
    [Fact]
    public void Info_WritesTraceLineWithInfoMarker()
    {
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            Logger.Info("hello world");

            Assert.Contains(listener.Messages, m => m.Contains("INFO: hello world", StringComparison.Ordinal));
            Assert.Contains(listener.Messages, m => m.StartsWith("[", StringComparison.Ordinal));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Error_Message_WritesTraceLineWithErrorMarker()
    {
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            Logger.Error("bad input");

            Assert.Contains(listener.Messages, m => m.Contains("ERROR: bad input", StringComparison.Ordinal));
            Assert.Contains(listener.Messages, m => m.StartsWith("[", StringComparison.Ordinal));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Error_Exception_IncludesContextTypeAndMessage()
    {
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var ex = new InvalidOperationException("boom");

            Logger.Error(ex, "LoadAsync");

            Assert.Contains(listener.Messages, m => m.Contains("ERROR:", StringComparison.Ordinal)
                && m.Contains("[LoadAsync]", StringComparison.Ordinal)
                && m.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
                && m.Contains("boom", StringComparison.Ordinal));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Error_Exception_WithNullContext_DoesNotThrow()
    {
        var ex = new Exception("sample");

        var act = () => Logger.Error(ex, context: null);

        var thrown = Record.Exception(act);
        Assert.Null(thrown);
    }

    private sealed class CaptureTraceListener : TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Messages.Add(message);
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Messages.Add(message);
            }
        }
    }
}
