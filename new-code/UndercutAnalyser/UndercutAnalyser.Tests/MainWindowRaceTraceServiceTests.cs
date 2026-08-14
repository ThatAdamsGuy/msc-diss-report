using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowRaceTraceServiceTests
{
    [Fact]
    public void Compute_ReturnsNoData_WhenNoLaps()
    {
        var result = RaceTraceWorkflowService.Compute(
            laps: [],
            drivers: [],
            raceControlMessages: [],
            options: new RaceTraceComputationOptions(true, true, false, 0.3, 110, false),
            traceVisibilityByDriver: new Dictionary<int, bool>());

        Assert.False(result.HasRenderableData);
        Assert.Empty(result.Series);
        Assert.Empty(result.NormalizedVisibilityByDriver);
    }

    [Fact]
    public void Compute_OrdersByDriverNumber_WhenTeamSortDisabled()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 1, DateStart = t0, LapDuration = 91f },
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 81, Code = "PIA", BroadcastName = "Piastri", TeamName = "McLaren", TeamColour = "FF8000" },
            new() { DriverNumber = 4, Code = "NOR", BroadcastName = "Norris", TeamName = "McLaren", TeamColour = "FF8000" }
        };

        var result = RaceTraceWorkflowService.Compute(
            laps,
            drivers,
            raceControlMessages: [],
            options: new RaceTraceComputationOptions(true, true, false, 0.3, 110, false),
            traceVisibilityByDriver: new Dictionary<int, bool>());

        Assert.True(result.HasRenderableData);
        Assert.Equal([4, 81], result.Series.Select(s => s.DriverNumber).ToArray());
    }

    [Fact]
    public void Compute_OrdersByTeamThenNumber_WhenTeamSortEnabled()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 1, DateStart = t0, LapDuration = 91f },
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f },
            new() { DriverNumber = 63, LapNumber = 1, DateStart = t0, LapDuration = 92f }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 81, Code = "PIA", TeamName = "McLaren", TeamColour = "FF8000" },
            new() { DriverNumber = 4, Code = "NOR", TeamName = "McLaren", TeamColour = "FF8000" },
            new() { DriverNumber = 63, Code = "RUS", TeamName = "Mercedes", TeamColour = "00D2BE" }
        };

        var result = RaceTraceWorkflowService.Compute(
            laps,
            drivers,
            raceControlMessages: [],
            options: new RaceTraceComputationOptions(true, true, false, 0.3, 110, true),
            traceVisibilityByDriver: new Dictionary<int, bool>());

        Assert.True(result.HasRenderableData);
        Assert.Equal([4, 81, 63], result.Series.Select(s => s.DriverNumber).ToArray());
    }

    [Fact]
    public void Compute_AssignsSolidAndDashedForTeammates()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f },
            new() { DriverNumber = 81, LapNumber = 1, DateStart = t0, LapDuration = 91f }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 4, Code = "NOR", TeamName = "McLaren", TeamColour = "FF8000" },
            new() { DriverNumber = 81, Code = "PIA", TeamName = "McLaren", TeamColour = "FF8000" }
        };

        var result = RaceTraceWorkflowService.Compute(
            laps,
            drivers,
            raceControlMessages: [],
            options: new RaceTraceComputationOptions(true, true, false, 0.3, 110, false),
            traceVisibilityByDriver: new Dictionary<int, bool>());

        var byDriver = result.Series.ToDictionary(s => s.DriverNumber);
        Assert.True(byDriver[4].IsSolidLine);
        Assert.False(byDriver[81].IsSolidLine);
    }

    [Fact]
    public void Compute_NormalizesVisibility_RemovesStaleAndDefaultsMissingToVisible()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f },
            new() { DriverNumber = 81, LapNumber = 1, DateStart = t0, LapDuration = 91f }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 4, Code = "NOR" },
            new() { DriverNumber = 81, Code = "PIA" }
        };

        var visibility = new Dictionary<int, bool>
        {
            [4] = false,
            [99] = true
        };

        var result = RaceTraceWorkflowService.Compute(
            laps,
            drivers,
            raceControlMessages: [],
            options: new RaceTraceComputationOptions(true, true, false, 0.3, 110, false),
            traceVisibilityByDriver: visibility);

        Assert.False(result.NormalizedVisibilityByDriver[4]);
        Assert.True(result.NormalizedVisibilityByDriver[81]);
        Assert.DoesNotContain(99, result.NormalizedVisibilityByDriver.Keys);
    }

    [Fact]
    public void Compute_ExcludesPitInAndPitOut_WhenIncludePitLapsFalse()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f },
            new() { DriverNumber = 4, LapNumber = 2, DateStart = t0.AddSeconds(90), LapDuration = 91f },
            new() { DriverNumber = 4, LapNumber = 3, DateStart = t0.AddSeconds(181), LapDuration = 92f, IsPitOutLap = true },
            new() { DriverNumber = 4, LapNumber = 4, DateStart = t0.AddSeconds(273), LapDuration = 93f }
        };

        var result = RaceTraceWorkflowService.Compute(
            laps,
            drivers: [new Driver { DriverNumber = 4, Code = "NOR" }],
            raceControlMessages: [],
            options: new RaceTraceComputationOptions(false, true, false, 0.3, 110, false),
            traceVisibilityByDriver: new Dictionary<int, bool>());

        var series = Assert.Single(result.Series);
        Assert.Equal([1d, 4d], series.Xs.ToArray());
    }

    [Fact]
    public void Compute_UsesLeaderLapReferenceDuringSafetyCarWindow_KeepingLeaderFlat()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f },
            new() { DriverNumber = 81, LapNumber = 1, DateStart = t0, LapDuration = 91f },

            new() { DriverNumber = 4, LapNumber = 2, DateStart = t0.AddSeconds(90), LapDuration = 120f },
            new() { DriverNumber = 81, LapNumber = 2, DateStart = t0.AddSeconds(91), LapDuration = 121f },

            new() { DriverNumber = 4, LapNumber = 3, DateStart = t0.AddSeconds(210), LapDuration = 119f },
            new() { DriverNumber = 81, LapNumber = 3, DateStart = t0.AddSeconds(212), LapDuration = 121f }
        };

        var messages = new List<RaceControlMessage>
        {
            new() { Category = "SafetyCar", Date = t0.AddSeconds(95), Message = "SAFETY CAR DEPLOYED" },
            new() { Category = "SafetyCar", Date = t0.AddSeconds(330), Message = "SAFETY CAR IN THIS LAP" }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 4, Code = "NOR" },
            new() { DriverNumber = 81, Code = "PIA" }
        };

        var result = RaceTraceWorkflowService.Compute(
            laps,
            drivers,
            raceControlMessages: messages,
            options: new RaceTraceComputationOptions(true, true, false, 0.3, 110, false),
            traceVisibilityByDriver: new Dictionary<int, bool>());

        var leaderSeries = Assert.Single(result.Series.Where(s => s.DriverNumber == 4));
        Assert.Equal(0d, leaderSeries.Ys[1] - leaderSeries.Ys[0], 6);
        Assert.Equal(0d, leaderSeries.Ys[2] - leaderSeries.Ys[1], 6);
    }

    [Fact]
    public void Compute_UsesLeaderLapReferenceDuringSafetyCarLapNumbersWindow_KeepingLeaderFlat()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 1, DateStart = t0, LapDuration = 90f },
            new() { DriverNumber = 81, LapNumber = 1, DateStart = t0.AddSeconds(2), LapDuration = 91f },

            new() { DriverNumber = 4, LapNumber = 2, DateStart = t0.AddSeconds(90), LapDuration = 120f },
            new() { DriverNumber = 81, LapNumber = 2, DateStart = t0.AddSeconds(92), LapDuration = 121f },

            new() { DriverNumber = 4, LapNumber = 3, DateStart = t0.AddSeconds(210), LapDuration = 118f },
            new() { DriverNumber = 81, LapNumber = 3, DateStart = t0.AddSeconds(213), LapDuration = 121f },

            new() { DriverNumber = 4, LapNumber = 4, DateStart = t0.AddSeconds(328), LapDuration = 119f },
            new() { DriverNumber = 81, LapNumber = 4, DateStart = t0.AddSeconds(332), LapDuration = 121f }
        };

        var messages = new List<RaceControlMessage>
        {
            new() { Category = "SafetyCar", Message = "SAFETY CAR DEPLOYED", LapNumber = 2 },
            new() { Category = "SafetyCar", Message = "SAFETY CAR IN THIS LAP", LapNumber = 3 }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 4, Code = "NOR" },
            new() { DriverNumber = 81, Code = "PIA" }
        };

        var result = RaceTraceWorkflowService.Compute(
            laps,
            drivers,
            raceControlMessages: messages,
            options: new RaceTraceComputationOptions(true, true, false, 0.3, 110, false),
            traceVisibilityByDriver: new Dictionary<int, bool>());

        var leaderSeries = Assert.Single(result.Series.Where(s => s.DriverNumber == 4));
        Assert.Equal(0d, leaderSeries.Ys[1] - leaderSeries.Ys[0], 6);
        Assert.Equal(0d, leaderSeries.Ys[2] - leaderSeries.Ys[1], 6);
    }

    [Fact]
    public void ParseScottPlotColor_FallsBackForInvalidHex()
    {
        var color = RaceTraceWorkflowService.ParseScottPlotColor("not-a-color", 3);

        Assert.Equal(ScottPlot.Colors.Orange, color);
    }
}
