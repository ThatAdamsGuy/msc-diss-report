using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class RaceTimingDomainLogicTests
{
    [Fact]
    public void AsUtc_UnspecifiedKind_ReturnsUtcKind()
    {
        var input = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);

        var utc = RaceTimingDomainLogic.AsUtc(input);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(input, utc);
    }

    [Fact]
    public void BuildPitInLapLookup_MapsPitOutToPriorLap()
    {
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 10, IsPitOutLap = false },
            new() { DriverNumber = 4, LapNumber = 11, IsPitOutLap = true },
            new() { DriverNumber = 81, LapNumber = 1, IsPitOutLap = true }
        };

        var lookup = RaceTimingDomainLogic.BuildPitInLapLookup(laps);

        Assert.Contains((4, 10), lookup);
        Assert.DoesNotContain((81, 0), lookup);
    }

    [Fact]
    public void IsInAnyWindow_EventLap_ReturnsTrueOnOverlap()
    {
        var lap = new EventLap
        {
            DriverNumber = 4,
            LapNumber = 12,
            DateStart = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            LapDuration = 90f
        };

        var windows = new List<TimeWindow>
        {
            new(new DateTime(2025, 1, 1, 12, 0, 30, DateTimeKind.Utc), new DateTime(2025, 1, 1, 12, 2, 0, DateTimeKind.Utc))
        };

        Assert.True(RaceTimingDomainLogic.IsInAnyWindow(lap, windows));
    }

    [Fact]
    public void BuildSafetyCarWindows_ClosesOpenWindowsAtLatestTimestamp()
    {
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var messages = new List<RaceControlMessage>
        {
            new() { Category = "SafetyCar", Message = "VSC DEPLOYED", Date = baseTime.AddSeconds(10) },
            new() { Category = "SafetyCar", Message = "OTHER", Date = baseTime.AddSeconds(40) }
        };

        var windows = RaceTimingDomainLogic.BuildSafetyCarWindows(messages);

        var window = Assert.Single(windows);
        Assert.Equal(baseTime.AddSeconds(10), window.StartUtc);
        Assert.Equal(baseTime.AddSeconds(40), window.EndUtc);
    }

    [Fact]
    public void BuildSafetyCarLapNumbers_ReturnsInclusiveLapRange()
    {
        var messages = new List<RaceControlMessage>
        {
            new() { Category = "SafetyCar", Message = "SAFETY CAR DEPLOYED", LapNumber = 4 },
            new() { Category = "SafetyCar", Message = "SAFETY CAR IN THIS LAP", LapNumber = 11 }
        };

        var laps = RaceTimingDomainLogic.BuildSafetyCarLapNumbers(messages);

        Assert.Equal([4, 5, 6, 7, 8, 9, 10, 11], laps.OrderBy(x => x).ToArray());
    }
}
