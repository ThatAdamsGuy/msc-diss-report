using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Fully resolved execution payload for one scan candidate, including request, metadata, and display fields.
/// </summary>
public sealed record ScanExecutionContext(
    int AttackerNumber,
    int TargetNumber,
    int DecisionLap,
    int AttackerPosition,
    double InitialGapSeconds,
    string AttackerCode,
    string TargetCode,
    double AttackerReferencePace,
    double TargetReferencePace,
    string AttackerCompound,
    int AttackerTyreAge,
    string TargetCompound,
    int TargetTyreAge,
    PredictionRequest Request);

public static class MainWindowScanRequestBuilder
{
    /// <summary>
    /// Builds one complete scan execution context from a candidate, including code lookup,
    /// tyre-state enrichment, reference pace mapping, and prediction request construction.
    /// </summary>
    public static ScanExecutionContext BuildExecutionContext(
        ScanCandidate candidate,
        IReadOnlyDictionary<int, Driver> driversByNumber,
        IReadOnlyDictionary<int, double> referencePaceByDriver,
        Func<int, int, (string compound, int age)> tyreStateResolver,
        string eventName,
        TyreSetSpecification attackerReplacementTyre,
        TyreSetSpecification targetReplacementTyre,
        int targetResponseLaps,
        LapModelParameters modelParameters)
    {
        var attackerCode = ResolveDriverCode(driversByNumber, candidate.AttackerNumber);
        var targetCode = ResolveDriverCode(driversByNumber, candidate.TargetNumber);

        var attackerRefPace = referencePaceByDriver.GetValueOrDefault(candidate.AttackerNumber, 0.0);
        var targetRefPace = referencePaceByDriver.GetValueOrDefault(candidate.TargetNumber, 0.0);

        var (attackerCompound, attackerTyreAge) = tyreStateResolver(candidate.AttackerNumber, candidate.DecisionLap);
        var (targetCompound, targetTyreAge) = tyreStateResolver(candidate.TargetNumber, candidate.DecisionLap);

        var request = MainWindowPredictionFactory.CreatePredictionRequest(
            eventName: eventName,
            decisionLap: candidate.DecisionLap,
            initialGapSeconds: candidate.InitialGapSeconds,
            attacker: new DriverScenarioInput(attackerCode, attackerCode, candidate.AttackerPosition + 1, attackerRefPace, attackerCompound, attackerTyreAge),
            target: new DriverScenarioInput(targetCode, targetCode, candidate.AttackerPosition, targetRefPace, targetCompound, targetTyreAge),
            attackerReplacementTyre: attackerReplacementTyre,
            targetReplacementTyre: targetReplacementTyre,
            targetResponseLaps: targetResponseLaps,
            modelParameters: modelParameters);

        return new ScanExecutionContext(
            AttackerNumber: candidate.AttackerNumber,
            TargetNumber: candidate.TargetNumber,
            DecisionLap: candidate.DecisionLap,
            AttackerPosition: candidate.AttackerPosition,
            InitialGapSeconds: candidate.InitialGapSeconds,
            AttackerCode: attackerCode,
            TargetCode: targetCode,
            AttackerReferencePace: attackerRefPace,
            TargetReferencePace: targetRefPace,
            AttackerCompound: attackerCompound,
            AttackerTyreAge: attackerTyreAge,
            TargetCompound: targetCompound,
            TargetTyreAge: targetTyreAge,
            Request: request);
    }

    /// <summary>
    /// Resolves a driver's short code for display/export, falling back to the numeric driver number when no code is available.
    /// </summary>
    public static string ResolveDriverCode(IReadOnlyDictionary<int, Driver> driversByNumber, int driverNumber)
    {
        if (!driversByNumber.TryGetValue(driverNumber, out var driver) || string.IsNullOrWhiteSpace(driver.Code))
            return driverNumber.ToString();

        return driver.Code;
    }
}
