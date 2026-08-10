using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictionInputOrchestratorTests
{
    [Fact]
    public void Validate_MissingAttacker_ReturnsExpectedFailure()
    {
        var result = MainWindowPredictionInputOrchestrator.Validate(
            attacker: null,
            target: new MainWindowPredictionSelection(81, "PIA", "PIA"),
            decisionLap: 12,
            initialGapText: "1.234",
            referencePaceByDriver: new Dictionary<int, double> { [81] = 90.5, [4] = 90.1 });

        Assert.False(result.IsValid);
        Assert.Equal("Select an attacking driver.", result.ValidationMessage);
    }

    [Fact]
    public void Validate_SameDriver_ReturnsExpectedFailure()
    {
        var result = MainWindowPredictionInputOrchestrator.Validate(
            attacker: new MainWindowPredictionSelection(4, "NOR", "NOR"),
            target: new MainWindowPredictionSelection(4, "NOR", "NOR"),
            decisionLap: 12,
            initialGapText: "1.234",
            referencePaceByDriver: new Dictionary<int, double> { [4] = 90.1 });

        Assert.False(result.IsValid);
        Assert.Equal("Attacking and target drivers must be different.", result.ValidationMessage);
    }

    [Fact]
    public void Validate_NonNumericGap_ReturnsExpectedFailure()
    {
        var result = MainWindowPredictionInputOrchestrator.Validate(
            attacker: new MainWindowPredictionSelection(4, "NOR", "NOR"),
            target: new MainWindowPredictionSelection(81, "PIA", "PIA"),
            decisionLap: 12,
            initialGapText: "abc",
            referencePaceByDriver: new Dictionary<int, double> { [4] = 90.1, [81] = 90.5 });

        Assert.False(result.IsValid);
        Assert.Equal("Initial gap must be numeric.", result.ValidationMessage);
    }

    [Fact]
    public void Validate_MissingReferencePace_ReturnsExpectedFailure()
    {
        var result = MainWindowPredictionInputOrchestrator.Validate(
            attacker: new MainWindowPredictionSelection(4, "NOR", "NOR"),
            target: new MainWindowPredictionSelection(81, "PIA", "PIA"),
            decisionLap: 12,
            initialGapText: "1.234",
            referencePaceByDriver: new Dictionary<int, double> { [4] = 0.0, [81] = 90.5 });

        Assert.False(result.IsValid);
        Assert.Equal("Reference pace could not be derived. Adjust data filters or input state.", result.ValidationMessage);
    }

    [Fact]
    public void Validate_ValidInput_ReturnsNormalizedSuccess()
    {
        var result = MainWindowPredictionInputOrchestrator.Validate(
            attacker: new MainWindowPredictionSelection(4, "NOR", "NOR"),
            target: new MainWindowPredictionSelection(81, "PIA", "PIA"),
            decisionLap: 15,
            initialGapText: "1.250",
            referencePaceByDriver: new Dictionary<int, double> { [4] = 90.25, [81] = 90.75 });

        Assert.True(result.IsValid);
        Assert.Null(result.ValidationMessage);
        Assert.NotNull(result.Attacker);
        Assert.NotNull(result.Target);
        Assert.Equal(15, result.DecisionLap);
        Assert.Equal(1.25, result.InitialGapSeconds, 10);
        Assert.Equal(90.25, result.AttackerReferencePace, 10);
        Assert.Equal(90.75, result.TargetReferencePace, 10);
    }
}
