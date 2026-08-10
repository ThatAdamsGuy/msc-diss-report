using System.Globalization;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds selector options and default choices used by MainWindow scan and prediction panels.
/// </summary>
public static class MainWindowSelectorPopulationService
{
    /// <summary>
    /// Builds ordered driver selector items from currently loaded participants and laps.
    /// </summary>
    public static List<MainWindowPredictionSelection> BuildOrderedDriverSelections(
        IReadOnlyList<Driver> drivers,
        IReadOnlyList<EventLap> laps)
    {
        var driversByNumber = drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        return laps
            .Select(l => l.DriverNumber)
            .Distinct()
            .Where(driversByNumber.ContainsKey)
            .OrderBy(n => n)
            .Select(n =>
            {
                var d = driversByNumber[n];
                var code = string.IsNullOrWhiteSpace(d.Code) ? n.ToString(CultureInfo.InvariantCulture) : d.Code;
                var display = string.IsNullOrWhiteSpace(d.BroadcastName) ? code : $"{code} – {d.BroadcastName}";
                return new MainWindowPredictionSelection(n, code, display);
            })
            .ToList();
    }

    /// <summary>
    /// Builds available target decision laps for the selected single-scan target.
    /// </summary>
    public static List<int> BuildTargetDecisionLapChoices(
        IReadOnlyList<EventLap> laps,
        int? targetDriverNumber)
    {
        if (!targetDriverNumber.HasValue)
            return [];

        return laps
            .Where(l => l.DriverNumber == targetDriverNumber.Value && l.DateStart.HasValue && l.LapTimeAtSectorTwoLine.HasValue)
            .Select(l => l.LapNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Chooses a selected decision lap, preferring a previous selection when available.
    /// </summary>
    public static int? ChooseDecisionLap(IReadOnlyList<int> availableLaps, int? preferredLap)
    {
        if (availableLaps.Count == 0)
            return null;

        if (preferredLap.HasValue && availableLaps.Contains(preferredLap.Value))
            return preferredLap.Value;

        return availableLaps[0];
    }

    /// <summary>
    /// Builds prediction decision-lap choices and a default selected lap.
    /// </summary>
    public static (List<int> Laps, int? DefaultLap) BuildPredictDecisionLapChoices(IReadOnlyList<EventLap> laps)
    {
        var maxLap = laps.Count > 0 ? laps.Max(l => l.LapNumber) : 0;

        if (maxLap <= 0)
            return ([], null);

        var lapChoices = Enumerable.Range(1, maxLap).ToList();
        var defaultIndex = Math.Min(Math.Max(0, maxLap / 2 - 1), lapChoices.Count - 1);
        return (lapChoices, lapChoices[defaultIndex]);
    }
}
