using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Canonical race-timing domain helpers shared across scan, reference-lap, and UI orchestration flows.
/// </summary>
public static class RaceTimingDomainLogic
{
    /// <summary>
    /// Normalizes DateTime values to UTC so all timing comparisons use a single clock basis.
    /// </summary>
    public static DateTime AsUtc(DateTime input)
    {
        return input.Kind switch
        {
            DateTimeKind.Utc => input,
            DateTimeKind.Local => input.ToUniversalTime(),
            _ => DateTime.SpecifyKind(input, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Converts SC/VSC race-control messages into UTC active windows for lap filtering.
    /// </summary>
    public static IReadOnlyList<TimeWindow> BuildSafetyCarWindows(IReadOnlyList<RaceControlMessage> raceControlMessages)
    {
        var windows = new List<TimeWindow>();
        DateTime? vscStart = null;
        DateTime? scStart = null;

        foreach (var message in raceControlMessages
                     .Where(m => string.Equals(m.Category, "SafetyCar", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(m.Category, "Safety Car", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.Date))
        {
            if (!message.Date.HasValue)
            {
                continue;
            }

            var timestamp = AsUtc(message.Date.Value);
            var text = (message.Message ?? string.Empty).Trim().ToUpperInvariant();

            if (text.Contains("VSC DEPLOYED", StringComparison.Ordinal))
            {
                vscStart = timestamp;
                continue;
            }

            if (text.Contains("VSC ENDING", StringComparison.Ordinal))
            {
                if (vscStart.HasValue)
                {
                    windows.Add(new TimeWindow(vscStart.Value, timestamp));
                    vscStart = null;
                }

                continue;
            }

            if (text.Contains("SAFETY CAR DEPLOYED", StringComparison.Ordinal))
            {
                scStart = timestamp;
                continue;
            }

            if (text.Contains("SAFETY CAR IN THIS LAP", StringComparison.Ordinal) && scStart.HasValue)
            {
                windows.Add(new TimeWindow(scStart.Value, timestamp));
                scStart = null;
            }
        }

        var maxKnownDate = raceControlMessages
            .Where(m => m.Date.HasValue)
            .Select(m => AsUtc(m.Date!.Value))
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        if (vscStart.HasValue)
        {
            windows.Add(new TimeWindow(vscStart.Value, maxKnownDate));
        }

        if (scStart.HasValue)
        {
            windows.Add(new TimeWindow(scStart.Value, maxKnownDate));
        }

        return windows;
    }

    /// <summary>
    /// Converts SC/VSC race-control messages into inclusive affected lap numbers.
    /// </summary>
    public static IReadOnlySet<int> BuildSafetyCarLapNumbers(IReadOnlyList<RaceControlMessage> raceControlMessages)
    {
        var lapNumbers = new HashSet<int>();
        int? vscStartLap = null;
        int? scStartLap = null;

        foreach (var message in raceControlMessages
                     .Where(m => string.Equals(m.Category, "SafetyCar", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(m.Category, "Safety Car", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.Date)
                     .ThenBy(m => m.LapNumber ?? int.MaxValue))
        {
            var text = (message.Message ?? string.Empty).Trim().ToUpperInvariant();
            var lapNumber = message.LapNumber;

            if (text.Contains("VSC DEPLOYED", StringComparison.Ordinal))
            {
                vscStartLap = lapNumber;
                continue;
            }

            if (text.Contains("VSC ENDING", StringComparison.Ordinal))
            {
                AddLapRange(lapNumbers, vscStartLap, lapNumber);
                vscStartLap = null;
                continue;
            }

            if (text.Contains("SAFETY CAR DEPLOYED", StringComparison.Ordinal))
            {
                scStartLap = lapNumber;
                continue;
            }

            if (text.Contains("SAFETY CAR IN THIS LAP", StringComparison.Ordinal))
            {
                AddLapRange(lapNumbers, scStartLap, lapNumber);
                scStartLap = null;
            }
        }

        if (raceControlMessages.Count == 0)
        {
            return lapNumbers;
        }

        var windows = BuildSafetyCarWindows(raceControlMessages);
        if (windows.Count == 0)
        {
            return lapNumbers;
        }

        var datedScMessages = raceControlMessages
            .Where(m => (string.Equals(m.Category, "SafetyCar", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(m.Category, "Safety Car", StringComparison.OrdinalIgnoreCase))
                     && m.Date.HasValue
                     && m.LapNumber.HasValue)
            .OrderBy(m => m.Date)
            .ToList();

        if (datedScMessages.Count == 0)
        {
            return lapNumbers;
        }

        foreach (var window in windows)
        {
            var startLap = datedScMessages
                .Where(m => AsUtc(m.Date!.Value) <= window.StartUtc)
                .OrderByDescending(m => m.Date)
                .Select(m => m.LapNumber!.Value)
                .FirstOrDefault();

            var endLap = datedScMessages
                .Where(m => AsUtc(m.Date!.Value) >= window.EndUtc)
                .OrderBy(m => m.Date)
                .Select(m => m.LapNumber!.Value)
                .FirstOrDefault();

            if (startLap > 0 && endLap > 0)
            {
                AddLapRange(lapNumbers, startLap, endLap);
            }
        }

        return lapNumbers;
    }

    private static void AddLapRange(HashSet<int> lapNumbers, int? startLap, int? endLap)
    {
        if (!startLap.HasValue || !endLap.HasValue)
        {
            return;
        }

        var from = Math.Min(startLap.Value, endLap.Value);
        var to = Math.Max(startLap.Value, endLap.Value);

        for (var lap = from; lap <= to; lap++)
        {
            lapNumbers.Add(lap);
        }
    }

    /// <summary>
    /// Builds inferred pit-in laps from pit-out markers (pit-in = prior lap for same driver).
    /// </summary>
    public static HashSet<(int DriverNumber, int LapNumber)> BuildPitInLapLookup(IReadOnlyList<EventLap> laps)
    {
        var lookup = new HashSet<(int DriverNumber, int LapNumber)>();

        foreach (var pitOutLap in laps.Where(l => l.IsPitOutLap && l.LapNumber > 1))
        {
            lookup.Add((pitOutLap.DriverNumber, pitOutLap.LapNumber - 1));
        }

        return lookup;
    }

    /// <summary>
    /// Returns true when a lap interval overlaps any supplied SC/VSC window.
    /// </summary>
    public static bool IsInAnyWindow(DateTime lapStartUtc, DateTime lapEndUtc, IReadOnlyList<TimeWindow> windows)
    {
        foreach (var window in windows)
        {
            if (lapStartUtc <= window.EndUtc && lapEndUtc >= window.StartUtc)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when an EventLap overlaps any supplied SC/VSC window.
    /// </summary>
    public static bool IsInAnyWindow(EventLap lap, IReadOnlyList<TimeWindow> windows)
    {
        if (!lap.DateStart.HasValue || !lap.LapDuration.HasValue)
            return false;

        var lapStartUtc = AsUtc(lap.DateStart.Value);
        var lapEndUtc = lapStartUtc.AddSeconds(lap.LapDuration.Value);
        return IsInAnyWindow(lapStartUtc, lapEndUtc, windows);
    }
}
