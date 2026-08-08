namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// The predicted output for a single lap of a single driver through the pit sequence.
    ///
    /// TyreAgeAtStart is the age of the active tyre at the beginning of this lap.
    /// For the pit lap, this is the age of the old tyre. For the out lap, this is
    /// TyreSetSpecification.InitialAgeLaps (e.g. 0 for a new set).
    ///
    /// CumulativePredictionTimeSeconds is the accumulated predicted lap time from lap n
    /// onwards (the prediction window only, not the full race elapsed time).
    /// </summary>
    public sealed record PredictedLap(
        string DriverCode,
        int LapNumber,
        bool IsPitLap,
        bool IsOutLap,
        TyreCompound Compound,
        int TyreAgeAtStart,
        LapPredictionBreakdown Breakdown,
        double CumulativePredictionTimeSeconds);
}
