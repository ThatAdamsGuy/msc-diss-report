using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Validates and executes one single-scan undercut simulation scenario.
/// </summary>
public static class MainWindowSingleScanSimulationService
{
    /// <summary>
    /// Runs one single-scan simulation from normalized panel inputs and returns either a user-facing
    /// validation/execution failure or a successful mapped scan row.
    /// </summary>
    public static MainWindowSingleScanSimulationResult Simulate(
        MainWindowSingleScanSimulationInput input,
        Func<PredictionRequest, PredictionResult> predict)
    {
        if (input.Attacker is null || input.Target is null)
            return MainWindowSingleScanSimulationResult.Failure("Select attacking and target drivers.");

        if (input.Attacker.DriverNumber == input.Target.DriverNumber)
            return MainWindowSingleScanSimulationResult.Failure("Attacking and target drivers must be different.");

        if (!input.DecisionLapNumber.HasValue)
            return MainWindowSingleScanSimulationResult.Failure("Select a target lap.");

        if (!input.StartingGapSeconds.HasValue)
            return MainWindowSingleScanSimulationResult.Failure("Starting gap is not available for this scenario.");

        var attackerRefPace = input.AttackerPaceOverrideSeconds.HasValue && input.AttackerPaceOverrideSeconds.Value > 0
            ? input.AttackerPaceOverrideSeconds.Value
            : input.DerivedReferencePaceByDriver.GetValueOrDefault(input.Attacker.DriverNumber, 0.0);

        var targetRefPace = input.TargetPaceOverrideSeconds.HasValue && input.TargetPaceOverrideSeconds.Value > 0
            ? input.TargetPaceOverrideSeconds.Value
            : input.DerivedReferencePaceByDriver.GetValueOrDefault(input.Target.DriverNumber, 0.0);

        if (attackerRefPace <= 0 || targetRefPace <= 0)
            return MainWindowSingleScanSimulationResult.Failure("Reference pace could not be derived.");

        var request = MainWindowPredictionFactory.CreatePredictionRequest(
            eventName: input.EventName,
            decisionLap: input.DecisionLapNumber.Value,
            initialGapSeconds: input.StartingGapSeconds.Value,
            attacker: new DriverScenarioInput(
                input.Attacker.Code,
                input.Attacker.Code,
                2,
                attackerRefPace,
                input.AttackerCompound,
                input.AttackerTyreAge),
            target: new DriverScenarioInput(
                input.Target.Code,
                input.Target.Code,
                1,
                targetRefPace,
                input.TargetCompound,
                input.TargetTyreAge),
            attackerReplacementTyre: input.AttackerReplacementTyre,
            targetReplacementTyre: input.TargetReplacementTyre,
            targetResponseLaps: input.TargetResponseLaps,
            modelParameters: input.ModelParameters);

        try
        {
            var prediction = predict(request);
            var row = MainWindowScanRowFactory.CreateSuccess(
                attacker: input.Attacker.Code,
                target: input.Target.Code,
                decisionLap: input.DecisionLapNumber.Value,
                attackerCompound: input.AttackerCompound,
                attackerTyreAge: input.AttackerTyreAge,
                targetCompound: input.TargetCompound,
                targetTyreAge: input.TargetTyreAge,
                g0: input.StartingGapSeconds.Value,
                prediction: prediction);

            return MainWindowSingleScanSimulationResult.Success(row, "1 scenario simulated.");
        }
        catch (Exception ex)
        {
            return MainWindowSingleScanSimulationResult.Failure($"Simulation failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Normalized input payload for one single-scan simulation.
/// </summary>
public sealed record MainWindowSingleScanSimulationInput(
    string EventName,
    MainWindowPredictionSelection? Attacker,
    MainWindowPredictionSelection? Target,
    int? DecisionLapNumber,
    double? StartingGapSeconds,
    double? AttackerPaceOverrideSeconds,
    double? TargetPaceOverrideSeconds,
    IReadOnlyDictionary<int, double> DerivedReferencePaceByDriver,
    string AttackerCompound,
    int AttackerTyreAge,
    string TargetCompound,
    int TargetTyreAge,
    TyreSetSpecification AttackerReplacementTyre,
    TyreSetSpecification TargetReplacementTyre,
    int TargetResponseLaps,
    LapModelParameters ModelParameters);

/// <summary>
/// Result of running one single-scan simulation.
/// </summary>
public sealed record MainWindowSingleScanSimulationResult(
    bool IsSuccess,
    string StatusMessage,
    ScanRowData? Row)
{
    /// <summary>
    /// Creates a failed simulation result with a UI-ready status message.
    /// </summary>
    public static MainWindowSingleScanSimulationResult Failure(string statusMessage) =>
        new(IsSuccess: false, StatusMessage: statusMessage, Row: null);

    /// <summary>
    /// Creates a successful simulation result with one mapped scan row.
    /// </summary>
    public static MainWindowSingleScanSimulationResult Success(ScanRowData row, string statusMessage) =>
        new(IsSuccess: true, StatusMessage: statusMessage, Row: row);
}
