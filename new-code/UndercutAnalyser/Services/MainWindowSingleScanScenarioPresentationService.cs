using System.Globalization;

namespace UndercutAnalyser.Services;

/// <summary>
/// Projects derived single-scan scenario data into UI-ready field text and selection intent.
/// </summary>
public static class MainWindowSingleScanScenarioPresentationService
{
    /// <summary>
    /// Builds presentation values for single-scan scenario controls.
    /// </summary>
    public static MainWindowSingleScanScenarioPresentation Build(
        MainWindowSingleScanScenarioResult scenario,
        int? selectedTargetDriverNumber)
    {
        if (!scenario.HasScenario)
            return MainWindowSingleScanScenarioPresentation.Empty;

        var shouldAutoSelectTarget = scenario.EffectiveTargetDriverNumber.HasValue &&
                                     (!selectedTargetDriverNumber.HasValue ||
                                      selectedTargetDriverNumber.Value != scenario.EffectiveTargetDriverNumber.Value);

        return new MainWindowSingleScanScenarioPresentation(
            HasScenario: true,
            ShouldAutoSelectTarget: shouldAutoSelectTarget,
            TargetDriverToSelect: scenario.EffectiveTargetDriverNumber,
            AttackerPaceText: scenario.AttackerReferencePace > 0
                ? scenario.AttackerReferencePace.ToString("F3", CultureInfo.InvariantCulture)
                : string.Empty,
            TargetPaceText: scenario.TargetReferencePace > 0
                ? scenario.TargetReferencePace.ToString("F3", CultureInfo.InvariantCulture)
                : string.Empty,
            AttackerCompoundText: scenario.AttackerCompound,
            AttackerTyreAgeText: scenario.AttackerTyreAge.ToString(CultureInfo.InvariantCulture),
            TargetCompoundText: scenario.TargetCompound,
            TargetTyreAgeText: scenario.TargetTyreAge.ToString(CultureInfo.InvariantCulture),
            StartingGapText: scenario.StartingGapSeconds.HasValue
                ? scenario.StartingGapSeconds.Value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)
                : string.Empty);
    }
}

/// <summary>
/// UI-ready single-scan scenario projection for MainWindow control updates.
/// </summary>
public sealed record MainWindowSingleScanScenarioPresentation(
    bool HasScenario,
    bool ShouldAutoSelectTarget,
    int? TargetDriverToSelect,
    string AttackerPaceText,
    string TargetPaceText,
    string AttackerCompoundText,
    string AttackerTyreAgeText,
    string TargetCompoundText,
    string TargetTyreAgeText,
    string StartingGapText)
{
    /// <summary>
    /// Empty projection that clears all scenario fields.
    /// </summary>
    public static MainWindowSingleScanScenarioPresentation Empty => new(
        HasScenario: false,
        ShouldAutoSelectTarget: false,
        TargetDriverToSelect: null,
        AttackerPaceText: string.Empty,
        TargetPaceText: string.Empty,
        AttackerCompoundText: string.Empty,
        AttackerTyreAgeText: string.Empty,
        TargetCompoundText: string.Empty,
        TargetTyreAgeText: string.Empty,
        StartingGapText: string.Empty);
}
