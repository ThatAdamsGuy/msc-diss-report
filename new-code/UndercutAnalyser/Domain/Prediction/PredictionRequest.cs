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
    ///   GapAtTargetPitLapCompleteSeconds         — TargetPitLapComplete: end of target pit lap (n + TargetResponseLaps)
    ///   GapAtTargetOutLapCompleteSeconds         — TargetOutLapComplete: end of target out lap (n + TargetResponseLaps + 1)
    ///   GapAtBothDriversNormalLapCompleteSeconds — BothDriversNormalLapComplete: following normal lap for both drivers
    ///
    /// For example when TargetResponseLaps = 2, GapAtTargetPitLapCompleteSeconds is the gap at n+2.
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
