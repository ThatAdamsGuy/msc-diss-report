namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// Models the tyre warm-up penalty applied to the first lap after a pit stop (the out lap).
    /// This is a configurable flat penalty that does not decay — subsequent laps are unaffected.
    /// Non-linear warm-up profiles are a future extension.
    /// </summary>
    public sealed record WarmUpModelParameters(double OutLapPenaltySeconds)
    {
        /// <summary>
        /// Returns a default warm-up model with a 0.3 s out-lap penalty.
        /// This is a reasonable starting estimate; the correct value is track and compound dependent.
        /// </summary>
        public static WarmUpModelParameters Default() => new(OutLapPenaltySeconds: 0.3);

        /// <summary>
        /// Returns a model with no warm-up penalty (useful for testing or circuits with rapid warm-up).
        /// </summary>
        public static WarmUpModelParameters None() => new(OutLapPenaltySeconds: 0.0);
    }
}
