using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services
{
    /// <summary>
    /// Runs both drivers through the full pit sequence and returns the three comparison
    /// gaps and the undercut classification.
    /// </summary>
    public interface IPitSequencePredictor
    {
        PredictionResult Predict(PredictionRequest request);
    }
}
