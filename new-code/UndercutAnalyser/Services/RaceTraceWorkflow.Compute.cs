using System.Globalization;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Computes render-ready race-trace series and metadata from session data and trace options.
/// </summary>
public static partial class RaceTraceWorkflowService
{
    /// <summary>
    /// Builds race-trace render data including filtered/adjusted laps, per-driver cumulative deltas,
    /// ordering, line style decisions, and visibility-state normalization.
    /// </summary>
    public static RaceTraceComputationResult Compute(
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<Driver> drivers,
        IReadOnlyList<RaceControlMessage> raceControlMessages,
        RaceTraceComputationOptions options,
        IReadOnlyDictionary<int, bool> traceVisibilityByDriver)
    {
        if (laps.Count == 0)
        {
            return new RaceTraceComputationResult(
                HasRenderableData: false,
                ConstantReferenceSeconds: null,
                Series: [],
                NormalizedVisibilityByDriver: new Dictionary<int, bool>());
        }

        var safetyCarWindows = RaceTimingDomainLogic.BuildSafetyCarWindows(raceControlMessages);
        var maxSessionLapNumber = laps.Max(l => l.LapNumber);
        var fuelEffectPerLapSeconds = options.ApplyFuelCorrection && maxSessionLapNumber > 0
            ? options.FuelSecondsPer10Kg * (options.FuelKg / 10.0) / maxSessionLapNumber
            : 0.0;

        var pitInLaps = RaceTimingDomainLogic.BuildPitInLapLookup(laps);

        var displayEligibleLaps = laps
            .Where(l => l.LapDuration.HasValue && l.DateStart.HasValue)
            .Where(l => IsLapEligible(l, options.IncludePitLaps, options.IncludeScVscLaps, safetyCarWindows, pitInLaps))
            .Select(l => new
            {
                Lap = l,
                AdjustedLapSeconds = l.LapDuration!.Value - (fuelEffectPerLapSeconds * Math.Max(0, maxSessionLapNumber - l.LapNumber))
            })
            .ToList();

        if (displayEligibleLaps.Count == 0)
        {
            return new RaceTraceComputationResult(
                HasRenderableData: false,
                ConstantReferenceSeconds: null,
                Series: [],
                NormalizedVisibilityByDriver: new Dictionary<int, bool>());
        }

        var referenceForTrace = ReferenceLapTimeCalculator.Calculate(
            laps,
            raceControlMessages,
            options.ApplyFuelCorrection ? options.FuelSecondsPer10Kg : 0.0,
            options.ApplyFuelCorrection ? options.FuelKg : 0.0);

        if (!referenceForTrace.AverageLapTimeSeconds.HasValue || referenceForTrace.AverageLapTimeSeconds.Value <= 0)
        {
            return new RaceTraceComputationResult(
                HasRenderableData: false,
                ConstantReferenceSeconds: null,
                Series: [],
                NormalizedVisibilityByDriver: new Dictionary<int, bool>());
        }

        var constantReference = referenceForTrace.AverageLapTimeSeconds.Value;

        var driverByNumber = drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var lapsByDriver = displayEligibleLaps
            .GroupBy(x => x.Lap.DriverNumber)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Lap.LapNumber).ToList());

        var solidByDriver = lapsByDriver.Keys.ToDictionary(
            driverNumber => driverNumber,
            driverNumber => ShouldUseSolidLineForDriver(driverNumber, lapsByDriver.Keys, driverByNumber));

        var orderedDriverSeries = options.SortLegendByTeamThenNumber
            ? lapsByDriver
                .OrderBy(x => GetTeamSortKey(x.Key, driverByNumber))
                .ThenBy(x => x.Key)
            : lapsByDriver
                .OrderBy(x => x.Key);

        var orderedDriverSeriesList = orderedDriverSeries.ToList();

        var activeDriverNumbers = orderedDriverSeriesList.Select(x => x.Key).ToHashSet();
        var normalizedVisibility = traceVisibilityByDriver
            .Where(kvp => activeDriverNumbers.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var series = new List<RaceTraceDriverSeries>(orderedDriverSeriesList.Count);

        foreach (var kvp in orderedDriverSeriesList)
        {
            var driverNumber = kvp.Key;
            var driverLaps = kvp.Value;

            var xs = new List<double>(driverLaps.Count);
            var ys = new List<double>(driverLaps.Count);

            var cumulativeDelta = 0.0;
            foreach (var item in driverLaps)
            {
                var lapNumber = item.Lap.LapNumber;
                var lapDelta = constantReference - item.AdjustedLapSeconds;
                cumulativeDelta += lapDelta;

                xs.Add(lapNumber);
                ys.Add(cumulativeDelta);
            }

            if (xs.Count == 0)
                continue;

            var driverName = driverByNumber.TryGetValue(driverNumber, out var driver)
                ? BuildLegendDriverName(driver, driverNumber)
                : driverNumber.ToString(CultureInfo.InvariantCulture);

            var teamColour = driverByNumber.TryGetValue(driverNumber, out var d) ? d.TeamColour : string.Empty;
            var isSolid = solidByDriver[driverNumber];
            var isVisible = !normalizedVisibility.TryGetValue(driverNumber, out var storedVisible) || storedVisible;
            normalizedVisibility[driverNumber] = isVisible;

            series.Add(new RaceTraceDriverSeries(
                DriverNumber: driverNumber,
                DriverName: driverName,
                TeamColour: teamColour,
                IsSolidLine: isSolid,
                IsVisible: isVisible,
                Xs: xs,
                Ys: ys));
        }

        return new RaceTraceComputationResult(
            HasRenderableData: series.Count > 0,
            ConstantReferenceSeconds: constantReference,
            Series: series,
            NormalizedVisibilityByDriver: normalizedVisibility);
    }

    /// <summary>
    /// Returns whether a lap passes current plotting filters (pit-lap and SC/VSC inclusion).
    /// </summary>
    private static bool IsLapEligible(
        EventLap lap,
        bool includePitLaps,
        bool includeScVscLaps,
        IReadOnlyList<TimeWindow> safetyCarWindows,
        HashSet<(int DriverNumber, int LapNumber)> pitInLaps)
    {
        if (!includePitLaps)
        {
            if (lap.IsPitOutLap || pitInLaps.Contains((lap.DriverNumber, lap.LapNumber)))
            {
                return false;
            }
        }

        if (!includeScVscLaps && lap.DateStart.HasValue && lap.LapDuration.HasValue)
        {
            var lapStartUtc = RaceTimingDomainLogic.AsUtc(lap.DateStart.Value);
            var lapEndUtc = lapStartUtc.AddSeconds(lap.LapDuration.Value);
            if (RaceTimingDomainLogic.IsInAnyWindow(lapStartUtc, lapEndUtc, safetyCarWindows))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Chooses line style so teammates can be distinguished: one solid, one dashed.
    /// </summary>
    private static bool ShouldUseSolidLineForDriver(
        int driverNumber,
        IEnumerable<int> plottedDriverNumbers,
        IReadOnlyDictionary<int, Driver> driverByNumber)
    {
        if (!driverByNumber.TryGetValue(driverNumber, out var currentDriver))
        {
            return true;
        }

        var groupKey = GetTeamGroupKey(currentDriver, driverNumber);

        var teammateNumbers = plottedDriverNumbers
            .Where(n => driverByNumber.TryGetValue(n, out var d) && GetTeamGroupKey(d, n) == groupKey)
            .ToArray();

        if (teammateNumbers.Length <= 1)
        {
            return true;
        }

        return driverNumber == teammateNumbers.Min();
    }

    /// <summary>
    /// Builds a stable grouping key for teammate detection (team name, then colour fallback).
    /// </summary>
    private static string GetTeamGroupKey(Driver driver, int driverNumber)
    {
        if (!string.IsNullOrWhiteSpace(driver.TeamName))
        {
            return "team:" + driver.TeamName.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(driver.TeamColour))
        {
            return "colour:" + driver.TeamColour.Trim().ToUpperInvariant();
        }

        return "driver:" + driverNumber.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Provides a legend sort key that clusters drivers by team where possible.
    /// </summary>
    private static string GetTeamSortKey(int driverNumber, IReadOnlyDictionary<int, Driver> driverByNumber)
    {
        if (!driverByNumber.TryGetValue(driverNumber, out var driver))
        {
            return "ZZZ";
        }

        if (!string.IsNullOrWhiteSpace(driver.TeamName))
        {
            return driver.TeamName.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(driver.TeamColour))
        {
            return driver.TeamColour.Trim().ToUpperInvariant();
        }

        return "ZZZ";
    }

    /// <summary>
    /// Builds a readable legend label combining driver code and broadcast name.
    /// </summary>
    private static string BuildLegendDriverName(Driver driver, int driverNumber)
    {
        var code = string.IsNullOrWhiteSpace(driver.Code)
            ? driverNumber.ToString(CultureInfo.InvariantCulture)
            : driver.Code;

        return string.IsNullOrWhiteSpace(driver.BroadcastName)
            ? code
            : $"{code} ({driver.BroadcastName})";
    }

    /// <summary>
    /// Parses a ScottPlot colour from team colour hex, with deterministic fallback colours.
    /// </summary>
    public static ScottPlot.Color ParseScottPlotColor(string hex, int fallbackSeed)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            var clean = hex.Trim();
            if (!clean.StartsWith("#", StringComparison.Ordinal))
            {
                clean = "#" + clean;
            }

            var hexDigits = clean[1..];
            var isSupportedHexLength = hexDigits.Length is 3 or 4 or 6 or 8;
            var isHex = isSupportedHexLength && hexDigits.All(Uri.IsHexDigit);
            if (isHex)
            {
                try
                {
                    return ScottPlot.Color.FromHex(clean);
                }
                catch
                {
                    // fallback below
                }
            }
        }

        var fallback = new[]
        {
            ScottPlot.Colors.Blue,
            ScottPlot.Colors.Red,
            ScottPlot.Colors.Green,
            ScottPlot.Colors.Orange,
            ScottPlot.Colors.Purple,
            ScottPlot.Colors.Brown,
            ScottPlot.Colors.Teal,
            ScottPlot.Colors.Magenta
        };

        return fallback[Math.Abs(fallbackSeed) % fallback.Length];
    }

    /// <summary>
    /// Parses a WPF brush from team colour hex, with deterministic fallback colours.
    /// </summary>
    public static System.Windows.Media.Brush ParseLegendBrush(string hex, int fallbackSeed)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            var clean = hex.Trim();
            if (!clean.StartsWith("#", StringComparison.Ordinal))
            {
                clean = "#" + clean;
            }

            try
            {
                var parsed = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(clean);
                return new System.Windows.Media.SolidColorBrush(parsed);
            }
            catch
            {
                // fallback below
            }
        }

        var fallback = new[]
        {
            System.Windows.Media.Colors.Blue,
            System.Windows.Media.Colors.Red,
            System.Windows.Media.Colors.Green,
            System.Windows.Media.Colors.Orange,
            System.Windows.Media.Colors.Purple,
            System.Windows.Media.Colors.Brown,
            System.Windows.Media.Colors.Teal,
            System.Windows.Media.Colors.Magenta
        };

        return new System.Windows.Media.SolidColorBrush(fallback[Math.Abs(fallbackSeed) % fallback.Length]);
    }
}

/// <summary>
/// Input options used to compute race-trace render series.
/// </summary>
public sealed record RaceTraceComputationOptions(
    bool IncludePitLaps,
    bool IncludeScVscLaps,
    bool ApplyFuelCorrection,
    double FuelSecondsPer10Kg,
    double FuelKg,
    bool SortLegendByTeamThenNumber);

/// <summary>
/// Computed line-series payload for one driver in the race trace.
/// </summary>
public sealed record RaceTraceDriverSeries(
    int DriverNumber,
    string DriverName,
    string TeamColour,
    bool IsSolidLine,
    bool IsVisible,
    IReadOnlyList<double> Xs,
    IReadOnlyList<double> Ys);

/// <summary>
/// Result of race-trace computation, including ordered series and normalized visibility state.
/// </summary>
public sealed record RaceTraceComputationResult(
    bool HasRenderableData,
    double? ConstantReferenceSeconds,
    IReadOnlyList<RaceTraceDriverSeries> Series,
    IReadOnlyDictionary<int, bool> NormalizedVisibilityByDriver);
