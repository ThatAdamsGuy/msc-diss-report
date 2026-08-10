using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// UI-facing driver scenario input used to construct model-ready prediction requests.
/// </summary>
public sealed record DriverScenarioInput(                                   
    string Code,
    string DisplayName,
    int Position,
    double ReferencePaceSeconds,
    string CurrentCompoundText,
    int CurrentTyreAgeLaps);

public static partial class WorkspaceWorkflowService
{
    /// <summary>
    /// Builds a full prediction request from UI scan/predict inputs, including compound parsing and driver-state mapping.
    /// </summary>
    public static PredictionRequest CreatePredictionRequest(
        string eventName,                                                                                                                  
        int decisionLap,
        double initialGapSeconds,
        DriverScenarioInput attacker,
        DriverScenarioInput target,
        TyreSetSpecification attackerReplacementTyre,
        TyreSetSpecification targetReplacementTyre,
        int targetResponseLaps,
        LapModelParameters modelParameters)
    {
        return new PredictionRequest(
            EventName: eventName,
            DecisionLap: decisionLap,
            InitialAttackerGapToTargetSeconds: initialGapSeconds,
            Attacker: new DriverPredictionState(
                attacker.Code,
                attacker.DisplayName,
                attacker.Position,
                attacker.ReferencePaceSeconds,
                TyreCompoundParser.FromOpenF1String(attacker.CurrentCompoundText),
                attacker.CurrentTyreAgeLaps),
            Target: new DriverPredictionState(
                target.Code,
                target.DisplayName,
                target.Position,
                target.ReferencePaceSeconds,
                TyreCompoundParser.FromOpenF1String(target.CurrentCompoundText),
                target.CurrentTyreAgeLaps),
            AttackerReplacementTyre: attackerReplacementTyre,
            TargetReplacementTyre: targetReplacementTyre,
            TargetResponseLaps: targetResponseLaps,
            ModelParameters: modelParameters);
    }

    /// <summary>
    /// Converts model classification enum values into table-friendly labels (Ahead, Marginal, Behind).
    /// </summary>
    public static string ToResultLabel(UndercutClassification classification)
    {
        return classification switch
        {
            UndercutClassification.PredictedAhead => "Ahead",
            UndercutClassification.PredictedMarginal => "Marginal",
            _ => "Behind"
        };
    }
}
