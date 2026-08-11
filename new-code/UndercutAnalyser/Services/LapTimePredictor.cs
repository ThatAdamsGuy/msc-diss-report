using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services
{
    /// <summary>
    /// Deterministic implementation of the lap time prediction model.
    ///
    /// L = B + C + D(a) + W(i) + R(i) + P(i)
    ///
    ///   B — driver reference pace (driver-specific, passed in via LapPredictionInput)
    ///   C — tyre compound offset (from LapModelParameters, relative to Soft = 0)
    ///   D(a) — linear degradation: rate × tyreAgeAtStart
    ///   W(i) — warm-up penalty on the out lap only (single flat value)
    ///   R(i) — traffic penalty, applied on the out lap if flagged
    ///   P(i) — full pit lane loss, applied on the pit lap only
    ///
    /// Tyre age convention: TyreAgeAtStart is the age at the beginning of the lap.
    /// Degradation is calculated from this value. The caller increments age after the lap.
    ///
    /// Pit lap: uses old compound/age + full pit loss. The replacement tyre begins on
    /// the following (out) lap at the specified InitialAgeLaps.
    /// </summary>
    public sealed class LapTimePredictor : ILapTimePredictor
    {
        /// <summary>
        /// Calculates one deterministic lap prediction from the supplied lap state and
        /// model parameters, returning a full term-by-term breakdown.
        /// </summary>
        public LapPredictionBreakdown Predict(LapPredictionInput input)
        {
            var p = input.ModelParameters;

            // B — reference pace (driver-specific)
            var referencePace = input.ReferencePaceSeconds;

            // C — compound offset (Soft = 0, Medium/Hard positive)
            var compoundOffset = p.GetCompoundOffset(input.Compound);

            // D(a) — linear degradation: k × a
            var degradationRate = p.GetDegradationRate(input.Compound);
            var degradation = degradationRate * input.TyreAgeAtStart;

            // W(i) — warm-up penalty on the out lap only
            var warmUp = input.IsOutLap ? p.GetWarmUpPenalty(input.Compound) : 0.0;

            // R(i) — traffic penalty on the out lap when flagged
            var traffic = (input.IsOutLap && input.ApplyTrafficPenalty)
                ? p.Traffic.PenaltySeconds
                : 0.0;

            // P(i) — full pit lane loss assigned to the pit lap
            var pitLoss = input.IsPitLap ? p.PitLaneLossSeconds : 0.0;

            return new LapPredictionBreakdown(
                ReferencePaceSeconds: referencePace,
                CompoundOffsetSeconds: compoundOffset,
                DegradationSeconds: degradation,
                WarmUpSeconds: warmUp,
                TrafficSeconds: traffic,
                PitLossSeconds: pitLoss);
        }
    }
}
