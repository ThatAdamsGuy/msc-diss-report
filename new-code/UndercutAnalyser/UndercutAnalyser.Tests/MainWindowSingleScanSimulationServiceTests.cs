using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSingleScanSimulationServiceTests
{
    [Fact]
    public void Simulate_ReturnsValidationFailure_WhenAttackerAndTargetMissing()
    {
        var input = CreateInput(attacker: null, target: null, decisionLap: 10, g0: 1.2);

        var result = SingleScanWorkflowService.Simulate(input, _ => CreatePredictionResult());

        Assert.False(result.IsSuccess);
        Assert.Equal("Select attacking and target drivers.", result.StatusMessage);
    }

    [Fact]
    public void Simulate_ReturnsValidationFailure_WhenReferencePaceUnavailable()
    {
        var attacker = new PredictionSelection(4, "NOR", "NOR");
        var target = new PredictionSelection(81, "PIA", "PIA");
        var input = CreateInput(
            attacker: attacker,
            target: target,
            decisionLap: 10,
            g0: 1.2,
            derivedPace: new Dictionary<int, double> { [4] = 0.0, [81] = 90.5 });

        var result = SingleScanWorkflowService.Simulate(input, _ => CreatePredictionResult());

        Assert.False(result.IsSuccess);
        Assert.Equal("Reference pace could not be derived.", result.StatusMessage);
    }

    [Fact]
    public void Simulate_ReturnsSuccessRow_WhenPredictionSucceeds()
    {
        var attacker = new PredictionSelection(4, "NOR", "NOR");
        var target = new PredictionSelection(81, "PIA", "PIA");
        var input = CreateInput(attacker: attacker, target: target, decisionLap: 10, g0: 1.2);

        var result = SingleScanWorkflowService.Simulate(input, _ => CreatePredictionResult());

        Assert.True(result.IsSuccess);
        Assert.Equal("1 scenario simulated.", result.StatusMessage);
        Assert.NotNull(result.Row);
        Assert.Equal("NOR", result.Row!.Attacker);
        Assert.Equal("PIA", result.Row.Target);
    }

    [Fact]
    public void Simulate_ReturnsExecutionFailure_WhenPredictorThrows()
    {
        var attacker = new PredictionSelection(4, "NOR", "NOR");
        var target = new PredictionSelection(81, "PIA", "PIA");
        var input = CreateInput(attacker: attacker, target: target, decisionLap: 10, g0: 1.2);

        var result = SingleScanWorkflowService.Simulate(input, _ => throw new InvalidOperationException("boom"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Simulation failed: boom", result.StatusMessage);
    }

    private static MainWindowSingleScanSimulationInput CreateInput(
        PredictionSelection? attacker,
        PredictionSelection? target,
        int? decisionLap,
        double? g0,
        IReadOnlyDictionary<int, double>? derivedPace = null)
    {
        return new MainWindowSingleScanSimulationInput(
            EventName: "Test Event",
            Attacker: attacker,
            Target: target,
            DecisionLapNumber: decisionLap,
            StartingGapSeconds: g0,
            AttackerPaceOverrideSeconds: null,
            TargetPaceOverrideSeconds: null,
            DerivedReferencePaceByDriver: derivedPace ?? new Dictionary<int, double> { [4] = 90.1, [81] = 90.5 },
            AttackerCompound: "SOFT",
            AttackerTyreAge: 10,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 12,
            AttackerReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 0),
            TargetReplacementTyre: new TyreSetSpecification(TyreCompound.Medium, 0),
            TargetResponseLaps: 1,
            ModelParameters: new LapModelParameters(
                new Dictionary<TyreCompound, double>
                {
                    [TyreCompound.Soft] = 0,
                    [TyreCompound.Medium] = 0.1,
                    [TyreCompound.Hard] = 0.2
                },
                new Dictionary<TyreCompound, double>
                {
                    [TyreCompound.Soft] = 0.10,
                    [TyreCompound.Medium] = 0.07,
                    [TyreCompound.Hard] = 0.04
                },
                new WarmUpModelParameters(0.3),
                22,
                0.25,
                new TrafficModelParameters(false, false, 0.3)));
    }

    private static PredictionResult CreatePredictionResult()
    {
        return new PredictionResult(
            AttackerLaps: [],
            TargetLaps: [],
            InitialGapSeconds: 1.2,
            GapAtTargetPitLapCompleteSeconds: 0.7,
            GapAtTargetOutLapCompleteSeconds: 0.5,
            GapAtBothDriversNormalLapCompleteSeconds: 0.4,
            DeltaGAtTargetPitLapCompleteSeconds: -0.5,
            DeltaGAtTargetOutLapCompleteSeconds: -0.7,
            DeltaGAtBothDriversNormalLapCompleteSeconds: -0.8,
            Classification: UndercutClassification.PredictedAhead,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);
    }
}
