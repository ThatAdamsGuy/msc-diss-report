namespace UndercutAnalyser.Services;

/// <summary>
/// Computes render-ready scan result views (filtered rows, status text, export availability) for main and single scan panels.
/// </summary>
public static class MainWindowScanResultsPresenter
{
    /// <summary>
    /// Builds the main-scan view state from all rows and current result-filter toggles.
    /// </summary>
    public static MainScanViewState BuildMainScanViewState(
        IReadOnlyList<ScanRowData> allRows,
        bool? showAhead,
        bool? showMarginal,
        bool? showBehind)
    {
        var filteredRows = allRows
            .Where(row => MainWindowLogic.ShouldShowResult(row.Result, showAhead, showMarginal, showBehind))
            .ToList();

        var opportunityCount = MainWindowLogic.CountOpportunities(filteredRows.Select(r => r.Result));
        var statusText = MainWindowLogic.BuildMainScanStatusText(filteredRows.Count, allRows.Count, opportunityCount);

        return new MainScanViewState(
            FilteredRows: filteredRows,
            StatusText: statusText,
            EnableExport: allRows.Count > 0);
    }

    /// <summary>
    /// Builds the single-scan view state from all rows, base status text, and current result-filter toggles.
    /// </summary>
    public static SingleScanViewState BuildSingleScanViewState(
        IReadOnlyList<ScanRowData> allRows,
        string? baseStatus,
        bool? showAhead,
        bool? showMarginal,
        bool? showBehind)
    {
        var filteredRows = allRows
            .Where(row => MainWindowLogic.ShouldShowResult(row.Result, showAhead, showMarginal, showBehind))
            .ToList();

        if (allRows.Count == 0)
        {
            return new SingleScanViewState(
                FilteredRows: filteredRows,
                StatusText: baseStatus ?? string.Empty,
                EnableExport: false);
        }

        var opportunityCount = MainWindowLogic.CountOpportunities(filteredRows.Select(r => r.Result));
        var summaryPrefix = baseStatus ?? $"{allRows.Count} single-driver scenario{(allRows.Count == 1 ? string.Empty : "s")} scanned.";
        var statusText = MainWindowLogic.BuildSingleScanStatusText(summaryPrefix, filteredRows.Count, opportunityCount);

        return new SingleScanViewState(
            FilteredRows: filteredRows,
            StatusText: statusText,
            EnableExport: allRows.Count > 0);
    }
}

/// <summary>
/// Render-ready state for main scan results panel.
/// </summary>
public sealed record MainScanViewState(
    IReadOnlyList<ScanRowData> FilteredRows,
    string StatusText,
    bool EnableExport);

/// <summary>
/// Render-ready state for single scan results panel.
/// </summary>
public sealed record SingleScanViewState(
    IReadOnlyList<ScanRowData> FilteredRows,
    string StatusText,
    bool EnableExport);
