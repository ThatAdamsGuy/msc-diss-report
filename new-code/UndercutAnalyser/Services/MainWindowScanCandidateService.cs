using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// One potential undercut scenario candidate identified from lap ordering and eligibility checks.
/// </summary>
public sealed record ScanCandidate(
    int AttackerNumber,
    int TargetNumber,
    int DecisionLap,
    int AttackerPosition,
    double InitialGapSeconds);

public static class MainWindowScanCandidateService
{
    /// <summary>
    /// Builds a driver/lap lookup table containing only laps with timing data needed for scan candidate evaluation.
    /// </summary>
    public static Dictionary<int, Dictionary<int, EventLap>> BuildLapIndex(IReadOnlyList<EventLap> laps)
    {
        return laps
            .GroupBy(l => l.DriverNumber)
            .ToDictionary(g => g.Key,
                g => g.Where(l => l.DateStart.HasValue && l.LapDuration.HasValue)
                      .ToDictionary(l => l.LapNumber));
    }

    /// <summary>
    /// Derives on-track order per lap from Sector-2 crossing timestamps, earliest crossing first.
    /// </summary>
    public static Dictionary<int, List<int>> BuildOnTrackOrderPerLap(Dictionary<int, Dictionary<int, EventLap>> lapIndex)
    {
        var allLapNumbers = lapIndex.Values
            .SelectMany(d => d.Keys)
            .Distinct()
            .Order();

        var result = new Dictionary<int, List<int>>();

        foreach (var lapNum in allLapNumbers)
        {
            var sectorTwoCrossings = new List<(int driverNumber, DateTime crossing)>();
            foreach (var (driverNum, lapMap) in lapIndex)
            {
                if (!lapMap.TryGetValue(lapNum, out var lap)) continue;
                if (!lap.DateStart.HasValue || !lap.LapTimeAtSectorTwoLine.HasValue) continue;

                var crossing = AsUtc(lap.DateStart.Value).AddSeconds(lap.LapTimeAtSectorTwoLine.Value);
                sectorTwoCrossings.Add((driverNum, crossing));
            }

            result[lapNum] = sectorTwoCrossings
                .OrderBy(x => x.crossing)
                .Select(x => x.driverNumber)
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// Returns true when any part of a lap overlaps a safety-car or virtual-safety-car time window.
    /// </summary>
    public static bool IsInAnySafetyCarWindow(EventLap lap, IReadOnlyList<TimeWindow> windows)
    {
        return RaceTimingDomainLogic.IsInAnyWindow(lap, windows);
    }

    /// <summary>
    /// Finds valid scan candidates where the attacker is directly behind a target at the decision marker
    /// and all configured eligibility checks pass (pace, lap validity, tyre age, no SC/VSC overlap, positive initial gap).
    /// </summary>
    public static List<ScanCandidate> FindCandidates(
        IReadOnlyList<int> driverNumbers,
        Dictionary<int, Dictionary<int, EventLap>> lapIndex,
        Dictionary<int, List<int>> orderPerLap,
        Dictionary<int, double> referencePaceByDriver,
        IReadOnlyList<TimeWindow> safetyCarWindows,
        int minAge,
        Func<int, int, (string compound, int age)> tyreStateResolver)
    {
        var results = new List<ScanCandidate>();

        foreach (var attackerNumber in driverNumbers)
        {
            if (!lapIndex.TryGetValue(attackerNumber, out var attackerLapMap)) continue;

            var attackerRefPace = referencePaceByDriver.GetValueOrDefault(attackerNumber, 0.0);
            if (attackerRefPace <= 0) continue;

            foreach (var lapNumber in attackerLapMap.Keys.Order())
            {
                var attackerLap = attackerLapMap[lapNumber];
                if (!attackerLap.DateStart.HasValue || !attackerLap.LapDuration.HasValue) continue;
                if (attackerLap.IsPitOutLap) continue;
                if (IsInAnySafetyCarWindow(attackerLap, safetyCarWindows)) continue;

                var (_, attackerTyreAge) = tyreStateResolver(attackerNumber, lapNumber);
                if (attackerTyreAge < minAge) continue;

                if (!orderPerLap.TryGetValue(lapNumber, out var order)) continue;
                var attackerPos = order.IndexOf(attackerNumber);
                if (attackerPos <= 0) continue;

                var targetNumber = order[attackerPos - 1];
                if (!lapIndex.TryGetValue(targetNumber, out var targetLapMap)) continue;

                if (!targetLapMap.TryGetValue(lapNumber, out var targetDecisionLap)) continue;
                if (targetDecisionLap.IsPitOutLap) continue;
                if (IsInAnySafetyCarWindow(targetDecisionLap, safetyCarWindows)) continue;

                var targetRefPace = referencePaceByDriver.GetValueOrDefault(targetNumber, 0.0);
                if (targetRefPace <= 0) continue;

                if (!attackerLap.LapTimeAtSectorTwoLine.HasValue || !targetDecisionLap.LapTimeAtSectorTwoLine.HasValue)
                    continue;

                var attackerSectorTwoLine = AsUtc(attackerLap.DateStart.Value).AddSeconds(attackerLap.LapTimeAtSectorTwoLine.Value);
                var targetSectorTwoLine = AsUtc(targetDecisionLap.DateStart.Value).AddSeconds(targetDecisionLap.LapTimeAtSectorTwoLine.Value);
                var g0 = (attackerSectorTwoLine - targetSectorTwoLine).TotalSeconds;

                if (g0 <= 0) continue;

                results.Add(new ScanCandidate(
                    AttackerNumber: attackerNumber,
                    TargetNumber: targetNumber,
                    DecisionLap: lapNumber,
                    AttackerPosition: attackerPos,
                    InitialGapSeconds: g0));
            }
        }

        return results;
    }

    /// <summary>
    /// Normalizes a DateTime value to UTC so timing comparisons use a consistent clock basis.
    /// </summary>
    private static DateTime AsUtc(DateTime input)
    {
        return RaceTimingDomainLogic.AsUtc(input);
    }
}
