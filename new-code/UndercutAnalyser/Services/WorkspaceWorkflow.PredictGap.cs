using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Derives attacker-target initial gap at a decision lap using Sector-2 crossing timestamps.
/// </summary>
public static partial class WorkspaceWorkflowService
{
    /// <summary>
    /// Returns the signed initial gap in seconds at the decision lap, or null when required lap timing data is unavailable.
    /// Positive means attacker is behind the target at the decision marker.
    /// </summary>
    public static double? TryDeriveInitialGapSeconds(
        IReadOnlyList<EventLap> laps,
        int attackerDriverNumber,
        int targetDriverNumber,
        int decisionLapNumber)
    {
        var attackerLap = laps.FirstOrDefault(l =>
            l.DriverNumber == attackerDriverNumber &&
            l.LapNumber == decisionLapNumber &&
            l.DateStart.HasValue &&
            l.LapTimeAtSectorTwoLine.HasValue);

        var targetLap = laps.FirstOrDefault(l =>
            l.DriverNumber == targetDriverNumber &&
            l.LapNumber == decisionLapNumber &&
            l.DateStart.HasValue &&
            l.LapTimeAtSectorTwoLine.HasValue);

        if (attackerLap is null || targetLap is null)
            return null;

        var attackerSectorTwoLine = RaceTimingDomainLogic.AsUtc(attackerLap.DateStart!.Value).AddSeconds(attackerLap.LapTimeAtSectorTwoLine!.Value);
        var targetSectorTwoLine = RaceTimingDomainLogic.AsUtc(targetLap.DateStart!.Value).AddSeconds(targetLap.LapTimeAtSectorTwoLine!.Value);
        return (attackerSectorTwoLine - targetSectorTwoLine).TotalSeconds;
    }
}
