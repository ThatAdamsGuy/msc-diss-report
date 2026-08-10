using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace UndercutAnalyser.Services;

/// <summary>
/// Shared CSV save-dialog and file-write helper for MainWindow export flows.
/// </summary>
public static class MainWindowCsvFileSaveService
{
    /// <summary>
    /// Prompts for a destination file and writes CSV content using the specified encoding.
    /// Returns true when a file is written, false when canceled.
    /// </summary>
    public static bool TrySaveCsv(Window owner, string csvContent, MainWindowCsvSaveOptions options)
    {
        var dlg = new SaveFileDialog
        {
            Title = options.Title,
            Filter = options.Filter,
            FileName = options.FileName,
            DefaultExt = options.DefaultExt
        };

        if (dlg.ShowDialog(owner) != true)
            return false;

        File.WriteAllText(dlg.FileName, csvContent, options.Encoding);
        return true;
    }
}

/// <summary>
/// Settings for CSV save dialog presentation and write behavior.
/// </summary>
public sealed record MainWindowCsvSaveOptions(
    string? Title,
    string Filter,
    string FileName,
    string DefaultExt,
    Encoding Encoding);
