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

        var result = MainWindowCsvFileSaveService.TrySaveCsvCore(
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

        var result = MainWindowCsvFileSaveService.TrySaveCsvCore(
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
}
