using System.Collections.Generic;

namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// Classification of the undercut outcome based on the primary comparison gap (GapAfterTargetPitSeconds).
    /// The marginal zone threshold is configurable (default ±0.25 s) in LapModelParameters.
    /// </summary>
    public enum UndercutClassification
    {
        /// <summary>Attacking driver is predicted to be clearly ahead (GapAfterTargetPitSeconds &lt; −threshold).</summary>
        PredictedAhead,

        /// <summary>
        /// The gap is within the marginal threshold in either direction (|GapAfterTargetPitSeconds| ≤ threshold).
        /// The predicted gap lies within the configured marginal threshold and is therefore classified as marginal.
        /// </summary>
        PredictedMarginal,

        /// <summary>Attacking driver is predicted to remain behind (GapAfterTargetPitSeconds &gt; +threshold).</summary>
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
    ///   GapAfterTargetPitSeconds — PRIMARY: end of target pit lap (n + response delay).
    ///     This is the undercut success point used for classification.
    ///   GapAtN2Seconds — SECONDARY: end of target out lap.
    ///   GapAtN3Seconds — TERTIARY: following normal lap for both drivers.
    ///
    /// Naming note: N2/N3 are logical endpoint labels, not fixed +2/+3
    /// offsets from decision lap n when response delay > 1.
    ///
    /// Gap change (ΔG(n) = G(n) - G0 = T_A(n) - T_T(n)):
    ///   DeltaGAtN1Seconds / DeltaGAtN2Seconds / DeltaGAtN3Seconds
    ///   Negative = attacking driver gained time on the target driver (regardless of final position).
    ///   Positive = attacking driver lost time to the target driver.
    ///   This is derived purely from the predicted elapsed times — no Stay Out scenario required.
    ///
    /// Classification is derived from GapAfterTargetPitSeconds vs MarginalThresholdSeconds.
    /// </summary>
    public sealed record PredictionResult(
        IReadOnlyList<PredictedLap> AttackerLaps,
        IReadOnlyList<PredictedLap> TargetLaps,
        double InitialGapSeconds,
        double GapAfterTargetPitSeconds,
        double GapAtN2Seconds,
        double GapAtN3Seconds,
        double DeltaGAtN1Seconds,
        double DeltaGAtN2Seconds,
        double DeltaGAtN3Seconds,
        UndercutClassification Classification,
        double MarginalThresholdSeconds,
        IReadOnlyList<string> Warnings);
}
