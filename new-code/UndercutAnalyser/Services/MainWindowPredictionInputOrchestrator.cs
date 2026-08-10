using System.Globalization;

namespace UndercutAnalyser.Services;

/// <summary>
/// Centralizes manual prediction input validation and normalization so MainWindow can delegate
/// branching logic to a deterministic, testable service.
/// </summary>
public static class MainWindowPredictionInputOrchestrator
{
    /// <summary>
    /// Validates user inputs for manual prediction and returns either a normalized success payload
    /// or a user-facing validation message that can be shown directly in the UI.
    /// </summary>
    public static PredictionInputValidationResult Validate(
        MainWindowPredictionSelection? attacker,
        MainWindowPredictionSelection? target,
        int? decisionLap,
        string? initialGapText,
        IReadOnlyDictionary<int, double> referencePaceByDriver)
    {
        if (attacker is null)
            return PredictionInputValidationResult.Failure("Select an attacking driver.");

        if (target is null)
            return PredictionInputValidationResult.Failure("Select a target driver.");

        if (attacker.DriverNumber == target.DriverNumber)
            return PredictionInputValidationResult.Failure("Attacking and target drivers must be different.");

        if (!decisionLap.HasValue)
            return PredictionInputValidationResult.Failure("Select a decision lap.");

        if (!double.TryParse(initialGapText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var initialGap))
            return PredictionInputValidationResult.Failure("Initial gap must be numeric.");

        var attackerReferencePace = referencePaceByDriver.GetValueOrDefault(attacker.DriverNumber, 0.0);
        var targetReferencePace = referencePaceByDriver.GetValueOrDefault(target.DriverNumber, 0.0);

        if (attackerReferencePace <= 0 || targetReferencePace <= 0)
        {
            return PredictionInputValidationResult.Failure("Reference pace could not be derived. Adjust data filters or input state.");
        }

        return PredictionInputValidationResult.Success(
            attacker,
            target,
            decisionLap.Value,
            initialGap,
            attackerReferencePace,
            targetReferencePace);
    }
}

/// <summary>
/// Minimal driver selection payload used by prediction-input validation.
/// </summary>
public sealed record MainWindowPredictionSelection(int DriverNumber, string Code, string DisplayName);

/// <summary>
/// Result of validating and normalizing manual prediction inputs.
/// </summary>
public sealed record PredictionInputValidationResult(
    bool IsValid,
    string? ValidationMessage,
    MainWindowPredictionSelection? Attacker,
    MainWindowPredictionSelection? Target,
    int DecisionLap,
    double InitialGapSeconds,
    double AttackerReferencePace,
    double TargetReferencePace)
{
    /// <summary>
    /// Creates a failed validation result with a UI-ready error message.
    /// </summary>
    public static PredictionInputValidationResult Failure(string message) =>
        new(
            IsValid: false,
            ValidationMessage: message,
            Attacker: null,
            Target: null,
            DecisionLap: 0,
            InitialGapSeconds: 0,
            AttackerReferencePace: 0,
            TargetReferencePace: 0);

    /// <summary>
    /// Creates a successful validation result with normalized values ready for request construction.
    /// </summary>
    public static PredictionInputValidationResult Success(
        MainWindowPredictionSelection attacker,
        MainWindowPredictionSelection target,
        int decisionLap,
        double initialGapSeconds,
        double attackerReferencePace,
        double targetReferencePace) =>
        new(
            IsValid: true,
            ValidationMessage: null,
            Attacker: attacker,
            Target: target,
            DecisionLap: decisionLap,
            InitialGapSeconds: initialGapSeconds,
            AttackerReferencePace: attackerReferencePace,
            TargetReferencePace: targetReferencePace);
}
