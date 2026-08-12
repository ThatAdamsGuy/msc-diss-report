using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowRaceTracePredictionMarkerServiceTests
{
    [Fact]
    public void ResolvePredictionMarkerSelections_ReturnsEmpty_WhenMarkersDisabled()
    {
        var rows = new List<ScanRowData>
        {
            CreateRow(attacker: "NOR", target: "PIA", lap: 10, result: "Ahead")
        };

        var selections = RaceTraceWorkflowService.ResolvePredictionMarkerSelections(
            rows,
            drivers: [new Driver { DriverNumber = 4, Code = "NOR" }],
            options: new RaceTracePredictionMarkerOptions(
                ShowMarkers: false,
                ShowGuides: true,
                OnlySelectedAttacker: false,
                ShowAhead: true,
                ShowMarginal: true,
                ShowBehind: true),
            selectedAttackerDriverNumber: null);

        Assert.Empty(selections);
    }

    [Fact]
    public void ResolvePredictionMarkerSelections_FiltersByOutcomeAndMapsDriverCode()
    {
        var rows = new List<ScanRowData>
        {
            CreateRow(attacker: "NOR", target: "PIA", lap: 10, result: "Ahead"),
            CreateRow(attacker: "NOR", target: "PIA", lap: 11, result: "Marginal"),
            CreateRow(attacker: "NOR", target: "PIA", lap: 12, result: "Behind")
        };

        var selections = RaceTraceWorkflowService.ResolvePredictionMarkerSelections(
            rows,
            drivers: [new Driver { DriverNumber = 4, Code = "NOR" }],
            options: new RaceTracePredictionMarkerOptions(
                ShowMarkers: true,
                ShowGuides: true,
                OnlySelectedAttacker: false,
                ShowAhead: true,
                ShowMarginal: false,
                ShowBehind: true),
            selectedAttackerDriverNumber: null);

        Assert.Equal(2, selections.Count);
        Assert.All(selections, s => Assert.Equal(4, s.AttackerDriverNumber));
        Assert.Contains(selections, s => s.Result == "Ahead");
        Assert.Contains(selections, s => s.Result == "Behind");
        Assert.DoesNotContain(selections, s => s.Result == "Marginal");
    }

    [Fact]
    public void ResolvePredictionMarkerSelections_OnlySelectedAttacker_ExcludesOthers()
    {
        var rows = new List<ScanRowData>
        {
            CreateRow(attacker: "NOR", target: "PIA", lap: 10, result: "Ahead"),
            CreateRow(attacker: "LEC", target: "HAM", lap: 10, result: "Behind")
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 4, Code = "NOR" },
            new() { DriverNumber = 16, Code = "LEC" }
        };

        var selections = RaceTraceWorkflowService.ResolvePredictionMarkerSelections(
            rows,
            drivers,
            options: new RaceTracePredictionMarkerOptions(
                ShowMarkers: true,
                ShowGuides: true,
                OnlySelectedAttacker: true,
                ShowAhead: true,
                ShowMarginal: true,
                ShowBehind: true),
            selectedAttackerDriverNumber: 4);

        var only = Assert.Single(selections);
        Assert.Equal(4, only.AttackerDriverNumber);
        Assert.Equal("NOR", only.AttackerToken);
    }

    [Theory]
    [InlineData("Ahead", "▲")]
    [InlineData("Marginal", "■")]
    [InlineData("Behind", "▼")]
    [InlineData("Error: x", "")]
    public void ResolveMarkerSymbol_UsesNonCircleSymbols(string result, string expected)
    {
        var symbol = RaceTraceWorkflowService.ResolveMarkerSymbol(result);
        Assert.Equal(expected, symbol);
    }

    [Fact]
    public void TryResolveSeriesPointAtLap_ReturnsPoint_WhenLapExists()
    {
        var series = new RaceTraceDriverSeries(
            DriverNumber: 4,
            DriverName: "Norris",
            DriverCode: "NOR",
            TeamColour: "FF8000",
            IsSolidLine: true,
            IsVisible: true,
            Xs: [9, 10, 11],
            Ys: [0.1, 0.2, 0.3]);

        var found = RaceTraceWorkflowService.TryResolveSeriesPointAtLap(series, decisionLap: 10, out var x, out var y);

        Assert.True(found);
        Assert.Equal(10, x, 5);
        Assert.Equal(0.2, y, 5);
    }

    [Fact]
    public void TryResolveSeriesPointAtLap_ReturnsFalse_WhenLapMissing()
    {
        var series = new RaceTraceDriverSeries(
            DriverNumber: 4,
            DriverName: "Norris",
            DriverCode: "NOR",
            TeamColour: "FF8000",
            IsSolidLine: true,
            IsVisible: true,
            Xs: [9, 10, 11],
            Ys: [0.1, 0.2, 0.3]);

        var found = RaceTraceWorkflowService.TryResolveSeriesPointAtLap(series, decisionLap: 12, out _, out _);

        Assert.False(found);
    }

    private static ScanRowData CreateRow(string attacker, string target, int lap, string result)
    {
        return new ScanRowData(
            Attacker: attacker,
            Target: target,
            DecisionLap: lap,
            AttackerCompound: "SOFT",
            AttackerTyreAge: 10,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 8,
            G0: "+0.500",
            GapAtTargetPitLapComplete: "+0.200 s",
            GapAtTargetOutLapComplete: "+0.100 s",
            GapAtBothDriversNormalLapComplete: "+0.050 s",
            DeltaGAtTargetPitLapComplete: "-0.300",
            Result: result);
    }
}
