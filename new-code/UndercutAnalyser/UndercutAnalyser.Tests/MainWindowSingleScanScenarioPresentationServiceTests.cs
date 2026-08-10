using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSingleScanScenarioPresentationServiceTests
{
    [Fact]
    public void Build_WhenNoScenario_ReturnsEmptyPresentation()
    {
        var presentation = SingleScanWorkflowService.Build(
            scenario: MainWindowSingleScanScenarioResult.Empty,
            selectedTargetDriverNumber: null);

        Assert.False(presentation.HasScenario);
        Assert.False(presentation.ShouldAutoSelectTarget);
        Assert.Null(presentation.TargetDriverToSelect);
        Assert.Equal(string.Empty, presentation.AttackerPaceText);
        Assert.Equal(string.Empty, presentation.TargetPaceText);
        Assert.Equal(string.Empty, presentation.AttackerCompoundText);
        Assert.Equal(string.Empty, presentation.TargetCompoundText);
        Assert.Equal(string.Empty, presentation.StartingGapText);
    }

    [Fact]
    public void Build_WhenTargetDiffers_RequestsAutoSelectAndFormatsFields()
    {
        var scenario = new MainWindowSingleScanScenarioResult(
            HasScenario: true,
            SuggestedAheadDriverNumber: 81,
            EffectiveTargetDriverNumber: 81,
            DecisionLapNumber: 14,
            AttackerReferencePace: 90.321,
            TargetReferencePace: 90.0,
            AttackerCompound: "SOFT",
            AttackerTyreAge: 8,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 12,
            StartingGapSeconds: 1.23456);

        var presentation = SingleScanWorkflowService.Build(
            scenario,
            selectedTargetDriverNumber: 4);

        Assert.True(presentation.HasScenario);
        Assert.True(presentation.ShouldAutoSelectTarget);
        Assert.Equal(81, presentation.TargetDriverToSelect);
        Assert.Equal("90.321", presentation.AttackerPaceText);
        Assert.Equal("90.000", presentation.TargetPaceText);
        Assert.Equal("SOFT", presentation.AttackerCompoundText);
        Assert.Equal("8", presentation.AttackerTyreAgeText);
        Assert.Equal("MEDIUM", presentation.TargetCompoundText);
        Assert.Equal("12", presentation.TargetTyreAgeText);
        Assert.Equal("+1.235", presentation.StartingGapText);
    }

    [Fact]
    public void Build_WhenTargetAlreadySelected_DoesNotRequestAutoSelect()
    {
        var scenario = new MainWindowSingleScanScenarioResult(
            HasScenario: true,
            SuggestedAheadDriverNumber: 81,
            EffectiveTargetDriverNumber: 81,
            DecisionLapNumber: 20,
            AttackerReferencePace: 0,
            TargetReferencePace: 0,
            AttackerCompound: "HARD",
            AttackerTyreAge: 5,
            TargetCompound: "SOFT",
            TargetTyreAge: 3,
            StartingGapSeconds: null);

        var presentation = SingleScanWorkflowService.Build(
            scenario,
            selectedTargetDriverNumber: 81);

        Assert.True(presentation.HasScenario);
        Assert.False(presentation.ShouldAutoSelectTarget);
        Assert.Equal("", presentation.AttackerPaceText);
        Assert.Equal("", presentation.TargetPaceText);
        Assert.Equal("", presentation.StartingGapText);
    }
}
