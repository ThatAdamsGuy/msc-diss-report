using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSelectorPopulationServiceTests
{
    [Fact]
    public void BuildOrderedDriverSelections_UsesLapParticipation_OrderAndDisplayFallbacks()
    {
        var drivers = new List<Driver>
        {
            new() { DriverNumber = 81, Code = "", BroadcastName = "Piastri" },
            new() { DriverNumber = 4, Code = "NOR", BroadcastName = "Norris" },
            new() { DriverNumber = 63, Code = "RUS", BroadcastName = "Russell" }
        };

        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 1 },
            new() { DriverNumber = 4, LapNumber = 1 },
            new() { DriverNumber = 999, LapNumber = 1 }
        };

        var result = MainWindowSelectorPopulationService.BuildOrderedDriverSelections(drivers, laps);

        Assert.Equal(2, result.Count);
        Assert.Equal(4, result[0].DriverNumber);
        Assert.Equal("NOR", result[0].Code);
        Assert.Equal("NOR – Norris", result[0].DisplayName);

        Assert.Equal(81, result[1].DriverNumber);
        Assert.Equal("81", result[1].Code);
        Assert.Equal("81 – Piastri", result[1].DisplayName);
    }

    [Fact]
    public void BuildTargetDecisionLapChoices_FiltersByTargetAndSectorTwoAvailability()
    {
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 3, DateStart = DateTime.UtcNow, DurationSector1 = 20f, DurationSector2 = 30f },
            new() { DriverNumber = 4, LapNumber = 2, DateStart = DateTime.UtcNow, DurationSector1 = 20f, DurationSector2 = 29f },
            new() { DriverNumber = 4, LapNumber = 3, DateStart = DateTime.UtcNow, DurationSector1 = 20f, DurationSector2 = 30f },
            new() { DriverNumber = 4, LapNumber = 1, DateStart = null, DurationSector1 = 20f, DurationSector2 = 30f },
            new() { DriverNumber = 81, LapNumber = 4, DateStart = DateTime.UtcNow, DurationSector1 = 20f, DurationSector2 = 30f }
        };

        var result = MainWindowSelectorPopulationService.BuildTargetDecisionLapChoices(laps, targetDriverNumber: 4);

        Assert.Equal([2, 3], result);
    }

    [Fact]
    public void ChooseDecisionLap_PrefersExistingSelectionOtherwiseFirst()
    {
        var laps = new List<int> { 5, 8, 10 };

        var kept = MainWindowSelectorPopulationService.ChooseDecisionLap(laps, preferredLap: 8);
        var fallback = MainWindowSelectorPopulationService.ChooseDecisionLap(laps, preferredLap: 7);

        Assert.Equal(8, kept);
        Assert.Equal(5, fallback);
    }

    [Fact]
    public void ChooseDecisionLap_ReturnsNull_WhenNoLapsAvailable()
    {
        var result = MainWindowSelectorPopulationService.ChooseDecisionLap([], preferredLap: 12);

        Assert.Null(result);
    }

    [Fact]
    public void BuildPredictDecisionLapChoices_ReturnsRangeAndMidpointDefault()
    {
        var laps = new List<EventLap>
        {
            new() { LapNumber = 1 },
            new() { LapNumber = 10 }
        };

        var (choices, defaultLap) = MainWindowSelectorPopulationService.BuildPredictDecisionLapChoices(laps);

        Assert.Equal(10, choices.Count);
        Assert.Equal(1, choices.First());
        Assert.Equal(10, choices.Last());
        Assert.Equal(5, defaultLap);
    }
}
