using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictionRunServiceTests
{
    [Fact]
    public void Run_WhenValidationFails_ReturnsValidationFailure()
    {
        var input = CreateInput(initialGapText: "not-a-number");

        var result = MainWindowPredictionRunService.Run(input);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsValidationFailure);
        Assert.Equal("Initial gap must be numeric.", result.ErrorMessage);
        Assert.Null(result.Prediction);
    }

    [Fact]
    public void Run_WhenPredictSucceeds_ReturnsSuccessWithCodes()
    {
        PredictionRequest? capturedRequest = null;
        var input = CreateInput();

        var result = MainWindowPredictionRunService.Run(input, request =>
        {
            capturedRequest = request;
            return CreatePredictionResult();
        });

        Assert.True(result.IsSuccess);
        Assert.False(result.IsValidationFailure);
        Assert.NotNull(result.Prediction);
        Assert.Equal("NOR", result.AttackerCode);
        Assert.Equal("PIA", result.TargetCode);

        Assert.NotNull(capturedRequest);
        Assert.Equal("Test Event", capturedRequest!.EventName);
        Assert.Equal(14, capturedRequest.DecisionLap);
        Assert.Equal(1.23, capturedRequest.InitialAttackerGapToTargetSeconds, 10);
    }

    [Fact]
    public void Run_WhenPredictThrows_ReturnsExecutionFailure()
    {
        var input = CreateInput();

        var result = MainWindowPredictionRunService.Run(input, _ => throw new InvalidOperationException("boom"));

        Assert.False(result.IsSuccess);
        Assert.False(result.IsValidationFailure);
        Assert.Equal("Prediction failed: boom", result.ErrorMessage);
        Assert.Null(result.Prediction);
    }

    private static MainWindowPredictionRunInput CreateInput(string? initialGapText = "1.23")
    {
        var attacker = new MainWindowPredictionSelection(4, "NOR", "Lando Norris");
        var target = new MainWindowPredictionSelection(81, "PIA", "Oscar Piastri");

        return new MainWindowPredictionRunInput(
            EventName: "Test Event",
            Attacker: attacker,
            Target: target,
            DecisionLapNumber: 14,
            InitialGapText: initialGapText,
            ReferencePaceByDriver: new Dictionary<int, double>
            {
                [4] = 90.4,
                [81] = 90.8
            },
            ResolveTyreState: (_, _) => ("SOFT", 6),
            AttackerReplacementCompoundText: "HARD",
            TargetReplacementCompoundText: "MEDIUM",
            AttackerReplacementAgeText: "1",
            TargetReplacementAgeText: "2",
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault());
    }

    private static PredictionResult CreatePredictionResult()
    {
        return new PredictionResult(
            AttackerLaps: [],
            TargetLaps: [],
            InitialGapSeconds: 1.23,
            GapAtTargetPitLapCompleteSeconds: 0.8,
            GapAtTargetOutLapCompleteSeconds: 0.6,
            GapAtBothDriversNormalLapCompleteSeconds: 0.5,
            DeltaGAtTargetPitLapCompleteSeconds: -0.43,
            DeltaGAtTargetOutLapCompleteSeconds: -0.63,
            DeltaGAtBothDriversNormalLapCompleteSeconds: -0.73,
            Classification: UndercutClassification.PredictedAhead,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);
    }
}
