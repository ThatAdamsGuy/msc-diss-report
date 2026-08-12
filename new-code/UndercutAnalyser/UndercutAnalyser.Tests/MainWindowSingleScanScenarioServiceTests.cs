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
    public void Derive_ReturnsEmpty_WhenDecisionLapMissing()
    {
        var result = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: new PredictionSelection(4, "NOR", "NOR"),
            SelectedTarget: null,
            DecisionLapNumber: null,
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

    [Fact]
    public void Derive_UsesSelectedTarget_WhenAheadTargetIsNotAvailableInTargetList()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 10, DateStart = t0, DurationSector1 = 19f, DurationSector2 = 30f, LapDuration = 89f },
            new() { DriverNumber = 4, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f },
            new() { DriverNumber = 63, LapNumber = 10, DateStart = t0.AddSeconds(2), DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f }
        };

        var result = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: new PredictionSelection(4, "NOR", "NOR"),
            SelectedTarget: new PredictionSelection(63, "RUS", "RUS"),
            DecisionLapNumber: 10,
            AvailableTargetDriverNumbers: [63],
            Laps: laps,
            Stints: [],
            RaceControlMessages: []));

        Assert.True(result.HasScenario);
        Assert.Equal(81, result.SuggestedAheadDriverNumber);
        Assert.Equal(63, result.EffectiveTargetDriverNumber);
    }

    [Fact]
    public void Derive_ReturnsEmpty_WhenNoSelectedTargetAndSuggestedTargetUnavailable()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 10, DateStart = t0, DurationSector1 = 19f, DurationSector2 = 30f, LapDuration = 89f },
            new() { DriverNumber = 4, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f }
        };

        var result = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: new PredictionSelection(4, "NOR", "NOR"),
            SelectedTarget: null,
            DecisionLapNumber: 10,
            AvailableTargetDriverNumbers: [63],
            Laps: laps,
            Stints: [],
            RaceControlMessages: []));

        Assert.False(result.HasScenario);
    }

    [Fact]
    public void Derive_UpdatesSuggestedTarget_WhenDecisionLapChanges()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            // Lap 10 order: 81 ahead of 4
            new() { DriverNumber = 81, LapNumber = 10, DateStart = t0, DurationSector1 = 19f, DurationSector2 = 30f, LapDuration = 89f },
            new() { DriverNumber = 4, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f },

            // Lap 11 order: 63 ahead of 4
            new() { DriverNumber = 63, LapNumber = 11, DateStart = t0.AddMinutes(2), DurationSector1 = 19f, DurationSector2 = 30f, LapDuration = 89f },
            new() { DriverNumber = 4, LapNumber = 11, DateStart = t0.AddMinutes(2), DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f }
        };

        var lap10 = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: new PredictionSelection(4, "NOR", "NOR"),
            SelectedTarget: new PredictionSelection(81, "PIA", "PIA"),
            DecisionLapNumber: 10,
            AvailableTargetDriverNumbers: [81, 63],
            Laps: laps,
            Stints: [],
            RaceControlMessages: []));

        var lap11 = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: new PredictionSelection(4, "NOR", "NOR"),
            SelectedTarget: new PredictionSelection(81, "PIA", "PIA"),
            DecisionLapNumber: 11,
            AvailableTargetDriverNumbers: [81, 63],
            Laps: laps,
            Stints: [],
            RaceControlMessages: []));

        Assert.True(lap10.HasScenario);
        Assert.True(lap11.HasScenario);
        Assert.Equal(81, lap10.SuggestedAheadDriverNumber);
        Assert.Equal(63, lap11.SuggestedAheadDriverNumber);
        Assert.Equal(81, lap10.EffectiveTargetDriverNumber);
        Assert.Equal(63, lap11.EffectiveTargetDriverNumber);
    }

    [Fact]
    public void Derive_ComputesGapWithExpectedSign_WhenAttackerIsBehind()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 12, DateStart = t0.AddSeconds(8), DurationSector1 = 25f, DurationSector2 = 25f, LapDuration = 90f },
            new() { DriverNumber = 4, LapNumber = 12, DateStart = t0, DurationSector1 = 30f, DurationSector2 = 30f, LapDuration = 92f }
        };

        var result = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: new PredictionSelection(4, "NOR", "NOR"),
            SelectedTarget: null,
            DecisionLapNumber: 12,
            AvailableTargetDriverNumbers: [81],
            Laps: laps,
            Stints: [],
            RaceControlMessages: []));

        Assert.True(result.HasScenario);
        Assert.NotNull(result.StartingGapSeconds);
        Assert.True(result.StartingGapSeconds!.Value > 0);
        Assert.Equal(2.0, result.StartingGapSeconds.Value, 10);
    }
}
