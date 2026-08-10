using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

public static partial class WorkspaceWorkflowService
{
    /// <summary>
    /// Decides whether a scan row should be visible based on its result class and the three UI filter toggles.
    /// Unknown/non-standard result values are left visible so users can still see warnings and errors.
    /// </summary>
    public static bool ShouldShowResult(string? result, bool? showAhead, bool? showMarginal, bool? showBehind)
    {
        if (string.Equals(result, "Ahead", StringComparison.OrdinalIgnoreCase))
            return showAhead == true;

        if (string.Equals(result, "Marginal", StringComparison.OrdinalIgnoreCase))
            return showMarginal == true;

        if (string.Equals(result, "Behind", StringComparison.OrdinalIgnoreCase))
            return showBehind == true;

        return true;
    }

    /// <summary>
    /// Counts rows considered undercut opportunities (Ahead or Marginal) for status text and quick KPI display.
    /// </summary>
    public static int CountOpportunities(IEnumerable<string?> results)
    {
        return results.Count(r =>
            string.Equals(r, "Ahead", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Marginal", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds the status line shown under the full-scan table, including visible rows, total scanned rows, and opportunities found.
    /// </summary>
    public static string BuildMainScanStatusText(int shownCount, int scannedCount, int opportunityCount)
    {
        return $"{shownCount} shown ({scannedCount} scanned) — {opportunityCount} opportunit{(opportunityCount == 1 ? "y" : "ies")} found";
    }

    /// <summary>
    /// Builds the status line shown under the single-scan table, preserving the caller's prefix message and appending visibility/opportunity metrics.
    /// </summary>
    public static string BuildSingleScanStatusText(string summaryPrefix, int shownCount, int opportunityCount)
    {
        return $"{summaryPrefix} — {shownCount} shown, {opportunityCount} opportunit{(opportunityCount == 1 ? "y" : "ies")} found";
    }

    /// <summary>
    /// Resolves tyre compound and tyre age for a driver at a specific lap using active stint data,
    /// then recent-stint fallback, then pit-out fallback if no stint data exists.
    /// </summary>
    public static (string compound, int age) ResolveTyreStateAtLap(
        IReadOnlyList<EventStint> stints,
        IReadOnlyList<EventLap> laps,
        int driverNumber,
        int lapNumber)
    {
        var stint = stints
            .Where(s => s.DriverNumber == driverNumber
                     && s.LapStart <= lapNumber
                     && (s.LapEnd == null || s.LapEnd >= lapNumber))
            .OrderByDescending(s => s.StintNumber)
            .FirstOrDefault();

        if (stint is not null)
        {
            var age = Math.Max(0, lapNumber - stint.LapStart + stint.TyreAgeAtStart);
            return (NormalizeCompoundText(stint.Compound), age);
        }

        var mostRecentKnownStint = stints
            .Where(s => s.DriverNumber == driverNumber && s.LapStart <= lapNumber)
            .OrderByDescending(s => s.LapStart)
            .ThenByDescending(s => s.StintNumber)
            .FirstOrDefault();

        if (mostRecentKnownStint is not null)
        {
            var fallbackAge = Math.Max(0, lapNumber - mostRecentKnownStint.LapStart + mostRecentKnownStint.TyreAgeAtStart);
            return (NormalizeCompoundText(mostRecentKnownStint.Compound), fallbackAge);
        }

        var lastPitOutLap = laps
            .Where(l => l.DriverNumber == driverNumber
                     && l.LapNumber <= lapNumber
                     && l.IsPitOutLap)
            .Select(l => l.LapNumber)
            .DefaultIfEmpty(1)
            .Max();

        return ("UNKNOWN", Math.Max(0, lapNumber - lastPitOutLap));
    }

    /// <summary>
    /// Escapes a single CSV field by applying quote wrapping and quote-doubling when needed.
    /// </summary>
    public static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Normalizes tyre compound text into canonical upper-case labels used by the UI and exports.
    /// Unrecognized values are passed through in upper-case, and blank values become UNKNOWN.
    /// </summary>
    public static string NormalizeCompoundText(string? compoundText)
    {
        return TyreCompoundParser.FromOpenF1String(compoundText) switch
        {
            TyreCompound.Soft => "SOFT",
            TyreCompound.Medium => "MEDIUM",
            TyreCompound.Hard => "HARD",
            TyreCompound.Intermediate => "INTERMEDIATE",
            TyreCompound.Wet => "WET",
            _ => string.IsNullOrWhiteSpace(compoundText)
                ? "UNKNOWN"
                : compoundText.Trim().ToUpperInvariant()
        };
    }
}
