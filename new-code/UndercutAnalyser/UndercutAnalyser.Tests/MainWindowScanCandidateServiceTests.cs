using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowScanCandidateServiceTests
{
    [Fact]
    public void BuildLapIndex_KeepsOnlyLapsWithDateStartAndDuration()
    {
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 1, LapNumber = 1, DateStart = DateTime.UtcNow, LapDuration = 90f },
            new() { DriverNumber = 1, LapNumber = 2, DateStart = DateTime.UtcNow, LapDuration = null },
            new() { DriverNumber = 2, LapNumber = 1, DateStart = null, LapDuration = 91f }
        };

        var index = MainWindowScanCandidateService.BuildLapIndex(laps);

        Assert.True(index.ContainsKey(1));
        Assert.Single(index[1]);
        Assert.True(index[1].ContainsKey(1));
        Assert.True(index.ContainsKey(2));
        Assert.Empty(index[2]);
    }

    [Fact]
    public void BuildOnTrackOrderPerLap_OrdersDriversBySectorTwoCrossingTime()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var lapIndex = new Dictionary<int, Dictionary<int, EventLap>>
        {
            [1] = new Dictionary<int, EventLap>
            {
                [5] = new() { DriverNumber = 1, LapNumber = 5, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 30f, LapDuration = 90f }
            },
            [2] = new Dictionary<int, EventLap>
            {
                [5] = new() { DriverNumber = 2, LapNumber = 5, DateStart = t0, DurationSector1 = 19f, DurationSector2 = 29f, LapDuration = 90f }
            }
        };

        var order = MainWindowScanCandidateService.BuildOnTrackOrderPerLap(lapIndex);

        Assert.Equal(new[] { 2, 1 }, order[5]);
    }

    [Fact]
    public void IsInAnySafetyCarWindow_DetectsOverlap()
    {
        var lap = new EventLap
        {
            DateStart = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            LapDuration = 90f
        };

        var windows = new List<TimeWindow>
        {
            new(new DateTime(2025,1,1,12,0,30,DateTimeKind.Utc), new DateTime(2025,1,1,12,1,0,DateTimeKind.Utc))
        };

        Assert.True(MainWindowScanCandidateService.IsInAnySafetyCarWindow(lap, windows));
    }

    [Fact]
    public void FindCandidates_ReturnsExpectedCandidate_WhenAllConditionsMatch()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var lapIndex = new Dictionary<int, Dictionary<int, EventLap>>
        {
            [1] = new Dictionary<int, EventLap>
            {
                [10] = new() { DriverNumber = 1, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f }
            },
            [2] = new Dictionary<int, EventLap>
            {
                [10] = new() { DriverNumber = 2, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 30f, LapDuration = 90f }
            }
        };

        var orderPerLap = new Dictionary<int, List<int>>
        {
            [10] = [2, 1]
        };

        var refPace = new Dictionary<int, double>
        {
            [1] = 91.0,
            [2] = 90.5
        };

        (string compound, int age) TyreResolver(int driver, int lap) => ("SOFT", 8);

        var candidates = MainWindowScanCandidateService.FindCandidates(
            driverNumbers: [1],
            lapIndex: lapIndex,
            orderPerLap: orderPerLap,
            referencePaceByDriver: refPace,
            safetyCarWindows: [],
            minAge: 5,
            tyreStateResolver: TyreResolver);

        var candidate = Assert.Single(candidates);
        Assert.Equal(1, candidate.AttackerNumber);
        Assert.Equal(2, candidate.TargetNumber);
        Assert.Equal(10, candidate.DecisionLap);
        Assert.Equal(1, candidate.AttackerPosition);
        Assert.True(candidate.InitialGapSeconds > 0);
    }

    [Fact]
    public void FindCandidates_SkipsWhenAttackerTyreAgeBelowMinimum()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var lapIndex = new Dictionary<int, Dictionary<int, EventLap>>
        {
            [1] = new Dictionary<int, EventLap>
            {
                [10] = new() { DriverNumber = 1, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 31f, LapDuration = 91f }
            },
            [2] = new Dictionary<int, EventLap>
            {
                [10] = new() { DriverNumber = 2, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 30f, LapDuration = 90f }
            }
        };

        var orderPerLap = new Dictionary<int, List<int>> { [10] = [2, 1] };
        var refPace = new Dictionary<int, double> { [1] = 91.0, [2] = 90.5 };
        (string compound, int age) TyreResolver(int driver, int lap) => ("SOFT", 2);

        var candidates = MainWindowScanCandidateService.FindCandidates(
            driverNumbers: [1],
            lapIndex: lapIndex,
            orderPerLap: orderPerLap,
            referencePaceByDriver: refPace,
            safetyCarWindows: [],
            minAge: 5,
            tyreStateResolver: TyreResolver);

        Assert.Empty(candidates);
    }
}
