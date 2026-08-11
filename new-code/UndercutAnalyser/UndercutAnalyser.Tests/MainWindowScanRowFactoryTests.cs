using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowScanRowFactoryTests
{
    [Fact]
    public void CreateSuccess_FormatsAllFields_AndUsesClassificationLabel()
    {
        var result = new PredictionResult(
            AttackerLaps: [],
            TargetLaps: [],
            InitialGapSeconds: 1.5,
            GapAtTargetPitLapCompleteSeconds: 0.75,
            GapAtTargetOutLapCompleteSeconds: -0.10,
            GapAtBothDriversNormalLapCompleteSeconds: -0.25,
            DeltaGAtTargetPitLapCompleteSeconds: -0.75,
            DeltaGAtTargetOutLapCompleteSeconds: -1.60,
            DeltaGAtBothDriversNormalLapCompleteSeconds: -1.75,
            Classification: UndercutClassification.PredictedMarginal,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);

        var row = ScanWorkflowService.CreateSuccess(
            attacker: "NOR",
            target: "ANT",
            decisionLap: 12,
            attackerCompound: "SOFT",
            attackerTyreAge: 8,
            targetCompound: "MEDIUM",
            targetTyreAge: 10,
            g0: 1.234,
            prediction: result);

        Assert.Equal("NOR", row.Attacker);
        Assert.Equal("ANT", row.Target);
        Assert.Equal(12, row.DecisionLap);
        Assert.Equal("SOFT", row.AttackerCompound);
        Assert.Equal(8, row.AttackerTyreAge);
        Assert.Equal("MEDIUM", row.TargetCompound);
        Assert.Equal(10, row.TargetTyreAge);
        Assert.Equal("+1.234", row.G0);
        Assert.Equal("+0.750 s", row.GapAtTargetPitLapComplete);
        Assert.Equal("-0.100 s", row.GapAtTargetOutLapComplete);
        Assert.Equal("-0.250 s", row.GapAtBothDriversNormalLapComplete);
        Assert.Equal("-0.750", row.DeltaGAtTargetPitLapComplete);
        Assert.Equal("Marginal", row.Result);
    }

    [Fact]
    public void CreateError_SetsPlaceholderGapFields_AndErrorResult()
    {
        var row = ScanWorkflowService.CreateError(
            attacker: "NOR",
            target: "ANT",
            decisionLap: 15,
            attackerCompound: "SOFT",
            attackerTyreAge: 9,
            targetCompound: "HARD",
            targetTyreAge: 12,
            g0: 0.456,
            errorMessage: "boom");

        Assert.Equal("+0.456", row.G0);
        Assert.Equal("-", row.GapAtTargetPitLapComplete);
        Assert.Equal("-", row.GapAtTargetOutLapComplete);
        Assert.Equal("-", row.GapAtBothDriversNormalLapComplete);
        Assert.Equal("-", row.DeltaGAtTargetPitLapComplete);
        Assert.Equal("Error: boom", row.Result);
    }
}
