namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// A fully specified request to run the undercut prediction engine.
    ///
    /// InitialAttackerGapToTargetSeconds is the observed gap at the decision point, signed:
    ///   Negative = attacker is currently ahead (unusual for an undercut attempt).
    ///   Positive = attacker is currently behind the target (normal case).
    ///
    /// TargetResponseLaps is the number of laps between the attacker's pit stop (lap n)
    /// and the target's pit stop (lap n + TargetResponseLaps). Must be >= 1.
    /// The conventional undercut case is TargetResponseLaps = 1.
    ///
    /// The prediction produces three comparison gaps:
    ///   GapAtN1 — end of attacker's out lap, target just pitted (PRIMARY classification point)
    ///   GapAtN2 — end of target's out lap
    ///   GapAtN3 — both drivers on their first normal lap after the stop
    /// </summary>
    public sealed record PredictionRequest(
        string EventName,
        int DecisionLap,
        double InitialAttackerGapToTargetSeconds,
        DriverPredictionState Attacker,
        DriverPredictionState Target,
        TyreSetSpecification AttackerReplacementTyre,
        TyreSetSpecification TargetReplacementTyre,
        int TargetResponseLaps,
        LapModelParameters ModelParameters);
}
