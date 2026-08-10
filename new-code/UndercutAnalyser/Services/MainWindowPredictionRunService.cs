using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Validates, builds, and executes a manual prediction run from normalized UI inputs.
/// </summary>
public static class MainWindowPredictionRunService
{
    /// <summary>
    /// Runs the full manual prediction pipeline and returns either a validation/execution failure
    /// or a successful prediction with driver codes for rendering.
    /// </summary>
    public static MainWindowPredictionRunResult Run(
        MainWindowPredictionRunInput input,
        Func<PredictionRequest, PredictionResult>? predict = null)
    {
        var validation = MainWindowPredictionInputOrchestrator.Validate(
            attacker: input.Attacker,
            target: input.Target,
            decisionLap: input.DecisionLapNumber,
            initialGapText: input.InitialGapText,
            referencePaceByDriver: input.ReferencePaceByDriver);

        if (!validation.IsValid)
            return MainWindowPredictionRunResult.ValidationFailure(validation.ValidationMessage);

        var attacker = validation.Attacker!;
        var target = validation.Target!;
        var decisionLap = validation.DecisionLap;

        var requestData = MainWindowManualPredictionRequestBuilder.Build(
            eventName: input.EventName,
            validation: validation,
            attackerTyreState: input.ResolveTyreState(attacker.DriverNumber, decisionLap),
            targetTyreState: input.ResolveTyreState(target.DriverNumber, decisionLap),
            attackerReplacementCompoundText: input.AttackerReplacementCompoundText,
            targetReplacementCompoundText: input.TargetReplacementCompoundText,
            attackerReplacementAgeText: input.AttackerReplacementAgeText,
            targetReplacementAgeText: input.TargetReplacementAgeText,
            targetResponseLaps: input.TargetResponseLaps,
            modelParameters: input.ModelParameters);

        MainWindowPredictionExecutionResult execution;

        if (predict is null)
        {
            var predictor = new PitSequencePredictor(new LapTimePredictor());
            execution = MainWindowPredictionExecutionService.Execute(requestData.Request, predictor);
        }
        else
        {
            try
            {
                execution = MainWindowPredictionExecutionResult.Success(predict(requestData.Request));
            }
            catch (Exception ex)
            {
                execution = MainWindowPredictionExecutionResult.Failure($"Prediction failed: {ex.Message}");
            }
        }

        if (!execution.IsSuccess || execution.Result is null)
            return MainWindowPredictionRunResult.ExecutionFailure(execution.ErrorMessage);

        return MainWindowPredictionRunResult.Success(
            execution.Result,
            requestData.AttackerCode,
            requestData.TargetCode);
    }
}

/// <summary>
/// Normalized input payload for one manual prediction run.
/// </summary>
public sealed record MainWindowPredictionRunInput(
    string EventName,
    MainWindowPredictionSelection? Attacker,
    MainWindowPredictionSelection? Target,
    int? DecisionLapNumber,
    string? InitialGapText,
    IReadOnlyDictionary<int, double> ReferencePaceByDriver,
    Func<int, int, (string compound, int age)> ResolveTyreState,
    string AttackerReplacementCompoundText,
    string TargetReplacementCompoundText,
    string? AttackerReplacementAgeText,
    string? TargetReplacementAgeText,
    int TargetResponseLaps,
    LapModelParameters ModelParameters);

/// <summary>
/// Outcome of one manual prediction run pipeline.
/// </summary>
public sealed record MainWindowPredictionRunResult(
    bool IsSuccess,
    bool IsValidationFailure,
    string? ErrorMessage,
    PredictionResult? Prediction,
    string AttackerCode,
    string TargetCode)
{
    /// <summary>
    /// Creates a validation-failure result with a UI-ready message.
    /// </summary>
    public static MainWindowPredictionRunResult ValidationFailure(string? message) =>
        new(IsSuccess: false, IsValidationFailure: true, ErrorMessage: message, Prediction: null, AttackerCode: string.Empty, TargetCode: string.Empty);

    /// <summary>
    /// Creates an execution-failure result with a UI-ready message.
    /// </summary>
    public static MainWindowPredictionRunResult ExecutionFailure(string? message) =>
        new(IsSuccess: false, IsValidationFailure: false, ErrorMessage: message, Prediction: null, AttackerCode: string.Empty, TargetCode: string.Empty);

    /// <summary>
    /// Creates a successful prediction run result.
    /// </summary>
    public static MainWindowPredictionRunResult Success(PredictionResult prediction, string attackerCode, string targetCode) =>
        new(IsSuccess: true, IsValidationFailure: false, ErrorMessage: null, Prediction: prediction, AttackerCode: attackerCode, TargetCode: targetCode);
}
