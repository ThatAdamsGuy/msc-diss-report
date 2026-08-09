namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// A fully specified request to run the undercut prediction engine.
    ///
    /// InitialAttackerGapToTargetSeconds is the observed gap at the decision lap
    /// (measured at Sector Line Two), signed:
    ///   Negative = attacking driver is currently ahead (unusual for an undercut attempt).
    ///   Positive = attacking driver is currently behind the target driver (normal case).
    ///
    /// TargetResponseLaps is the number of laps between the attacking driver's pit stop (lap n)
    /// and the target driver's pit stop (lap n + TargetResponseLaps). Must be >= 1.
    /// The conventional undercut case is TargetResponseLaps = 1.
    ///
    /// The prediction produces three comparison gaps:
    ///   GapAfterTargetPitSeconds — PRIMARY: end of target pit lap (n + TargetResponseLaps)
    ///   GapAtN2Seconds           — SECONDARY: end of target out lap (n + TargetResponseLaps + 1)
    ///   GapAtN3Seconds           — TERTIARY: following normal lap for both drivers
    ///
    /// Note: N2/N3 are endpoint labels, not literal lap offsets from n.
    /// For example when TargetResponseLaps = 2, GapAfterTargetPitSeconds is the gap at n+2.
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
