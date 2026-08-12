using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictGapServiceTests
{
    [Fact]
    public void TryDeriveInitialGapSeconds_ReturnsSignedGap_WhenBothDriverLapsHaveSectorTwoCrossing()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 12, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 50f },
            new() { DriverNumber = 81, LapNumber = 12, DateStart = t0, DurationSector1 = 19f, DurationSector2 = 50f }
        };

        var result = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 12);

        Assert.NotNull(result);
        Assert.Equal(1.0, result.Value, 10);
    }

    [Fact]
    public void TryDeriveInitialGapSeconds_UsesAbsoluteCrossingTimes_NotRelativeSectorSums()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            // Attacker starts earlier but has slower S1+S2 (crosses at +60s)
            new() { DriverNumber = 4, LapNumber = 22, DateStart = t0, DurationSector1 = 30f, DurationSector2 = 30f },
            // Target starts 8s later but has much quicker S1+S2 (crosses at +58s)
            new() { DriverNumber = 81, LapNumber = 22, DateStart = t0.AddSeconds(8), DurationSector1 = 25f, DurationSector2 = 25f }
        };

        var result = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 22);

        Assert.NotNull(result);
        Assert.Equal(2.0, result.Value, 10);
    }

    [Fact]
    public void TryDeriveInitialGapSeconds_ReturnsZero_WhenCrossingsAreSimultaneous()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 30, DateStart = t0, DurationSector1 = 25f, DurationSector2 = 35f },
            new() { DriverNumber = 81, LapNumber = 30, DateStart = t0.AddSeconds(4), DurationSector1 = 23f, DurationSector2 = 33f }
        };

        var result = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 30);

        Assert.NotNull(result);
        Assert.Equal(0.0, result.Value, 10);
    }

    [Fact]
    public void TryDeriveInitialGapSeconds_ReturnsNull_WhenEitherDecisionLapIsMissing()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 12, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 50f }
        };

        var result = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 12);

        Assert.Null(result);
    }

    [Fact]
    public void TryDeriveInitialGapSeconds_ReturnsNull_WhenAttackerDateStartIsMissing()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 12, DateStart = null, DurationSector1 = 20f, DurationSector2 = 50f },
            new() { DriverNumber = 81, LapNumber = 12, DateStart = t0, DurationSector1 = 19f, DurationSector2 = 50f }
        };

        var result = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 12);

        Assert.Null(result);
    }

    [Fact]
    public void TryDeriveInitialGapSeconds_ReturnsNull_WhenSectorTwoCrossingDataIsUnavailable()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 12, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 50f },
            new() { DriverNumber = 81, LapNumber = 12, DateStart = t0, DurationSector1 = 19f, DurationSector2 = null }
        };

        var result = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 12);

        Assert.Null(result);
    }
}
