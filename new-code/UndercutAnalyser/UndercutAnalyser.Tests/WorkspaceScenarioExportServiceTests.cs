using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class WorkspaceScenarioExportServiceTests
{
    [Fact]
    public void ResolveDisplayedResultsRows_PrefersSingleScanRows_WhenAvailable()
    {
        var mainRows = new List<ScanRowData> { CreateRow("NOR", "HAM", "Ahead") };
        var singleRows = new List<ScanRowData> { CreateRow("LEC", "RUS", "Behind") };

        var rows = WorkspaceWorkflowService.ResolveDisplayedResultsRows(mainRows, singleRows);

        Assert.Single(rows);
        Assert.Equal("LEC", rows[0].Attacker);
    }

    [Fact]
    public void ResolveDisplayedResultsRows_FallsBackToMainRows_WhenSingleScanRowsEmpty()
    {
        var mainRows = new List<ScanRowData> { CreateRow("NOR", "HAM", "Ahead") };

        var rows = WorkspaceWorkflowService.ResolveDisplayedResultsRows(mainRows, []);

        Assert.Single(rows);
        Assert.Equal("NOR", rows[0].Attacker);
    }

    [Theory]
    [InlineData(true, 0, 0, true)]
    [InlineData(false, 1, 0, true)]
    [InlineData(false, 0, 1, true)]
    [InlineData(false, 0, 0, false)]
    public void HasExportableScenarioData_UsesMeetingOrLoadedData(
        bool hasMeeting,
        int lapCount,
        int driverCount,
        bool expected)
    {
        var laps = Enumerable.Range(1, lapCount)
            .Select(i => new EventLap { DriverNumber = 4, LapNumber = i })
            .ToList();

        var drivers = Enumerable.Range(1, driverCount)
            .Select(i => new Driver { DriverNumber = i, Code = $"D{i}" })
            .ToList();

        var result = WorkspaceWorkflowService.HasExportableScenarioData(
            meetingKey: hasMeeting ? 123 : null,
            laps: laps,
            drivers: drivers);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildScenarioExportPayload_IncludesDisplayedRowsAndCoreSections()
    {
        var mainRows = new List<ScanRowData>
        {
            CreateRow("NOR", "HAM", "Ahead")
        };

        var singleRows = new List<ScanRowData>
        {
            CreateRow("LEC", "RUS", "Behind")
        };

        var payload = WorkspaceWorkflowService.BuildScenarioExportPayload(
            exportedAtUtc: new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            eventDisplay: "Hungarian GP",
            meetingKey: 9001,
            selection: new ScenarioExportSelection(
                AttackerDriverNumber: 16,
                AttackerCode: "LEC",
                TargetDriverNumber: 63,
                TargetCode: "RUS",
                DecisionLap: 44),
            parameters: new ScenarioExportParameters(
                FuelSecondsPer10Kg: 0.3,
                FuelKg: 80,
                TargetResponseLaps: 1,
                MarginalThreshold: 0.25,
                TrafficPenalty: 0.4,
                ApplyAttackerTraffic: true,
                ApplyTargetTraffic: false,
                PitLaneLoss: 21.5,
                ScanMinTyreAge: 5,
                AttackerReplacementCompound: "SOFT",
                AttackerReplacementAge: 0,
                TargetReplacementCompound: "MEDIUM",
                TargetReplacementAge: 2),
            drivers: [new Driver { DriverNumber = 16, Code = "LEC", BroadcastName = "Charles Leclerc" }],
            laps: [new EventLap { DriverNumber = 16, LapNumber = 44 }],
            stints: [new EventStint { DriverNumber = 16, Compound = "SOFT" }],
            raceControlMessages: [new RaceControlMessage { Category = "SafetyCar", Message = "VSC DEPLOYED" }],
            mainScanRows: mainRows,
            singleScanRows: singleRows);

        Assert.Equal("Hungarian GP", payload.EventDisplay);
        Assert.Equal(9001, payload.MeetingKey);
        Assert.Single(payload.Results.MainScanRows);
        Assert.Single(payload.Results.SingleScanRows);
        Assert.Single(payload.Results.DisplayedRows);
        Assert.Equal("LEC", payload.Results.DisplayedRows[0].Attacker);
    }

    [Fact]
    public void SerializeScenarioExport_ProducesJsonContainingCoreSections()
    {
        var payload = WorkspaceWorkflowService.BuildScenarioExportPayload(
            exportedAtUtc: DateTime.UtcNow,
            eventDisplay: "Monza",
            meetingKey: 77,
            selection: new ScenarioExportSelection(
                AttackerDriverNumber: 4,
                AttackerCode: "NOR",
                TargetDriverNumber: 81,
                TargetCode: "PIA",
                DecisionLap: 12),
            parameters: new ScenarioExportParameters(
                FuelSecondsPer10Kg: 0.3,
                FuelKg: 100,
                TargetResponseLaps: 1,
                MarginalThreshold: 0.25,
                TrafficPenalty: 0.2,
                ApplyAttackerTraffic: false,
                ApplyTargetTraffic: true,
                PitLaneLoss: 22,
                ScanMinTyreAge: 6,
                AttackerReplacementCompound: "HARD",
                AttackerReplacementAge: 1,
                TargetReplacementCompound: "MEDIUM",
                TargetReplacementAge: 2),
            drivers: [],
            laps: [],
            stints: [],
            raceControlMessages: [],
            mainScanRows: [],
            singleScanRows: []);

        var json = WorkspaceWorkflowService.SerializeScenarioExport(payload);

        Assert.Contains("\"eventDisplay\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"parameters\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"results\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"data\"", json, StringComparison.OrdinalIgnoreCase);
    }

    private static ScanRowData CreateRow(string attacker, string target, string result)
    {
        return new ScanRowData(
            Attacker: attacker,
            Target: target,
            DecisionLap: 10,
            AttackerCompound: "SOFT",
            AttackerTyreAge: 8,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 9,
            G0: "+1.000",
            GapAtTargetPitLapComplete: "+0.400 s",
            GapAtTargetOutLapComplete: "+0.300 s",
            GapAtBothDriversNormalLapComplete: "+0.200 s",
            DeltaGAtTargetPitLapComplete: "-0.600",
            Result: result);
    }
}
