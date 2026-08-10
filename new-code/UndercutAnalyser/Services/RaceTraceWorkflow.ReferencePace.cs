using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Derives per-driver reference pace from clean laps, excluding pit transitions and SC/VSC-affected laps.
/// </summary>
public static partial class RaceTraceWorkflowService
{
    /// <summary>
    /// Computes average clean-lap pace per driver and returns only drivers with positive derived pace.
    /// </summary>
    public static Dictionary<int, double> DeriveReferencePacePerDriver(
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<TimeWindow> safetyCarWindows)
    {
        var pitInLaps = RaceTimingDomainLogic.BuildPitInLapLookup(laps);

        return laps
            .GroupBy(l => l.DriverNumber)
            .Select(g =>
            {
                var cleanLaps = g
                    .Where(l => l.LapDuration.HasValue && l.DateStart.HasValue
                             && !l.IsPitOutLap
                             && !pitInLaps.Contains((l.DriverNumber, l.LapNumber))
                             && !RaceTimingDomainLogic.IsInAnyWindow(l, safetyCarWindows))
                    .ToList();

                var avg = cleanLaps.Count > 0
                    ? cleanLaps.Average(l => (double)l.LapDuration!.Value)
                    : 0.0;

                return (DriverNumber: g.Key, Pace: avg);
            })
            .Where(x => x.Pace > 0)
            .ToDictionary(x => x.DriverNumber, x => x.Pace);
    }
}
