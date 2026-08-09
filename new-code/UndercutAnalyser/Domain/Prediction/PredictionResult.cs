using System.Collections.Generic;

namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// Classification of the undercut outcome based on the primary comparison gap (GapAtTargetPitLapCompleteSeconds).
    /// The marginal zone threshold is configurable (default ±0.25 s) in LapModelParameters.
    /// </summary>
    public enum UndercutClassification
    {
        /// <summary>Attacking driver is predicted to be clearly ahead (GapAtTargetPitLapCompleteSeconds &lt; −threshold).</summary>
        PredictedAhead,

        /// <summary>
        /// The gap is within the marginal threshold in either direction (|GapAtTargetPitLapCompleteSeconds| ≤ threshold).
        /// The predicted gap lies within the configured marginal threshold and is therefore classified as marginal.
        /// </summary>
        PredictedMarginal,

        /// <summary>Attacking driver is predicted to remain behind (GapAtTargetPitLapCompleteSeconds &gt; +threshold).</summary>
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
    ///   GapAtTargetPitLapCompleteSeconds         — PRIMARY: end of target pit lap (n + response delay).
    ///     This is the undercut success point used for classification.
    ///   GapAtTargetOutLapCompleteSeconds         — SECONDARY: end of target out lap.
    ///   GapAtBothDriversNormalLapCompleteSeconds — TERTIARY: following normal lap for both drivers.
    ///
    /// Gap change (ΔG(n) = G(n) - G0 = T_A(n) - T_T(n)):
    ///   DeltaGAtTargetPitLapCompleteSeconds
    ///   DeltaGAtTargetOutLapCompleteSeconds
    ///   DeltaGAtBothDriversNormalLapCompleteSeconds
    ///   Negative = attacking driver gained time on the target driver (regardless of final position).
    ///   Positive = attacking driver lost time to the target driver.
    ///   This is derived purely from the predicted elapsed times — no Stay Out scenario required.
    ///
    /// Classification is derived from GapAtTargetPitLapCompleteSeconds vs MarginalThresholdSeconds.
    /// </summary>
    public sealed record PredictionResult(
        IReadOnlyList<PredictedLap> AttackerLaps,
        IReadOnlyList<PredictedLap> TargetLaps,
        double InitialGapSeconds,
        double GapAtTargetPitLapCompleteSeconds,
        double GapAtTargetOutLapCompleteSeconds,
        double GapAtBothDriversNormalLapCompleteSeconds,
        double DeltaGAtTargetPitLapCompleteSeconds,
        double DeltaGAtTargetOutLapCompleteSeconds,
        double DeltaGAtBothDriversNormalLapCompleteSeconds,
        UndercutClassification Classification,
        double MarginalThresholdSeconds,
        IReadOnlyList<string> Warnings);
}
