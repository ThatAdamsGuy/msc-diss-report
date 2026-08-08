namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// The full term-by-term breakdown of a single predicted lap time.
    /// L = B + C + D(a) + W(i) + R(i) + P(i)
    ///
    /// Every term is stored separately so the prediction is fully traceable. The engineer
    /// can inspect which effect dominates and assess whether the assumptions are appropriate.
    ///
    /// All values are in seconds. Positive values add time (slower). Negative values would
    /// subtract time (not expected in the current model, but not rejected).
    /// </summary>
    public sealed record LapPredictionBreakdown(
        double ReferencePaceSeconds,
        double CompoundOffsetSeconds,
        double DegradationSeconds,
        double WarmUpSeconds,
        double TrafficSeconds,
        double PitLossSeconds)
    {
        /// <summary>
        /// Sum of all six terms. This is the complete predicted lap time (seconds).
        /// </summary>
        public double TotalSeconds =>
            ReferencePaceSeconds +
            CompoundOffsetSeconds +
            DegradationSeconds +
            WarmUpSeconds +
            TrafficSeconds +
            PitLossSeconds;
    }
}
