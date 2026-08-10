using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSingleScanRunServiceTests
{
    [Fact]
    public void Run_WhenDriversMissing_ReturnsValidationFailureFromSimulation()
    {
        var input = CreateInput(attacker: null, target: null);

        var result = SingleScanWorkflowService.Run(input);

        Assert.False(result.IsSuccess);
        Assert.Equal("Select attacking and target drivers.", result.StatusMessage);
        Assert.Null(result.Row);
    }

    [Fact]
    public void Run_WithValidInput_ParsesTextFieldsAndReturnsSuccessRow()
    {
        var attacker = new PredictionSelection(4, "NOR", "Lando Norris");
        var target = new PredictionSelection(81, "PIA", "Oscar Piastri");
        PredictionRequest? capturedRequest = null;

        var input = CreateInput(
            attacker: attacker,
            target: target,
            startingGapText: "1.250",
            attackerOverrideText: "90.200",
            targetOverrideText: "90.500",
            attackerReplAgeText: "2",
            targetReplAgeText: "3");

        var result = SingleScanWorkflowService.Run(input, request =>
        {
            capturedRequest = request;
            return CreatePredictionResult();
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Row);
        Assert.Equal("NOR", result.Row!.Attacker);
        Assert.Equal("PIA", result.Row.Target);

        Assert.NotNull(capturedRequest);
        Assert.Equal(1.25, capturedRequest!.InitialAttackerGapToTargetSeconds, 10);
        Assert.Equal(90.2, capturedRequest.Attacker.ReferencePaceSeconds, 10);
        Assert.Equal(90.5, capturedRequest.Target.ReferencePaceSeconds, 10);
        Assert.Equal(2, capturedRequest.AttackerReplacementTyre.InitialAgeLaps);
        Assert.Equal(3, capturedRequest.TargetReplacementTyre.InitialAgeLaps);
    }

    private static MainWindowSingleScanRunInput CreateInput(
        PredictionSelection? attacker,
        PredictionSelection? target,
        string? startingGapText = "1.100",
        string? attackerOverrideText = null,
        string? targetOverrideText = null,
        string? attackerReplAgeText = "0",
        string? targetReplAgeText = "0")
    {
        return new MainWindowSingleScanRunInput(
            EventName: "Test Event",
            Attacker: attacker,
            Target: target,
            DecisionLapNumber: 10,
            StartingGapText: startingGapText,
            AttackerPaceOverrideText: attackerOverrideText,
            TargetPaceOverrideText: targetOverrideText,
            DerivedReferencePaceByDriver: new Dictionary<int, double>
            {
                [4] = 90.4,
                [81] = 90.8
            },
            AttackerCompound: "SOFT",
            AttackerTyreAge: 8,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 12,
            AttackerReplacementCompoundText: "HARD",
            AttackerReplacementAgeText: attackerReplAgeText,
            TargetReplacementCompoundText: "SOFT",
            TargetReplacementAgeText: targetReplAgeText,
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault());
    }

    private static PredictionResult CreatePredictionResult()
    {
        return new PredictionResult(
            AttackerLaps: [],
            TargetLaps: [],
            InitialGapSeconds: 1.1,
            GapAtTargetPitLapCompleteSeconds: 0.7,
            GapAtTargetOutLapCompleteSeconds: 0.5,
            GapAtBothDriversNormalLapCompleteSeconds: 0.4,
            DeltaGAtTargetPitLapCompleteSeconds: -0.4,
            DeltaGAtTargetOutLapCompleteSeconds: -0.6,
            DeltaGAtBothDriversNormalLapCompleteSeconds: -0.7,
            Classification: UndercutClassification.PredictedAhead,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);
    }
}
