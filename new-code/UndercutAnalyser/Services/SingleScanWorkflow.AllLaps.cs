using System.Globalization;

namespace UndercutAnalyser.Services;

/// <summary>
/// Runs multi-lap single-scan workflows while reusing single-run simulation composition.
/// </summary>
public static partial class SingleScanWorkflowService
{
    /// <summary>
    /// Evaluates one attacker-target pairing across all supplied decision laps and returns successful rows.
    /// </summary>
    public static List<ScanRowData> RunAllLaps(
        MainWindowSingleScanAllLapsInput input,
        Func<int, int, int, string> resolveGapText,
        Func<int, int, (string compound, int age)> resolveTyreState)
    {
        var rows = new List<ScanRowData>();

        foreach (var lapNumber in input.DecisionLapNumbers)
        {
            var gapAtLap = resolveGapText(input.Attacker.DriverNumber, input.Target.DriverNumber, lapNumber);
            if (string.IsNullOrEmpty(gapAtLap))
            {
                continue;
            }

            var attackerTyreState = resolveTyreState(input.Attacker.DriverNumber, lapNumber);
            var targetTyreState = resolveTyreState(input.Target.DriverNumber, lapNumber);

            var runInput = new MainWindowSingleScanRunInput(
                EventName: input.EventName,
                Attacker: input.Attacker,
                Target: input.Target,
                DecisionLapNumber: lapNumber,
                StartingGapText: gapAtLap,
                AttackerPaceOverrideText: input.AttackerPaceOverrideText,
                TargetPaceOverrideText: input.TargetPaceOverrideText,
                DerivedReferencePaceByDriver: input.DerivedReferencePaceByDriver,
                AttackerCompound: attackerTyreState.compound,
                AttackerTyreAge: attackerTyreState.age,
                TargetCompound: targetTyreState.compound,
                TargetTyreAge: targetTyreState.age,
                AttackerReplacementCompoundText: input.AttackerReplacementCompoundText,
                AttackerReplacementAgeText: input.AttackerReplacementAgeText,
                TargetReplacementCompoundText: input.TargetReplacementCompoundText,
                TargetReplacementAgeText: input.TargetReplacementAgeText,
                TargetResponseLaps: input.TargetResponseLaps,
                ModelParameters: input.ModelParameters);

            var result = Run(runInput);
            if (result.IsSuccess && result.Row is not null)
            {
                rows.Add(result.Row);
            }
        }

        return rows;
    }

    /// <summary>
    /// Evaluates one attacker across all decision laps, auto-selecting target as the car immediately ahead.
    /// </summary>
    public static List<ScanRowData> RunAllLapsForAttacker(
        MainWindowSingleScanAllLapsForAttackerInput input,
        Func<int, int, int, string> resolveGapText,
        Func<int, int, (string compound, int age)> resolveTyreState)
    {
        var rows = new List<ScanRowData>();
        var lapIndex = ScanWorkflowService.BuildLapIndex(input.Laps);
        var orderPerLap = ScanWorkflowService.BuildOnTrackOrderPerLap(lapIndex);

        foreach (var lapNumber in input.DecisionLapNumbers)
        {
            int? targetDriverNumber = null;
            if (orderPerLap.TryGetValue(lapNumber, out var order))
            {
                var attackerPos = order.IndexOf(input.Attacker.DriverNumber);
                if (attackerPos > 0)
                {
                    targetDriverNumber = order[attackerPos - 1];
                }
            }

            if (!targetDriverNumber.HasValue)
            {
                continue;
            }

            var targetDriver = input.Drivers.FirstOrDefault(d => d.DriverNumber == targetDriverNumber.Value);
            if (targetDriver is null)
            {
                continue;
            }

            var targetCode = string.IsNullOrWhiteSpace(targetDriver.Code)
                ? targetDriver.DriverNumber.ToString(CultureInfo.InvariantCulture)
                : targetDriver.Code;

            var targetDisplayName = string.IsNullOrWhiteSpace(targetDriver.BroadcastName)
                ? targetCode
                : targetDriver.BroadcastName;

            var gapAtLap = resolveGapText(input.Attacker.DriverNumber, targetDriver.DriverNumber, lapNumber);
            if (string.IsNullOrEmpty(gapAtLap))
            {
                continue;
            }

            var attackerTyreState = resolveTyreState(input.Attacker.DriverNumber, lapNumber);
            var targetTyreState = resolveTyreState(targetDriver.DriverNumber, lapNumber);

            var runInput = new MainWindowSingleScanRunInput(
                EventName: input.EventName,
                Attacker: input.Attacker,
                Target: new PredictionSelection(targetDriver.DriverNumber, targetCode, targetDisplayName),
                DecisionLapNumber: lapNumber,
                StartingGapText: gapAtLap,
                AttackerPaceOverrideText: input.AttackerPaceOverrideText,
                TargetPaceOverrideText: input.TargetPaceOverrideText,
                DerivedReferencePaceByDriver: input.DerivedReferencePaceByDriver,
                AttackerCompound: attackerTyreState.compound,
                AttackerTyreAge: attackerTyreState.age,
                TargetCompound: targetTyreState.compound,
                TargetTyreAge: targetTyreState.age,
                AttackerReplacementCompoundText: input.AttackerReplacementCompoundText,
                AttackerReplacementAgeText: input.AttackerReplacementAgeText,
                TargetReplacementCompoundText: input.TargetReplacementCompoundText,
                TargetReplacementAgeText: input.TargetReplacementAgeText,
                TargetResponseLaps: input.TargetResponseLaps,
                ModelParameters: input.ModelParameters);

            var result = Run(runInput);
            if (result.IsSuccess && result.Row is not null)
            {
                rows.Add(result.Row);
            }
        }

        return rows;
    }
}
