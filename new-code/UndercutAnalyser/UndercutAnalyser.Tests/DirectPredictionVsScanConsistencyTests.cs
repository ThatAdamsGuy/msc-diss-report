using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class DirectPredictionVsScanConsistencyTests
{
    [Fact]
    public void EquivalentDirectAndScanRequests_ProduceNumericallyIdenticalPredictionOutputs()
    {
        var model = new LapModelParameters(
            CompoundOffsetsSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.0,
                [TyreCompound.Medium] = 0.3,
                [TyreCompound.Hard] = 0.8
            },
            DegradationRatesSecondsPerLap: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.10,
                [TyreCompound.Medium] = 0.08,
                [TyreCompound.Hard] = 0.05
            },
            WarmUpPenaltiesSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 1.1,
                [TyreCompound.Medium] = 1.2,
                [TyreCompound.Hard] = 1.3
            },
            PitLaneLossSeconds: 21.5,
            MarginalThresholdSeconds: 0.25,
            Traffic: new TrafficModelParameters(false, false, 0.4));

        var directRequest = WorkspaceWorkflowService.CreatePredictionRequest(
            eventName: "Parity Test Event",
            decisionLap: 30,
            initialGapSeconds: 1.2,
            attacker: new DriverScenarioInput("NOR", "NOR", 2, 90.2, "MEDIUM", 11),
            target: new DriverScenarioInput("PIA", "PIA", 1, 90.7, "HARD", 13),
            attackerReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 1),
            targetReplacementTyre: new TyreSetSpecification(TyreCompound.Medium, 2),
            targetResponseLaps: 2,
            modelParameters: model);

        var drivers = new Dictionary<int, Driver>
        {
            [4] = new() { DriverNumber = 4, Code = "NOR" },
            [81] = new() { DriverNumber = 81, Code = "PIA" }
        };

        var paces = new Dictionary<int, double>
        {
            [4] = 90.2,
            [81] = 90.7
        };

        var candidate = new ScanCandidate(
            AttackerNumber: 4,
            TargetNumber: 81,
            DecisionLap: 30,
            AttackerPosition: 1,
            InitialGapSeconds: 1.2);

        (string compound, int age) TyreResolver(int driver, int lap) =>
            driver == 4 ? ("MEDIUM", 11) : ("HARD", 13);

        var context = ScanWorkflowService.BuildExecutionContext(
            candidate: candidate,
            driversByNumber: drivers,
            referencePaceByDriver: paces,
            tyreStateResolver: TyreResolver,
            eventName: "Parity Test Event",
            attackerReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 1),
            targetReplacementTyre: new TyreSetSpecification(TyreCompound.Medium, 2),
            targetResponseLaps: 2,
            modelParameters: model);

        var predictor = new PitSequencePredictor(new LapTimePredictor());

        var direct = predictor.Predict(directRequest);
        var fromScan = predictor.Predict(context.Request);

        Assert.Equal(direct.InitialGapSeconds, fromScan.InitialGapSeconds, 10);
        Assert.Equal(direct.GapAtTargetPitLapCompleteSeconds, fromScan.GapAtTargetPitLapCompleteSeconds, 10);
        Assert.Equal(direct.GapAtTargetOutLapCompleteSeconds, fromScan.GapAtTargetOutLapCompleteSeconds, 10);
        Assert.Equal(direct.GapAtBothDriversNormalLapCompleteSeconds, fromScan.GapAtBothDriversNormalLapCompleteSeconds, 10);
        Assert.Equal(direct.DeltaGAtTargetPitLapCompleteSeconds, fromScan.DeltaGAtTargetPitLapCompleteSeconds, 10);
        Assert.Equal(direct.DeltaGAtTargetOutLapCompleteSeconds, fromScan.DeltaGAtTargetOutLapCompleteSeconds, 10);
        Assert.Equal(direct.DeltaGAtBothDriversNormalLapCompleteSeconds, fromScan.DeltaGAtBothDriversNormalLapCompleteSeconds, 10);
        Assert.Equal(direct.Classification, fromScan.Classification);
        Assert.Equal(direct.MarginalThresholdSeconds, fromScan.MarginalThresholdSeconds, 10);

        Assert.Equal(direct.AttackerLaps.Count, fromScan.AttackerLaps.Count);
        Assert.Equal(direct.TargetLaps.Count, fromScan.TargetLaps.Count);

        for (var i = 0; i < direct.AttackerLaps.Count; i++)
            Assert.Equal(direct.AttackerLaps[i], fromScan.AttackerLaps[i]);

        for (var i = 0; i < direct.TargetLaps.Count; i++)
            Assert.Equal(direct.TargetLaps[i], fromScan.TargetLaps[i]);
    }
}
