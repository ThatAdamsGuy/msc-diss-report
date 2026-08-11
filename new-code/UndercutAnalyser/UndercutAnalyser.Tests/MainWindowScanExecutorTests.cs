using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowScanExecutorTests
{
    [Fact]
    public void Execute_ProducesSuccessRows_WhenPredictorSucceeds()
    {
        var context = CreateContext("NOR", "ANT", decisionLap: 15, g0: 1.2);
        var predictor = new StubPredictor(_ => CreatePredictionResult(UndercutClassification.PredictedAhead));

        var rows = ScanWorkflowService.Execute([context], predictor);

        var row = Assert.Single(rows);
        Assert.Equal("NOR", row.Attacker);
        Assert.Equal("ANT", row.Target);
        Assert.Equal(15, row.DecisionLap);
        Assert.Equal("Ahead", row.Result);
        Assert.Equal("+1.200", row.G0);
    }

    [Fact]
    public void Execute_ProducesErrorRows_WhenPredictorThrows()
    {
        var context = CreateContext("NOR", "ANT", decisionLap: 16, g0: 0.7);
        var predictor = new StubPredictor(_ => throw new InvalidOperationException("predict boom"));

        var rows = ScanWorkflowService.Execute([context], predictor);

        var row = Assert.Single(rows);
        Assert.Equal("Error: predict boom", row.Result);
        Assert.Equal("-", row.GapAtTargetPitLapComplete);
        Assert.Equal("-", row.DeltaGAtTargetPitLapComplete);
    }

    [Fact]
    public void Execute_ProcessesAllContexts_WithoutStoppingOnFailure()
    {
        var c1 = CreateContext("A", "B", decisionLap: 10, g0: 1.0);
        var c2 = CreateContext("C", "D", decisionLap: 11, g0: 1.1);

        var predictor = new StubPredictor(req =>
        {
            if (req.DecisionLap == 10)
                throw new InvalidOperationException("fail one");

            return CreatePredictionResult(UndercutClassification.PredictedMarginal);
        });

        var rows = ScanWorkflowService.Execute([c1, c2], predictor);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Error: fail one", rows[0].Result);
        Assert.Equal("Marginal", rows[1].Result);
    }

    private static ScanExecutionContext CreateContext(string attackerCode, string targetCode, int decisionLap, double g0)
    {
        var request = new PredictionRequest(
            EventName: "Test",
            DecisionLap: decisionLap,
            InitialAttackerGapToTargetSeconds: g0,
            Attacker: new DriverPredictionState(attackerCode, attackerCode, 2, 91.0, TyreCompound.Soft, 10),
            Target: new DriverPredictionState(targetCode, targetCode, 1, 91.2, TyreCompound.Medium, 11),
            AttackerReplacementTyre: new TyreSetSpecification(TyreCompound.Hard, 0),
            TargetReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 2),
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault());

        return new ScanExecutionContext(
            AttackerNumber: 1,
            TargetNumber: 2,
            DecisionLap: decisionLap,
            AttackerPosition: 1,
            InitialGapSeconds: g0,
            AttackerCode: attackerCode,
            TargetCode: targetCode,
            AttackerReferencePace: 91.0,
            TargetReferencePace: 91.2,
            AttackerCompound: "SOFT",
            AttackerTyreAge: 10,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 11,
            Request: request);
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
