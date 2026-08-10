using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictionExecutionServiceTests
{
    [Fact]
    public void Execute_WhenPredictorSucceeds_ReturnsSuccessResult()
    {
        var request = CreateRequest();
        var expected = CreatePredictionResult(UndercutClassification.PredictedAhead);
        var predictor = new StubPredictor(_ => expected);

        var result = MainWindowPredictionExecutionService.Execute(request, predictor);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Result);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Execute_WhenPredictorThrows_ReturnsFailureWithFormattedMessage()
    {
        var request = CreateRequest();
        var predictor = new StubPredictor(_ => throw new InvalidOperationException("boom"));

        var result = MainWindowPredictionExecutionService.Execute(request, predictor);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Result);
        Assert.Equal("Prediction failed: boom", result.ErrorMessage);
    }

    private static PredictionRequest CreateRequest()
    {
        return new PredictionRequest(
            EventName: "Test",
            DecisionLap: 12,
            InitialAttackerGapToTargetSeconds: 1.1,
            Attacker: new DriverPredictionState("NOR", "NOR", 2, 90.1, TyreCompound.Soft, 6),
            Target: new DriverPredictionState("PIA", "PIA", 1, 90.5, TyreCompound.Medium, 8),
            AttackerReplacementTyre: new TyreSetSpecification(TyreCompound.Hard, 0),
            TargetReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 2),
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault());
    }

    private static PredictionResult CreatePredictionResult(UndercutClassification classification)
    {
        return new PredictionResult(
            AttackerLaps: [],
            TargetLaps: [],
            InitialGapSeconds: 1.0,
            GapAtTargetPitLapCompleteSeconds: 0.5,
            GapAtTargetOutLapCompleteSeconds: 0.4,
            GapAtBothDriversNormalLapCompleteSeconds: 0.3,
            DeltaGAtTargetPitLapCompleteSeconds: -0.5,
            DeltaGAtTargetOutLapCompleteSeconds: -0.6,
            DeltaGAtBothDriversNormalLapCompleteSeconds: -0.7,
            Classification: classification,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);
    }

    private sealed class StubPredictor : IPitSequencePredictor
    {
        private readonly Func<PredictionRequest, PredictionResult> _predict;

        public StubPredictor(Func<PredictionRequest, PredictionResult> predict)
        {
            _predict = predict;
        }

        public PredictionResult Predict(PredictionRequest request) => _predict(request);
    }
}
