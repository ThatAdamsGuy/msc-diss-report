using System.Globalization;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Composes and runs a single-scan simulation from normalized UI inputs.
/// </summary>
public static partial class SingleScanWorkflowService
{
    /// <summary>
    /// Builds a simulation request payload from normalized text/select inputs and executes it.
    /// </summary>
    public static MainWindowSingleScanSimulationResult Run(
        MainWindowSingleScanRunInput input,
        Func<PredictionRequest, PredictionResult>? predict = null)
    {
        var simulationInput = new MainWindowSingleScanSimulationInput(
            EventName: input.EventName,
            Attacker: input.Attacker,
            Target: input.Target,
            DecisionLapNumber: input.DecisionLapNumber,
            StartingGapSeconds: ParseNullableDouble(input.StartingGapText),
            AttackerPaceOverrideSeconds: ParseNullableDouble(input.AttackerPaceOverrideText),
            TargetPaceOverrideSeconds: ParseNullableDouble(input.TargetPaceOverrideText),
            DerivedReferencePaceByDriver: input.DerivedReferencePaceByDriver,
            AttackerCompound: input.AttackerCompound,
            AttackerTyreAge: input.AttackerTyreAge,
            TargetCompound: input.TargetCompound,
            TargetTyreAge: input.TargetTyreAge,
            AttackerReplacementTyre: new TyreSetSpecification(
                TyreCompoundParser.FromOpenF1String(input.AttackerReplacementCompoundText),
                ParseIntOrZero(input.AttackerReplacementAgeText)),
            TargetReplacementTyre: new TyreSetSpecification(
                TyreCompoundParser.FromOpenF1String(input.TargetReplacementCompoundText),
                ParseIntOrZero(input.TargetReplacementAgeText)),
            TargetResponseLaps: input.TargetResponseLaps,
            ModelParameters: input.ModelParameters);

        var predictionRunner = predict ?? (request =>
        {
            var predictor = new PitSequencePredictor(new LapTimePredictor());
            return predictor.Predict(request);
        });

        return SingleScanWorkflowService.Simulate(simulationInput, predictionRunner);
    }

    private static double? ParseNullableDouble(string? text)
    {
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int ParseIntOrZero(string? text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}

/// <summary>
/// Normalized input payload for single-scan orchestration.
/// </summary>
public sealed record MainWindowSingleScanRunInput(
    string EventName,
    PredictionSelection? Attacker,
    PredictionSelection? Target,
    int? DecisionLapNumber,
    string? StartingGapText,
    string? AttackerPaceOverrideText,
    string? TargetPaceOverrideText,
    IReadOnlyDictionary<int, double> DerivedReferencePaceByDriver,
    string AttackerCompound,
    int AttackerTyreAge,
    string TargetCompound,
    int TargetTyreAge,
    string AttackerReplacementCompoundText,
    string? AttackerReplacementAgeText,
    string TargetReplacementCompoundText,
    string? TargetReplacementAgeText,
    int TargetResponseLaps,
    LapModelParameters ModelParameters);
