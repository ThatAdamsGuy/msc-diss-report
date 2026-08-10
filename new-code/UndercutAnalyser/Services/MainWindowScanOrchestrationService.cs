using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Orchestrates full-scan execution by composing candidate selection, request building, model execution, and row mapping.
/// </summary>
public static class MainWindowScanOrchestrationService
{
    /// <summary>
    /// Runs the full scan for selected drivers and returns normalized scan rows.
    /// </summary>
    public static List<ScanRowData> Run(MainWindowScanOrchestrationInput input)
    {
        var referencePaceByDriver = MainWindowReferencePaceService.DeriveReferencePacePerDriver(
            input.Laps,
            input.SafetyCarWindows);

        var lapIndex = MainWindowScanCandidateService.BuildLapIndex(input.Laps);
        var orderPerLap = MainWindowScanCandidateService.BuildOnTrackOrderPerLap(lapIndex);
        var driversByNumber = input.Drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var candidates = MainWindowScanCandidateService.FindCandidates(
            driverNumbers: input.DriverNumbers,
            lapIndex: lapIndex,
            orderPerLap: orderPerLap,
            referencePaceByDriver: referencePaceByDriver,
            safetyCarWindows: input.SafetyCarWindows,
            minAge: input.MinTyreAge,
            tyreStateResolver: input.TyreStateResolver);

        var contexts = candidates
            .Select(candidate => MainWindowScanRequestBuilder.BuildExecutionContext(
                candidate: candidate,
                driversByNumber: driversByNumber,
                referencePaceByDriver: referencePaceByDriver,
                tyreStateResolver: input.TyreStateResolver,
                eventName: input.EventName,
                attackerReplacementTyre: input.AttackerReplacementTyre,
                targetReplacementTyre: input.TargetReplacementTyre,
                targetResponseLaps: input.TargetResponseLaps,
                modelParameters: input.ModelParameters))
            .ToList();

        var predictor = new PitSequencePredictor(new LapTimePredictor());
        return MainWindowScanExecutor.Execute(contexts, predictor);
    }
}

/// <summary>
/// Input payload for full-scan orchestration.
/// </summary>
public sealed record MainWindowScanOrchestrationInput(
    IReadOnlyList<int> DriverNumbers,
    int MinTyreAge,
    string EventName,
    TyreSetSpecification AttackerReplacementTyre,
    TyreSetSpecification TargetReplacementTyre,
    int TargetResponseLaps,
    LapModelParameters ModelParameters,
    IReadOnlyList<TimeWindow> SafetyCarWindows,
    IReadOnlyList<EventLap> Laps,
    IReadOnlyList<Driver> Drivers,
    Func<int, int, (string compound, int age)> TyreStateResolver);
