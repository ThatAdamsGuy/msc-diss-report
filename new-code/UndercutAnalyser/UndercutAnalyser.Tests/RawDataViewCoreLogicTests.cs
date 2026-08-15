using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class RawDataViewCoreLogicTests
{
    [Fact]
    public void BuildPitLapsCore_IncludesPriorLapForPitOutAboveLapOne()
    {
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, IsPitOutLap = true },
            new() { DriverNumber = 4, LapNumber = 3, IsPitOutLap = true },
            new() { DriverNumber = 4, LapNumber = 5, IsPitOutLap = false }
        };

        var pitLaps = RawDataView.BuildPitLapsCore(laps);

        Assert.Single(pitLaps);
        Assert.Contains(2, pitLaps);
    }

    [Fact]
    public void BuildLapNumbersCore_ReturnsDistinctSortedLapNumbers()
    {
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 3 },
            new() { DriverNumber = 81, LapNumber = 1 },
            new() { DriverNumber = 4, LapNumber = 2 },
            new() { DriverNumber = 4, LapNumber = 3 }
        };

        var result = RawDataView.BuildLapNumbersCore(laps);

        Assert.Equal([1, 2, 3], result);
    }

    [Fact]
    public void ResolveDriverNameCore_UsesFullNameThenCodeThenFallback()
    {
        var byNumber = new Dictionary<int, Driver>
        {
            [4] = new() { DriverNumber = 4, FirstName = "Lando", LastName = "Norris", Code = "NOR" },
            [81] = new() { DriverNumber = 81, FirstName = "", LastName = "", Code = "PIA" },
            [63] = new() { DriverNumber = 63, FirstName = "", LastName = "", Code = "" }
        };

        Assert.Equal("Lando Norris", RawDataView.ResolveDriverNameCore(4, byNumber));
        Assert.Equal("PIA", RawDataView.ResolveDriverNameCore(81, byNumber));
        Assert.Equal("Driver 63", RawDataView.ResolveDriverNameCore(63, byNumber));
        Assert.Equal("Driver 99", RawDataView.ResolveDriverNameCore(99, byNumber));
    }

    [Fact]
    public void BuildCompoundsByLapCore_SkipsInvalidRanges_AndMapsValidAndOpenEndedStints()
    {
        var stints = new List<EventStint>
        {
            new() { DriverNumber = 4, LapStart = -1, LapEnd = 3, Compound = "SOFT" },
            new() { DriverNumber = 4, LapStart = 7, LapEnd = 5, Compound = "HARD" },
            new() { DriverNumber = 4, LapStart = 1, LapEnd = 2, Compound = "MEDIUM" },
            new() { DriverNumber = 4, LapStart = 3, LapEnd = null, Compound = "HARD" }
        };

        var map = RawDataView.BuildCompoundsByLapCore(stints, maxDriverLap: 5);

        Assert.Equal("MEDIUM", map[1]);
        Assert.Equal("MEDIUM", map[2]);
        Assert.Equal("HARD", map[3]);
        Assert.Equal("HARD", map[4]);
        Assert.Equal("HARD", map[5]);
        Assert.Equal(5, map.Count);
    }

    [Fact]
    public void BuildExtraInfoTextCore_FormatsWithAndWithoutReference()
    {
        var withReference = RawDataView.BuildExtraInfoTextCore(
            reference: new ReferenceLapTimeResult(
                TotalLapRows: 200,
                MaxSessionLapNumber: 57,
                IncludedLaps: 140,
                ExcludedPitOutLaps: 8,
                ExcludedPitInLaps: 9,
                ExcludedSafetyCarLaps: 10,
                FuelEffectPerLapSeconds: 0.123,
                SumLapTimeSeconds: 10000,
                AverageLapTimeSeconds: 88.765,
                SafetyCarWindows: []),
            lapCount: 150,
            lapNumberCount: 55,
            fuelKg: 100,
            fuelSecondsPer10Kg: 0.3,
            stintsCount: 12);

        var withoutReference = RawDataView.BuildExtraInfoTextCore(
            reference: null,
            lapCount: 150,
            lapNumberCount: 55,
            fuelKg: 100,
            fuelSecondsPer10Kg: 0.3,
            stintsCount: 12);

        Assert.Contains("Rows: 200", withReference, StringComparison.Ordinal);
        Assert.Contains("Reference avg: 88.765s", withReference, StringComparison.Ordinal);
        Assert.Contains("Fuel/lap: 0.123s", withReference, StringComparison.Ordinal);

        Assert.Contains("Rows: 150", withoutReference, StringComparison.Ordinal);
        Assert.Contains("Reference avg: N/A", withoutReference, StringComparison.Ordinal);
        Assert.Contains("Fuel/lap: N/As", withoutReference, StringComparison.Ordinal);
    }
}
