using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSingleScanScenarioServiceTests
{
    [Fact]
    public void Derive_ReturnsEmpty_WhenAttackerMissing()
    {
        var result = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: null,
            SelectedTarget: null,
            DecisionLapNumber: 5,
            AvailableTargetDriverNumbers: [81],
            Laps: [],
            Stints: [],
            RaceControlMessages: []));

        Assert.False(result.HasScenario);
    }

    [Fact]
    public void Derive_SuggestsAheadDriverAndBuildsScenario()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 10, DateStart = t0, DurationSector1 = 19f, DurationSector2 = 30f, LapDuration = 89f },
            new() { DriverNumber = 4, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f }
        };

        var result = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: new PredictionSelection(4, "NOR", "NOR"),
            SelectedTarget: new PredictionSelection(63, "RUS", "RUS"),
            DecisionLapNumber: 10,
            AvailableTargetDriverNumbers: [81, 63],
            Laps: laps,
            Stints: [],
            RaceControlMessages: []));

        Assert.True(result.HasScenario);
        Assert.Equal(81, result.SuggestedAheadDriverNumber);
        Assert.Equal(81, result.EffectiveTargetDriverNumber);
        Assert.False(string.IsNullOrWhiteSpace(result.AttackerCompound));
        Assert.False(string.IsNullOrWhiteSpace(result.TargetCompound));
        Assert.True(result.StartingGapSeconds.HasValue);
    }
}
