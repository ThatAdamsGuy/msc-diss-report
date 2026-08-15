using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace UndercutAnalyser.Services;

/// <summary>
/// Shared CSV save-dialog and file-write helper for MainWindow export flows.
/// </summary>
public static partial class WorkspaceWorkflowService
{
    /// <summary>
    /// Prompts for a destination file and writes CSV content using the specified encoding.
    /// Returns true when a file is written, false when canceled.
    /// </summary>
    public static bool TrySaveCsv(Window owner, string csvContent, MainWindowCsvSaveOptions options)
    {
        return TrySaveCsvWithDialog(
            owner,
            csvContent,
            options,
            dialog: new SaveFileDialogAdapter(new SaveFileDialog()),
            writeAllText: (path, content, encoding) => File.WriteAllText(path, content, encoding));
    }

    internal static bool TrySaveCsvWithDialog(
        Window owner,
        string csvContent,
        MainWindowCsvSaveOptions options,
        IMainWindowSaveFileDialog dialog,
        Action<string, string, Encoding> writeAllText)
    {
        dialog.Title = options.Title;
        dialog.Filter = options.Filter;
        dialog.FileName = options.FileName;
        dialog.DefaultExt = options.DefaultExt;

        return TrySaveCsvCore(
            csvContent,
            options,
            showDialog: () => dialog.ShowDialog(owner),
            selectedPath: () => dialog.FileName,
            writeAllText: writeAllText);
    }

    internal interface IMainWindowSaveFileDialog
    {
        string? Title { get; set; }
        string Filter { get; set; }
        string FileName { get; set; }
        string DefaultExt { get; set; }
        bool? ShowDialog(Window owner);
    }

    internal sealed class SaveFileDialogAdapter(SaveFileDialog dialog) : IMainWindowSaveFileDialog
    {
        public string? Title
        {
            get => dialog.Title;
            set => dialog.Title = value;
        }

        public string Filter
        {
            get => dialog.Filter;
            set => dialog.Filter = value;
        }

        public string FileName
        {
            get => dialog.FileName;
            set => dialog.FileName = value;
        }

        public string DefaultExt
        {
            get => dialog.DefaultExt;
            set => dialog.DefaultExt = value;
        }

        public bool? ShowDialog(Window owner) => dialog.ShowDialog(owner);
    }

    internal static bool TrySaveCsvCore(
        string csvContent,
        MainWindowCsvSaveOptions options,
        Func<bool?> showDialog,
        Func<string> selectedPath,
        Action<string, string, Encoding> writeAllText)
    {
        if (showDialog() != true)
            return false;

        writeAllText(selectedPath(), csvContent, options.Encoding);
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
