using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Executes manual prediction requests and converts runtime failures into UI-ready messages.
/// </summary>
public static class MainWindowPredictionExecutionService
{
    /// <summary>
    /// Runs the predictor for a prepared request and returns either a successful prediction result
    /// or a formatted failure message suitable for direct display in the prediction validation area.
    /// </summary>
    public static MainWindowPredictionExecutionResult Execute(PredictionRequest request, IPitSequencePredictor predictor)
    {
        try
        {
            var result = predictor.Predict(request);
            return MainWindowPredictionExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            return MainWindowPredictionExecutionResult.Failure($"Prediction failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Outcome of manual prediction execution.
/// </summary>
public sealed record MainWindowPredictionExecutionResult(
    bool IsSuccess,
    PredictionResult? Result,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    public static MainWindowPredictionExecutionResult Success(PredictionResult result) =>
        new(IsSuccess: true, Result: result, ErrorMessage: null);

    /// <summary>
    /// Creates a failed execution result with a UI-ready message.
    /// </summary>
    public static MainWindowPredictionExecutionResult Failure(string message) =>
        new(IsSuccess: false, Result: null, ErrorMessage: message);
}
