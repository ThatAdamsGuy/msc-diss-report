using System.Text;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowCsvFileSaveServiceTests
{
    [Fact]
    public void TrySaveCsvCore_WhenDialogCancelled_ReturnsFalseAndDoesNotWrite()
    {
        var writeCalled = false;
        var options = new MainWindowCsvSaveOptions(
            Title: null,
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "test.csv",
            DefaultExt: ".csv",
            Encoding: Encoding.UTF8);

        var result = WorkspaceWorkflowService.TrySaveCsvCore(
            csvContent: "a,b",
            options: options,
            showDialog: () => false,
            selectedPath: () => "ignored.csv",
            writeAllText: (_, _, _) => writeCalled = true);

        Assert.False(result);
        Assert.False(writeCalled);
    }

    [Fact]
    public void TrySaveCsvCore_WhenDialogAccepted_WritesWithRequestedEncodingAndReturnsTrue()
    {
        var capturedPath = string.Empty;
        var capturedContent = string.Empty;
        Encoding? capturedEncoding = null;

        var expectedEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var options = new MainWindowCsvSaveOptions(
            Title: "Export",
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "output.csv",
            DefaultExt: ".csv",
            Encoding: expectedEncoding);

        var result = WorkspaceWorkflowService.TrySaveCsvCore(
            csvContent: "x,y",
            options: options,
            showDialog: () => true,
            selectedPath: () => "C:\\temp\\output.csv",
            writeAllText: (path, content, encoding) =>
            {
                capturedPath = path;
                capturedContent = content;
                capturedEncoding = encoding;
            });

        Assert.True(result);
        Assert.Equal("C:\\temp\\output.csv", capturedPath);
        Assert.Equal("x,y", capturedContent);
        Assert.Same(expectedEncoding, capturedEncoding);
    }

    [Fact]
    public void TrySaveCsvCore_WhenWriteThrowsIOException_PropagatesException()
    {
        // Tests that I/O exceptions from write are propagated (not caught)
        var options = new MainWindowCsvSaveOptions(
            Title: "Export",
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "output.csv",
            DefaultExt: ".csv",
            Encoding: Encoding.UTF8);

        void Act() => WorkspaceWorkflowService.TrySaveCsvCore(
            csvContent: "data,goes,here",
            options: options,
            showDialog: () => true,
            selectedPath: () => "C:\\readonly\\protected.csv",
            writeAllText: (_, _, _) => throw new IOException("Access to the path is denied."));

        var exception = Record.Exception(Act);
        Assert.NotNull(exception);
        Assert.IsType<IOException>(exception);
    }

    [Fact]
    public void TrySaveCsvCore_WhenWriteThrowsUnauthorizedAccessException_PropagatesException()
    {
        // Tests that permission exceptions from write are propagated (not caught)
        var options = new MainWindowCsvSaveOptions(
            Title: "Export",
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "readonly.csv",
            DefaultExt: ".csv",
            Encoding: Encoding.UTF8);

        void Act() => WorkspaceWorkflowService.TrySaveCsvCore(
            csvContent: "protected,data",
            options: options,
            showDialog: () => true,
            selectedPath: () => "C:\\Program Files\\protected.csv",
            writeAllText: (_, _, _) => throw new UnauthorizedAccessException("Access denied."));

        var exception = Record.Exception(Act);
        Assert.NotNull(exception);
        Assert.IsType<UnauthorizedAccessException>(exception);
    }

    [Fact]
    public void TrySaveCsvCore_WithSpecialCharactersInContent_HandlesCsvFormatCorrectly()
    {
        // Tests CSV special character handling: commas in quoted fields, newlines, quotes
        var capturedContent = string.Empty;
        var options = new MainWindowCsvSaveOptions(
            Title: "Export",
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "special.csv",
            DefaultExt: ".csv",
            Encoding: Encoding.UTF8);

        var csvWithSpecialChars = "\"Driver Name, Inc.\",\"Lap: 1-5\"\n\"Quote \"\"Test\"\"\",\"End\"";

        var result = WorkspaceWorkflowService.TrySaveCsvCore(
            csvContent: csvWithSpecialChars,
            options: options,
            showDialog: () => true,
            selectedPath: () => "C:\\temp\\special.csv",
            writeAllText: (_, content, _) => capturedContent = content);

        Assert.True(result);
        Assert.Equal(csvWithSpecialChars, capturedContent);
    }
}
