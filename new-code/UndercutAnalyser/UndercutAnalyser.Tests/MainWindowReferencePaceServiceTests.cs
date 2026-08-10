using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowReferencePaceServiceTests
{
    [Fact]
    public void DeriveReferencePacePerDriver_ExcludesPitOutPitInAndScVscLaps()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0.AddMinutes(0), LapDuration = 90f },
            new() { DriverNumber = 4, LapNumber = 2, DateStart = t0.AddMinutes(2), LapDuration = 91f, IsPitOutLap = true }, // pit-out excluded
            new() { DriverNumber = 4, LapNumber = 3, DateStart = t0.AddMinutes(4), LapDuration = 92f }, // pit-in inferred from lap 4 pit-out
            new() { DriverNumber = 4, LapNumber = 4, DateStart = t0.AddMinutes(6), LapDuration = 93f, IsPitOutLap = true },
            new() { DriverNumber = 4, LapNumber = 5, DateStart = t0.AddMinutes(8), LapDuration = 94f }, // SC overlap excluded
            new() { DriverNumber = 4, LapNumber = 6, DateStart = t0.AddMinutes(10), LapDuration = 96f } // included
        };

        var windows = new List<TimeWindow>
        {
            new(t0.AddMinutes(8), t0.AddMinutes(9))
        };

        var result = RaceTraceWorkflowService.DeriveReferencePacePerDriver(laps, windows);

        var pace = Assert.Single(result);
        Assert.Equal(4, pace.Key);
        Assert.Equal(96d, pace.Value, 10);
    }

    [Fact]
    public void DeriveReferencePacePerDriver_ReturnsOnlyDriversWithPositivePace()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f },
            new() { DriverNumber = 81, LapNumber = 1, DateStart = t0, LapDuration = null }
        };

        var result = RaceTraceWorkflowService.DeriveReferencePacePerDriver(laps, []);

        Assert.Single(result);
        Assert.True(result.ContainsKey(4));
        Assert.False(result.ContainsKey(81));
    }
}
