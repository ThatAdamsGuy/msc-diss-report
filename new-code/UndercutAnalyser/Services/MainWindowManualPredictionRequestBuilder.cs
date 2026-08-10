using System.Globalization;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds a manual prediction request from already-validated selection inputs and UI text values.
/// Keeps parsing/fallback behavior deterministic and testable outside MainWindow code-behind.
/// </summary>
public static class MainWindowManualPredictionRequestBuilder
{
    /// <summary>
    /// Creates a full prediction request and exposes attacker/target codes used for result rendering.
    /// Replacement tyre ages default to zero when parsing fails.
    /// </summary>
    public static MainWindowManualPredictionRequest Build(
        string eventName,
        PredictionInputValidationResult validation,
        (string compound, int age) attackerTyreState,
        (string compound, int age) targetTyreState,
        string attackerReplacementCompoundText,
        string targetReplacementCompoundText,
        string? attackerReplacementAgeText,
        string? targetReplacementAgeText,
        int targetResponseLaps,
        LapModelParameters modelParameters)
    {
        var attacker = validation.Attacker!;
        var target = validation.Target!;

        var attackerReplacementAge = int.TryParse(attackerReplacementAgeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ara)
            ? ara
            : 0;
        var targetReplacementAge = int.TryParse(targetReplacementAgeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tra)
            ? tra
            : 0;

        var request = MainWindowPredictionFactory.CreatePredictionRequest(
            eventName: eventName,
            decisionLap: validation.DecisionLap,
            initialGapSeconds: validation.InitialGapSeconds,
            attacker: new DriverScenarioInput(attacker.Code, attacker.DisplayName, 2, validation.AttackerReferencePace, attackerTyreState.compound, attackerTyreState.age),
            target: new DriverScenarioInput(target.Code, target.DisplayName, 1, validation.TargetReferencePace, targetTyreState.compound, targetTyreState.age),
            attackerReplacementTyre: new TyreSetSpecification(TyreCompoundParser.FromOpenF1String(attackerReplacementCompoundText), attackerReplacementAge),
            targetReplacementTyre: new TyreSetSpecification(TyreCompoundParser.FromOpenF1String(targetReplacementCompoundText), targetReplacementAge),
            targetResponseLaps: targetResponseLaps,
            modelParameters: modelParameters);

        return new MainWindowManualPredictionRequest(request, attacker.Code, target.Code);
    }
}

/// <summary>
/// Result wrapper for manual prediction request assembly, including the codes used for UI rendering.
/// </summary>
public sealed record MainWindowManualPredictionRequest(
    PredictionRequest Request,
    string AttackerCode,
    string TargetCode);
