namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// Models the flat traffic penalty R applied to affected drivers during the pit sequence.
    /// Traffic is an external race effect — it is not predicted, only applied when the
    /// analysing engineer decides it is likely. The penalty is added to the predicted lap
    /// time of the flagged driver on their out lap.
    /// </summary>
    public sealed record TrafficModelParameters(
        bool ApplyToAttacker,
        bool ApplyToTarget,
        double PenaltySeconds)
    {
        /// <summary>No traffic assumed for either driver.</summary>
        public static TrafficModelParameters None() =>
            new(ApplyToAttacker: false, ApplyToTarget: false, PenaltySeconds: 0.0);

        /// <summary>
        /// Returns a model with a flat traffic penalty applied to the attacking driver's out lap.
        /// </summary>
        public static TrafficModelParameters AttackerOnly(double penaltySeconds) =>
            new(ApplyToAttacker: true, ApplyToTarget: false, PenaltySeconds: penaltySeconds);

        /// <summary>
        /// Returns a model with the penalty applied to the target driver's out lap.
        /// </summary>
        public static TrafficModelParameters TargetOnly(double penaltySeconds) =>
            new(ApplyToAttacker: false, ApplyToTarget: true, PenaltySeconds: penaltySeconds);
    }
}
