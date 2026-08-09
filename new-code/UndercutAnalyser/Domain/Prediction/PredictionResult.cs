using System.Collections.Generic;

namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// Classification of the undercut outcome based on the primary comparison gap (GapAtN1Seconds).
    /// The marginal zone threshold is configurable (default ±0.25 s) in LapModelParameters.
    /// </summary>
    public enum UndercutClassification
    {
        /// <summary>Attacker is predicted to be clearly ahead (GapAtN1 &lt; −threshold).</summary>
        PredictedAhead,

        /// <summary>
        /// The gap is within the marginal threshold in either direction (|GapAtN1| ≤ threshold).
        /// The outcome is too close to call with confidence.
        /// </summary>
        PredictedMarginal,

        /// <summary>Attacker is predicted to remain behind (GapAtN1 &gt; +threshold).</summary>
        PredictedBehind
    }

    /// <summary>
    /// The full output of a single undercut prediction run.
    ///
    /// Sign convention (consistent throughout the engine):
    ///   Negative gap = attacking driver is ahead.
    ///   Positive gap = attacking driver is behind the target.
    ///
    /// Absolute predicted gaps (G(n) = G0 + T_A(n) - T_T(n)):
    ///   GapAtN1Seconds — PRIMARY: end of target pit lap (n + response delay).
    ///     This is the undercut success point used for classification.
    ///   GapAtN2Seconds — SECONDARY: end of target out lap.
    ///   GapAtN3Seconds — TERTIARY: following normal lap for both drivers.
    ///
    /// Gap change (ΔG(n) = G(n) - G0 = T_A(n) - T_T(n)):
    ///   DeltaGAtN1Seconds / DeltaGAtN2Seconds / DeltaGAtN3Seconds
    ///   Negative = attacker gained time on target (regardless of whether they ended up ahead).
    ///   Positive = attacker lost time to target.
    ///   This is derived purely from the predicted elapsed times — no Stay Out scenario required.
    ///
    /// Classification is derived from GapAtN1Seconds vs MarginalThresholdSeconds.
    /// </summary>
    public sealed record PredictionResult(
        IReadOnlyList<PredictedLap> AttackerLaps,
        IReadOnlyList<PredictedLap> TargetLaps,
        double InitialGapSeconds,
        double GapAtN1Seconds,
        double GapAtN2Seconds,
        double GapAtN3Seconds,
        double DeltaGAtN1Seconds,
        double DeltaGAtN2Seconds,
        double DeltaGAtN3Seconds,
        UndercutClassification Classification,
        double MarginalThresholdSeconds,
        IReadOnlyList<string> Warnings);
}
