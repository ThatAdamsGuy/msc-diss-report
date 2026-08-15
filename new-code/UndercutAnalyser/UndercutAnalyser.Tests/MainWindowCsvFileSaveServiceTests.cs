using System.Text;
using System.Windows;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowCsvFileSaveServiceTests
{
    private sealed class FakeSaveFileDialog : WorkspaceWorkflowService.IMainWindowSaveFileDialog
    {
        public string? Title { get; set; }
        public string Filter { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DefaultExt { get; set; } = string.Empty;
        public bool? ShowDialogResult { get; set; }
        public string? FileNameAfterAccept { get; set; }
        public int ShowDialogCallCount { get; private set; }

        public bool? ShowDialog(Window owner)
        {
            ShowDialogCallCount++;
            if (ShowDialogResult == true && !string.IsNullOrWhiteSpace(FileNameAfterAccept))
            {
                FileName = FileNameAfterAccept;
            }

            return ShowDialogResult;
        }
    }

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
    public void TrySaveCsvCore_WhenDialogReturnsNull_ReturnsFalseAndDoesNotWrite()
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
            showDialog: () => null,
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

    [Fact]
    public void TrySaveCsvWithDialog_MapsOptionsAndWritesWhenAccepted()
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var options = new MainWindowCsvSaveOptions(
            Title: "Export full scenario",
            Filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName: "undercut_scenario_export.json",
            DefaultExt: ".json",
            Encoding: utf8NoBom);

        var dialog = new FakeSaveFileDialog
        {
            ShowDialogResult = true,
            FileNameAfterAccept = "C:\\temp\\export.json"
        };

        string? writtenPath = null;
        string? writtenContent = null;
        Encoding? writtenEncoding = null;

        var result = WorkspaceWorkflowService.TrySaveCsvWithDialog(
            owner: null!,
            csvContent: "{\"ok\":true}",
            options: options,
            dialog: dialog,
            writeAllText: (path, content, encoding) =>
            {
                writtenPath = path;
                writtenContent = content;
                writtenEncoding = encoding;
            });

        Assert.True(result);
        Assert.Equal(1, dialog.ShowDialogCallCount);
        Assert.Equal("Export full scenario", dialog.Title);
        Assert.Equal("JSON files (*.json)|*.json|All files (*.*)|*.*", dialog.Filter);
        Assert.Equal("C:\\temp\\export.json", dialog.FileName);
        Assert.Equal(".json", dialog.DefaultExt);
        Assert.Equal("C:\\temp\\export.json", writtenPath);
        Assert.Equal("{\"ok\":true}", writtenContent);
        Assert.Same(utf8NoBom, writtenEncoding);
    }

    [Fact]
    public void TrySaveCsvWithDialog_WhenCancelled_DoesNotWrite()
    {
        var options = new MainWindowCsvSaveOptions(
            Title: null,
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "output.csv",
            DefaultExt: ".csv",
            Encoding: Encoding.UTF8);

        var dialog = new FakeSaveFileDialog
        {
            ShowDialogResult = false,
            FileName = "C:\\temp\\output.csv"
        };

        var writeCalled = false;

        var result = WorkspaceWorkflowService.TrySaveCsvWithDialog(
            owner: null!,
            csvContent: "a,b",
            options: options,
            dialog: dialog,
            writeAllText: (_, _, _) => writeCalled = true);

        Assert.False(result);
        Assert.Equal(1, dialog.ShowDialogCallCount);
        Assert.False(writeCalled);
    }

    [Fact]
    public void MainWindowCsvSaveOptions_RecordEqualityAndWithBehaviour_AreDeterministic()
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var baseline = new MainWindowCsvSaveOptions(
            Title: "Export",
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "output.csv",
            DefaultExt: ".csv",
            Encoding: utf8NoBom);

        var same = new MainWindowCsvSaveOptions(
            Title: "Export",
            Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName: "output.csv",
            DefaultExt: ".csv",
            Encoding: utf8NoBom);

        var changed = baseline with { FileName = "other.csv" };

        Assert.Equal(baseline, same);
        Assert.NotEqual(baseline, changed);
        Assert.Equal("other.csv", changed.FileName);
        Assert.Equal("output.csv", baseline.FileName);
    }
}
