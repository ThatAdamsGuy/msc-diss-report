using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Derives single-scan scenario state (target suggestion, pace, tyre state, and starting gap)
/// from current selections and loaded session data.
/// </summary>
public static partial class SingleScanWorkflowService
{
    /// <summary>
    /// Computes single-scan scenario details for the current attacker/lap selection.
    /// </summary>
    public static MainWindowSingleScanScenarioResult Derive(MainWindowSingleScanScenarioInput input)
    {
        if (input.Attacker is null || !input.DecisionLapNumber.HasValue)
            return MainWindowSingleScanScenarioResult.Empty;

        var lapNumber = input.DecisionLapNumber.Value;

        var lapIndex = input.Laps
            .GroupBy(l => l.DriverNumber)
            .ToDictionary(g => g.Key,
                g => g.Where(l => l.DateStart.HasValue && l.LapDuration.HasValue)
                      .ToDictionary(l => l.LapNumber));

        if (!lapIndex.TryGetValue(input.Attacker.DriverNumber, out var attackerLapMap) ||
            !attackerLapMap.TryGetValue(lapNumber, out var attackerLap) ||
            !attackerLap.LapTimeAtSectorTwoLine.HasValue)
        {
            return MainWindowSingleScanScenarioResult.Empty;
        }

        int? suggestedAheadDriverNumber = null;
        var orderPerLap = ScanWorkflowService.BuildOnTrackOrderPerLap(lapIndex);
        if (orderPerLap.TryGetValue(lapNumber, out var order))
        {
            var attackerPos = order.IndexOf(input.Attacker.DriverNumber);
            if (attackerPos > 0)
                suggestedAheadDriverNumber = order[attackerPos - 1];
        }

        var effectiveTargetDriverNumber = input.SelectedTarget?.DriverNumber;
        var suggestedIsAvailable = suggestedAheadDriverNumber.HasValue &&
                                   input.AvailableTargetDriverNumbers.Contains(suggestedAheadDriverNumber.Value);

        if (input.PreferSuggestedTarget && suggestedIsAvailable)
        {
            effectiveTargetDriverNumber = suggestedAheadDriverNumber;
        }
        else if (!effectiveTargetDriverNumber.HasValue && suggestedIsAvailable)
        {
            effectiveTargetDriverNumber = suggestedAheadDriverNumber;
        }

        if (!effectiveTargetDriverNumber.HasValue)
            return MainWindowSingleScanScenarioResult.Empty;

        var safetyCarWindows = RaceTimingDomainLogic.BuildSafetyCarWindows(input.RaceControlMessages);
        var referencePace = RaceTraceWorkflowService.DeriveReferencePacePerDriver(input.Laps, safetyCarWindows);

        var (attackerCompound, attackerTyreAge) = WorkspaceWorkflowService.ResolveTyreStateAtLap(
            input.Stints,
            input.Laps,
            input.Attacker.DriverNumber,
            lapNumber);

        var (targetCompound, targetTyreAge) = WorkspaceWorkflowService.ResolveTyreStateAtLap(
            input.Stints,
            input.Laps,
            effectiveTargetDriverNumber.Value,
            lapNumber);

        var startingGapSeconds = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps: input.Laps,
            attackerDriverNumber: input.Attacker.DriverNumber,
            targetDriverNumber: effectiveTargetDriverNumber.Value,
            decisionLapNumber: lapNumber);

        return new MainWindowSingleScanScenarioResult(
            HasScenario: true,
            SuggestedAheadDriverNumber: suggestedAheadDriverNumber,
            EffectiveTargetDriverNumber: effectiveTargetDriverNumber,
            DecisionLapNumber: lapNumber,
            AttackerReferencePace: referencePace.GetValueOrDefault(input.Attacker.DriverNumber, 0.0),
            TargetReferencePace: referencePace.GetValueOrDefault(effectiveTargetDriverNumber.Value, 0.0),
            AttackerCompound: attackerCompound,
            AttackerTyreAge: attackerTyreAge,
            TargetCompound: targetCompound,
            TargetTyreAge: targetTyreAge,
            StartingGapSeconds: startingGapSeconds);
    }
}

/// <summary>
/// Input payload for deriving single-scan scenario values.
/// </summary>
public sealed record MainWindowSingleScanScenarioInput(
    PredictionSelection? Attacker,
    PredictionSelection? SelectedTarget,
    int? DecisionLapNumber,
    IReadOnlyCollection<int> AvailableTargetDriverNumbers,
    IReadOnlyList<EventLap> Laps,
    IReadOnlyList<EventStint> Stints,
    IReadOnlyList<RaceControlMessage> RaceControlMessages,
    bool PreferSuggestedTarget);

/// <summary>
/// Derived single-scan scenario state used by MainWindow field population.
/// </summary>
public sealed record MainWindowSingleScanScenarioResult(
    bool HasScenario,
    int? SuggestedAheadDriverNumber,
    int? EffectiveTargetDriverNumber,
    int DecisionLapNumber,
    double AttackerReferencePace,
    double TargetReferencePace,
    string AttackerCompound,
    int AttackerTyreAge,
    string TargetCompound,
    int TargetTyreAge,
    double? StartingGapSeconds)
{
    /// <summary>
    /// Empty scenario result indicating the UI should clear single-scan scenario fields.
    /// </summary>
    public static MainWindowSingleScanScenarioResult Empty => new(
        HasScenario: false,
        SuggestedAheadDriverNumber: null,
        EffectiveTargetDriverNumber: null,
        DecisionLapNumber: 0,
        AttackerReferencePace: 0.0,
        TargetReferencePace: 0.0,
        AttackerCompound: string.Empty,
        AttackerTyreAge: 0,
        TargetCompound: string.Empty,
        TargetTyreAge: 0,
        StartingGapSeconds: null);
}
