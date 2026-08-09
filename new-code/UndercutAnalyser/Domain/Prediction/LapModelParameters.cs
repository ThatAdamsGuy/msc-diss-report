using System.Collections.Generic;

namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// All configurable engineering parameters that govern the lap time prediction model.
    ///
    /// Sign convention for compound offsets:
    ///   Soft = 0 s (reference compound — fastest in a single lap).
    ///   Medium and Hard offsets are positive (slower than Soft).
    ///
    /// Degradation rates are per-compound, not per-driver. Both the attacking and target
    /// driver use the same degradation rate for a given compound.
    ///
    /// Fuel correction is deliberately excluded from this model. Both drivers burn fuel
    /// at equal rates over the short comparison window, so the relative effect is negligible.
    /// Fuel correction belongs in the race trace visualisation layer only.
    /// </summary>
    public sealed record LapModelParameters(
        IReadOnlyDictionary<TyreCompound, double> CompoundOffsetsSeconds,
        IReadOnlyDictionary<TyreCompound, double> DegradationRatesSecondsPerLap,
        WarmUpModelParameters WarmUp,
        double PitLaneLossSeconds,
        double MarginalThresholdSeconds,
        TrafficModelParameters Traffic)
    {
        /// <summary>
        /// Returns a sensible default parameter set.
        /// All values are intended as starting-point estimates — the engineer should tune
        /// them to the specific race and circuit before running a prediction.
        /// </summary>
        public static LapModelParameters CreateDefault() => new(
            CompoundOffsetsSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft]   = 0.0,   // reference compound
                [TyreCompound.Medium] = 0.1,
                [TyreCompound.Hard]   = 0.2
            },
            DegradationRatesSecondsPerLap: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft]   = 0.10,
                [TyreCompound.Medium] = 0.07,
                [TyreCompound.Hard]   = 0.04
            },
            WarmUp: WarmUpModelParameters.Default(),
            PitLaneLossSeconds: 22.0,
            MarginalThresholdSeconds: 0.25,
            Traffic: new TrafficModelParameters(
                ApplyToAttacker: false,
                ApplyToTarget: false,
                PenaltySeconds: 0.3));

        /// <summary>
        /// Returns the compound offset for the given compound.
        /// Returns 0.0 if the compound is not in the dictionary (e.g. Unknown).
        /// </summary>
        public double GetCompoundOffset(TyreCompound compound) =>
            CompoundOffsetsSeconds.TryGetValue(compound, out var v) ? v : 0.0;

        /// <summary>
        /// Returns the degradation rate (seconds per lap) for the given compound.
        /// Returns 0.0 if the compound is not in the dictionary.
        /// </summary>
        public double GetDegradationRate(TyreCompound compound) =>
            DegradationRatesSecondsPerLap.TryGetValue(compound, out var v) ? v : 0.0;
    }
}
