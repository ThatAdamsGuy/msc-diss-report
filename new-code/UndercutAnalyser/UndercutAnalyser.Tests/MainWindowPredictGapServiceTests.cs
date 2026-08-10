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
