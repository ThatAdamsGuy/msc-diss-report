using System.Text.Json;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

public static partial class WorkspaceWorkflowService
{
    public static IReadOnlyList<ScanRowData> ResolveDisplayedResultsRows(
        IReadOnlyList<ScanRowData> mainScanRows,
        IReadOnlyList<ScanRowData> singleScanRows)
    {
        if (singleScanRows.Count > 0)
            return singleScanRows;

        if (mainScanRows.Count > 0)
            return mainScanRows;

        return [];
    }

    public static bool HasExportableScenarioData(
        int? meetingKey,
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<Driver> drivers)
    {
        return meetingKey.HasValue || laps.Count > 0 || drivers.Count > 0;
    }

    public static ScenarioExportPayload BuildScenarioExportPayload(
        DateTime exportedAtUtc,
        string eventDisplay,
        int? meetingKey,
        ScenarioExportSelection selection,
        ScenarioExportParameters parameters,
        IReadOnlyList<Driver> drivers,
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<EventStint> stints,
        IReadOnlyList<RaceControlMessage> raceControlMessages,
        IReadOnlyList<ScanRowData> mainScanRows,
        IReadOnlyList<ScanRowData> singleScanRows)
    {
        var displayedRows = ResolveDisplayedResultsRows(mainScanRows, singleScanRows);

        return new ScenarioExportPayload(
            ExportedAtUtc: exportedAtUtc,
            EventDisplay: eventDisplay,
            MeetingKey: meetingKey,
            Selection: selection,
            Parameters: parameters,
            Data: new ScenarioExportData(
                Drivers: drivers,
                Laps: laps,
                Stints: stints,
                RaceControlMessages: raceControlMessages),
            Results: new ScenarioExportResults(
                MainScanRows: mainScanRows,
                SingleScanRows: singleScanRows,
                DisplayedRows: displayedRows));
    }

    public static string SerializeScenarioExport(ScenarioExportPayload payload)
    {
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

public sealed record ScenarioExportPayload(
    DateTime ExportedAtUtc,
    string EventDisplay,
    int? MeetingKey,
    ScenarioExportSelection Selection,
    ScenarioExportParameters Parameters,
    ScenarioExportData Data,
    ScenarioExportResults Results);

public sealed record ScenarioExportSelection(
    int? AttackerDriverNumber,
    string? AttackerCode,
    int? TargetDriverNumber,
    string? TargetCode,
    int? DecisionLap);

public sealed record ScenarioExportParameters(
    double FuelSecondsPer10Kg,
    double FuelKg,
    int TargetResponseLaps,
    double MarginalThreshold,
    double TrafficPenalty,
    bool ApplyAttackerTraffic,
    bool ApplyTargetTraffic,
    double PitLaneLoss,
    int? ScanMinTyreAge,
    string AttackerReplacementCompound,
    int? AttackerReplacementAge,
    string TargetReplacementCompound,
    int? TargetReplacementAge);

public sealed record ScenarioExportData(
    IReadOnlyList<Driver> Drivers,
    IReadOnlyList<EventLap> Laps,
    IReadOnlyList<EventStint> Stints,
    IReadOnlyList<RaceControlMessage> RaceControlMessages);

public sealed record ScenarioExportResults(
    IReadOnlyList<ScanRowData> MainScanRows,
    IReadOnlyList<ScanRowData> SingleScanRows,
    IReadOnlyList<ScanRowData> DisplayedRows);
