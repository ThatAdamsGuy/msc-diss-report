using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

/// <summary>
/// Regression tests for gap consistency across full-scan and single-scan paths.
/// These tests lock the convention to absolute Sector 2 crossing timestamps.
/// </summary>
public sealed class MainWindowGapConsistencyRegressionTests
{
    [Fact]
    public void FullScanCandidateGap_MatchesSingleScanGapDerivation_ForSameLapAndDrivers()
    {
        var t0 = new DateTime(2026, 7, 26, 14, 0, 0, DateTimeKind.Utc);

        var laps = new List<EventLap>
        {
            // target ahead in order list
            new() { DriverNumber = 44, LapNumber = 60, DateStart = t0.AddSeconds(7), DurationSector1 = 24f, DurationSector2 = 24f, LapDuration = 89f },
            // attacker behind
            new() { DriverNumber = 16, LapNumber = 60, DateStart = t0, DurationSector1 = 30f, DurationSector2 = 30f, LapDuration = 91f }
        };

        var lapIndex = ScanWorkflowService.BuildLapIndex(laps);
        var orderPerLap = ScanWorkflowService.BuildOnTrackOrderPerLap(lapIndex);

        var candidates = ScanWorkflowService.FindCandidates(
            driverNumbers: [16],
            lapIndex: lapIndex,
            orderPerLap: orderPerLap,
            referencePaceByDriver: new Dictionary<int, double> { [16] = 91.0, [44] = 89.0 },
            safetyCarWindows: [],
            minAge: 0,
            tyreStateResolver: (_, _) => ("SOFT", 10));

        var candidate = Assert.Single(candidates);

        var singleScanGap = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 16,
            targetDriverNumber: 44,
            decisionLapNumber: 60);

        Assert.NotNull(singleScanGap);
        Assert.Equal(candidate.InitialGapSeconds, singleScanGap!.Value, 10);
    }

    [Fact]
    public void GapSignConvention_IsConsistent_PositiveBehindNegativeAhead()
    {
        var t0 = new DateTime(2026, 7, 26, 14, 0, 0, DateTimeKind.Utc);

        var laps = new List<EventLap>
        {
            // Lap 10: attacker behind target by +2.0s at S2
            new() { DriverNumber = 4, LapNumber = 10, DateStart = t0, DurationSector1 = 25f, DurationSector2 = 35f },
            new() { DriverNumber = 81, LapNumber = 10, DateStart = t0.AddSeconds(8), DurationSector1 = 25f, DurationSector2 = 25f },

            // Lap 11: attacker ahead of target by -1.0s at S2
            new() { DriverNumber = 4, LapNumber = 11, DateStart = t0.AddMinutes(2), DurationSector1 = 25f, DurationSector2 = 35f },
            new() { DriverNumber = 81, LapNumber = 11, DateStart = t0.AddMinutes(2), DurationSector1 = 25f, DurationSector2 = 36f }
        };

        var lap10Gap = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 10);

        var lap11Gap = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps,
            attackerDriverNumber: 4,
            targetDriverNumber: 81,
            decisionLapNumber: 11);

        Assert.NotNull(lap10Gap);
        Assert.NotNull(lap11Gap);
        Assert.True(lap10Gap!.Value > 0.0);
        Assert.True(lap11Gap!.Value < 0.0);
        Assert.Equal(2.0, lap10Gap.Value, 10);
        Assert.Equal(-1.0, lap11Gap.Value, 10);
    }

    [Fact]
    public void FullScanCandidateGeneration_SkipsWhenGapIsNonPositive()
    {
        var t0 = new DateTime(2026, 7, 26, 14, 0, 0, DateTimeKind.Utc);

        var lapIndex = new Dictionary<int, Dictionary<int, EventLap>>
        {
            [16] = new Dictionary<int, EventLap>
            {
                // attacker crosses S2 earlier -> would imply negative gap (already ahead)
                [21] = new() { DriverNumber = 16, LapNumber = 21, DateStart = t0, DurationSector1 = 24f, DurationSector2 = 24f, LapDuration = 89f }
            },
            [44] = new Dictionary<int, EventLap>
            {
                [21] = new() { DriverNumber = 44, LapNumber = 21, DateStart = t0.AddSeconds(1), DurationSector1 = 24f, DurationSector2 = 24f, LapDuration = 89f }
            }
        };

        var orderPerLap = new Dictionary<int, List<int>>
        {
            [21] = [44, 16]
        };

        var candidates = ScanWorkflowService.FindCandidates(
            driverNumbers: [16],
            lapIndex: lapIndex,
            orderPerLap: orderPerLap,
            referencePaceByDriver: new Dictionary<int, double> { [16] = 90.0, [44] = 90.0 },
            safetyCarWindows: [],
            minAge: 0,
            tyreStateResolver: (_, _) => ("MEDIUM", 12));

        Assert.Empty(candidates);
    }
}
