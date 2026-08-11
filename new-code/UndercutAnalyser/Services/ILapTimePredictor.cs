using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services
{
    /// <summary>
    /// Input context for a single lap prediction. All state needed to evaluate
    /// L = B + C + D(a) + W(i) + R(i) + P(i) for one driver on one lap.
    /// </summary>
    public sealed record LapPredictionInput(
        string DriverCode,
        double ReferencePaceSeconds,
        TyreCompound Compound,
        int TyreAgeAtStart,
        bool IsPitLap,
        bool IsOutLap,
        bool ApplyTrafficPenalty,
        LapModelParameters ModelParameters);

    /// <summary>
    /// Predicts the time breakdown for a single driver lap.
    /// Contains no race sequence logic - only the lap time equation.
    /// </summary>
    public interface ILapTimePredictor
    {
        LapPredictionBreakdown Predict(LapPredictionInput input);
    }
}
