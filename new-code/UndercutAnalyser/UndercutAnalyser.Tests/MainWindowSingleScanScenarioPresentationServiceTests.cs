using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSingleScanScenarioPresentationServiceTests
{
    #region Core projection and formatting behaviour
    [Fact]
    public void Build_WhenNoScenario_ReturnsEmptyPresentation()
    {
        var presentation = SingleScanWorkflowService.Build(
            scenario: MainWindowSingleScanScenarioResult.Empty,
            selectedTargetDriverNumber: null,
            forceAutoSelectTarget: false);

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
            selectedTargetDriverNumber: 4,
            forceAutoSelectTarget: false);

        Assert.True(presentation.HasScenario);
        // User has already selected a target (4), so auto-select should NOT occur
        Assert.False(presentation.ShouldAutoSelectTarget);
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
    public void Build_WhenNoTargetSelected_RequestsAutoSelectWhenEffectiveTargetAvailable()
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
            selectedTargetDriverNumber: null,
            forceAutoSelectTarget: false);

        Assert.True(presentation.HasScenario);
        // No user selection yet, so auto-select should occur
        Assert.True(presentation.ShouldAutoSelectTarget);
        Assert.Equal(81, presentation.TargetDriverToSelect);
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
            selectedTargetDriverNumber: 81,
            forceAutoSelectTarget: false);

        Assert.True(presentation.HasScenario);
        Assert.False(presentation.ShouldAutoSelectTarget);
        Assert.Equal("", presentation.AttackerPaceText);
        Assert.Equal("", presentation.TargetPaceText);
        Assert.Equal("", presentation.StartingGapText);
    }

    #endregion

    #region Auto-select policy regressions

    [Fact]
    public void Build_WhenForceAutoSelectTargetEnabled_RequestsAutoSelectEvenWhenTargetAlreadySelected()
    {
        var scenario = new MainWindowSingleScanScenarioResult(
            HasScenario: true,
            SuggestedAheadDriverNumber: 63,
            EffectiveTargetDriverNumber: 63,
            DecisionLapNumber: 33,
            AttackerReferencePace: 90.1,
            TargetReferencePace: 90.2,
            AttackerCompound: "MEDIUM",
            AttackerTyreAge: 10,
            TargetCompound: "HARD",
            TargetTyreAge: 8,
            StartingGapSeconds: 0.456);

        var presentation = SingleScanWorkflowService.Build(
            scenario,
            selectedTargetDriverNumber: 44,
            forceAutoSelectTarget: true);

        Assert.True(presentation.HasScenario);
        Assert.True(presentation.ShouldAutoSelectTarget);
        Assert.Equal(63, presentation.TargetDriverToSelect);
        Assert.Equal("+0.456", presentation.StartingGapText);
    }

    [Fact]
    public void Build_WhenForceAutoSelectTargetDisabled_AndNoEffectiveTarget_DoesNotAutoSelect()
    {
        var scenario = new MainWindowSingleScanScenarioResult(
            HasScenario: true,
            SuggestedAheadDriverNumber: null,
            EffectiveTargetDriverNumber: null,
            DecisionLapNumber: 15,
            AttackerReferencePace: 89.9,
            TargetReferencePace: 90.1,
            AttackerCompound: "SOFT",
            AttackerTyreAge: 6,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 9,
            StartingGapSeconds: null);

        var presentation = SingleScanWorkflowService.Build(
            scenario,
            selectedTargetDriverNumber: null,
            forceAutoSelectTarget: false);

        Assert.True(presentation.HasScenario);
        Assert.False(presentation.ShouldAutoSelectTarget);
        Assert.Null(presentation.TargetDriverToSelect);
        Assert.Equal(string.Empty, presentation.StartingGapText);
    }

    [Fact]
    public void Build_WhenForceAutoSelectTargetEnabled_ButNoEffectiveTarget_DoesNotAutoSelect()
    {
        var scenario = new MainWindowSingleScanScenarioResult(
            HasScenario: true,
            SuggestedAheadDriverNumber: null,
            EffectiveTargetDriverNumber: null,
            DecisionLapNumber: 15,
            AttackerReferencePace: 89.9,
            TargetReferencePace: 90.1,
            AttackerCompound: "SOFT",
            AttackerTyreAge: 6,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 9,
            StartingGapSeconds: null);

        var presentation = SingleScanWorkflowService.Build(
            scenario,
            selectedTargetDriverNumber: 4,
            forceAutoSelectTarget: true);

        Assert.True(presentation.HasScenario);
        Assert.False(presentation.ShouldAutoSelectTarget);
        Assert.Null(presentation.TargetDriverToSelect);
    }

    [Fact]
    public void Build_WhenTargetAlreadySelected_AndForceFlagFalse_PreservesManualTargetChoiceByNotAutoSelecting()
    {
        var scenario = new MainWindowSingleScanScenarioResult(
            HasScenario: true,
            SuggestedAheadDriverNumber: 81,
            EffectiveTargetDriverNumber: 81,
            DecisionLapNumber: 40,
            AttackerReferencePace: 90.444,
            TargetReferencePace: 90.555,
            AttackerCompound: "HARD",
            AttackerTyreAge: 18,
            TargetCompound: "MEDIUM",
            TargetTyreAge: 14,
            StartingGapSeconds: -0.1234);

        var presentation = SingleScanWorkflowService.Build(
            scenario,
            selectedTargetDriverNumber: 63,
            forceAutoSelectTarget: false);

        Assert.True(presentation.HasScenario);
        Assert.False(presentation.ShouldAutoSelectTarget);
        Assert.Equal(81, presentation.TargetDriverToSelect);
        Assert.Equal("-0.123", presentation.StartingGapText);
    }

    #endregion
}
