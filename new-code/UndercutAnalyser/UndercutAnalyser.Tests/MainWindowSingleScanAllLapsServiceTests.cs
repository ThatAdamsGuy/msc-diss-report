using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSingleScanAllLapsServiceTests
{
    [Fact]
    public void RunAllLaps_CollectsSuccessfulRows_AndSkipsMissingGapLaps()
    {
        var input = CreateFixedTargetInput([10, 11, 12]);

        var rows = SingleScanWorkflowService.RunAllLaps(
            input,
            resolveGapText: (_, _, lap) => lap == 11 ? string.Empty : "1.000",
            resolveTyreState: (_, _) => ("SOFT", 5));

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("NOR", row.Attacker));
        Assert.All(rows, row => Assert.Equal("PIA", row.Target));
    }

    [Fact]
    public void RunAllLaps_WhenRunValidationFails_SkipsInvalidScenarioAndKeepsValidRows()
    {
        var input = CreateFixedTargetInput([10, 11]);

        var rows = SingleScanWorkflowService.RunAllLaps(
            input,
            resolveGapText: (_, _, lap) => lap == 10 ? "invalid-gap" : "1.250",
            resolveTyreState: (_, _) => ("SOFT", 5));

        var row = Assert.Single(rows);
        Assert.Equal("NOR", row.Attacker);
        Assert.Equal("PIA", row.Target);
    }

    private static MainWindowSingleScanAllLapsInput CreateFixedTargetInput(IReadOnlyList<int> laps)
    {
        return new MainWindowSingleScanAllLapsInput(
            Attacker: new PredictionSelection(4, "NOR", "Lando Norris"),
            Target: new PredictionSelection(81, "PIA", "Oscar Piastri"),
            DecisionLapNumbers: laps,
            EventName: "Test Event",
            AttackerPaceOverrideText: "90.2",
            TargetPaceOverrideText: "90.4",
            DerivedReferencePaceByDriver: new Dictionary<int, double>
            {
                [4] = 90.2,
                [81] = 90.4
            },
            AttackerReplacementCompoundText: "HARD",
            AttackerReplacementAgeText: "1",
            TargetReplacementCompoundText: "SOFT",
            TargetReplacementAgeText: "2",
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault());
    }
}
