using System.Diagnostics;
using System.Reflection;
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

    [Fact]
    public void Info_WithNullMessage_DoesNotThrow()
    {
        // Tests graceful handling of null message (defensive programming)
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var act = () => Logger.Info(null!);

            var thrown = Record.Exception(act);
            // Either doesn't throw, or throws ArgumentNullException (both acceptable)
            // Verify it doesn't cause unexpected exceptions
            if (thrown is not null)
            {
                Assert.IsType<ArgumentNullException>(thrown);
            }
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Info_WithEmptyMessage_WritesTraceLineWithInfoMarker()
    {
        // Tests that empty string messages are logged (not filtered)
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            Logger.Info(string.Empty);

            Assert.Contains(listener.Messages, m => m.Contains("INFO:", StringComparison.Ordinal));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Info_WithVeryLongMessage_WritesFullContent()
    {
        // Tests that long messages are not truncated
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var longMessage = string.Concat(Enumerable.Repeat("A", 10000));
            Logger.Info(longMessage);

            Assert.Contains(listener.Messages, m => m.Contains(longMessage, StringComparison.Ordinal));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Error_Message_WithSpecialCharacters_IsLogged()
    {
        // Tests logging of messages with special characters (newlines, tabs, unicode)
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var specialMessage = "Error:\n\tTab\t'Quote' \"DoubleQuote\" \\Backslash\\ \u2764 Unicode";
            Logger.Error(specialMessage);

            Assert.Contains(listener.Messages, m => m.Contains("ERROR:", StringComparison.Ordinal));
            // Verify the message content made it through (special chars preserved)
            Assert.Contains(listener.Messages, m => m.Contains("\u2764", StringComparison.Ordinal));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Error_Exception_WithInnerExceptions_IncludesContext()
    {
        // Tests logging of nested exception chains
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var innerEx = new ArgumentException("Invalid argument");
            var outerEx = new InvalidOperationException("Operation failed", innerEx);

            Logger.Error(outerEx, "ParseData");

            var logged = string.Join(" | ", listener.Messages);
            // Should contain reference to the outer exception type and context
            Assert.Contains("InvalidOperationException", logged);
            Assert.Contains("ParseData", logged);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void Info_MultipleConsecutiveCalls_AllLogged()
    {
        // Tests that multiple calls accumulate and are all logged
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            Logger.Info("First message");
            Logger.Info("Second message");
            Logger.Info("Third message");

            Assert.NotEmpty(listener.Messages);
            Assert.Contains(listener.Messages, m => m.Contains("First message"));
            Assert.Contains(listener.Messages, m => m.Contains("Second message"));
            Assert.Contains(listener.Messages, m => m.Contains("Third message"));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public void FormatMessage_IncludesIsoUtcPrefixAndLevel()
    {
        var method = typeof(Logger).GetMethod("FormatMessage", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var formatted = method!.Invoke(null, ["INFO", "payload"]) as string;

        Assert.NotNull(formatted);
        Assert.Contains("INFO: payload", formatted, StringComparison.Ordinal);
        Assert.StartsWith("[", formatted, StringComparison.Ordinal);
        Assert.Contains("] INFO:", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void InfoCore_WhenDebuggerAttached_CallsDebuggerLog()
    {
        var debuggerLogCalled = false;

        Logger.InfoCore(
            message: "attached",
            isDebuggerAttached: () => true,
            debuggerLog: (_, _, _) => debuggerLogCalled = true);

        Assert.True(debuggerLogCalled);
    }

    [Fact]
    public void ErrorCore_WhenDebuggerNotAttached_DoesNotCallDebuggerLog()
    {
        var debuggerLogCalled = false;

        Logger.ErrorCore(
            message: "detached",
            isDebuggerAttached: () => false,
            debuggerLog: (_, _, _) => debuggerLogCalled = true);

        Assert.False(debuggerLogCalled);
    }

    [Fact]
    public void InfoCore_WhenDebuggerNotAttached_DoesNotCallDebuggerLog()
    {
        var debuggerLogCalled = false;

        Logger.InfoCore(
            message: "detached",
            isDebuggerAttached: () => false,
            debuggerLog: (_, _, _) => debuggerLogCalled = true);

        Assert.False(debuggerLogCalled);
    }

    [Fact]
    public void ErrorCore_Exception_WhenDebuggerAttached_CallsDebuggerLog()
    {
        var debuggerLogCalled = false;

        Logger.ErrorCore(
            ex: new InvalidOperationException("boom"),
            context: "TestCtx",
            isDebuggerAttached: () => true,
            debuggerLog: (_, _, _) => debuggerLogCalled = true);

        Assert.True(debuggerLogCalled);
    }

    [Fact]
    public void ErrorCore_Exception_WithEmptyContext_UsesNoBracketPrefix()
    {
        var listener = new CaptureTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            Logger.ErrorCore(
                ex: new InvalidOperationException("boom"),
                context: string.Empty,
                isDebuggerAttached: () => false,
                debuggerLog: (_, _, _) => { });

            Assert.Contains(listener.Messages, m => m.Contains("ERROR:", StringComparison.Ordinal));
            Assert.DoesNotContain(listener.Messages, m => m.Contains("[]", StringComparison.Ordinal));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
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
