using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowScanResultsPresenterTests
{
    [Fact]
    public void BuildMainScanViewState_FiltersRowsAndBuildsStatus()
    {
        var rows = new List<ScanRowData>
        {
            CreateRow("Ahead"),
            CreateRow("Marginal"),
            CreateRow("Behind"),
            CreateRow("Error: boom")
        };

        var state = ScanWorkflowService.BuildMainScanViewState(
            allRows: rows,
            showAhead: true,
            showMarginal: false,
            showBehind: true);

        Assert.Equal(3, state.FilteredRows.Count);
        Assert.Contains(state.FilteredRows, r => r.Result == "Ahead");
        Assert.DoesNotContain(state.FilteredRows, r => r.Result == "Marginal");
        Assert.Contains(state.FilteredRows, r => r.Result == "Behind");
        Assert.Contains(state.FilteredRows, r => r.Result.StartsWith("Error:", StringComparison.Ordinal));
        Assert.Equal("3 shown (4 scanned) — 1 opportunity found", state.StatusText);
        Assert.True(state.EnableExport);
    }

    [Fact]
    public void BuildSingleScanViewState_WhenEmpty_UsesBaseStatusAndDisablesExport()
    {
        var state = ScanWorkflowService.BuildSingleScanViewState(
            allRows: [],
            baseStatus: "Select attacking and target drivers.",
            showAhead: true,
            showMarginal: true,
            showBehind: true);

        Assert.Empty(state.FilteredRows);
        Assert.Equal("Select attacking and target drivers.", state.StatusText);
        Assert.False(state.EnableExport);
    }

    [Fact]
    public void BuildSingleScanViewState_WhenRowsPresent_UsesDefaultSummaryPrefix()
    {
        var rows = new List<ScanRowData>
        {
            CreateRow("Ahead"),
            CreateRow("Behind")
        };

        var state = ScanWorkflowService.BuildSingleScanViewState(
            allRows: rows,
            baseStatus: null,
            showAhead: true,
            showMarginal: true,
            showBehind: false);

        Assert.Single(state.FilteredRows);
        Assert.Equal("Ahead", state.FilteredRows[0].Result);
        Assert.Equal("2 single-driver scenarios scanned. — 1 shown, 1 opportunity found", state.StatusText);
        Assert.True(state.EnableExport);
    }

    private static ScanRowData CreateRow(string result) =>
        new(
            Attacker: "NOR",
            Target: "PIA",
            DecisionLap: 10,
            AttackerCompound: "SOFT",
            AttackerTyreAge: 8,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 10,
            G0: "+1.000",
            GapAtTargetPitLapComplete: "+0.500 s",
            GapAtTargetOutLapComplete: "+0.300 s",
            GapAtBothDriversNormalLapComplete: "+0.200 s",
            DeltaGAtTargetPitLapComplete: "-0.500",
            Result: result);
}
