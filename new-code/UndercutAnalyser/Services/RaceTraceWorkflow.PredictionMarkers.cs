using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds prediction-marker selections for race-trace overlays.
/// </summary>
public static partial class RaceTraceWorkflowService
{
    public static IReadOnlyList<RaceTracePredictionMarkerSelection> ResolvePredictionMarkerSelections(
        IReadOnlyList<ScanRowData> rows,
        IReadOnlyList<Driver> drivers,
        RaceTracePredictionMarkerOptions options,
        int? selectedAttackerDriverNumber)
    {
        if (!options.ShowMarkers || rows.Count == 0)
            return [];

        var byCode = drivers
            .Where(d => !string.IsNullOrWhiteSpace(d.Code))
            .GroupBy(d => d.Code.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().DriverNumber);

        var selections = new List<RaceTracePredictionMarkerSelection>();

        foreach (var row in rows)
        {
            if (!IsResultAllowed(row.Result, options))
                continue;

            if (!TryResolveDriverNumber(row.Attacker, byCode, out var attackerDriverNumber))
                continue;

            if (options.OnlySelectedAttacker && selectedAttackerDriverNumber.HasValue && attackerDriverNumber != selectedAttackerDriverNumber.Value)
                continue;

            selections.Add(new RaceTracePredictionMarkerSelection(
                AttackerDriverNumber: attackerDriverNumber,
                DecisionLap: row.DecisionLap,
                Result: row.Result,
                AttackerToken: row.Attacker,
                TargetToken: row.Target,
                G0: row.G0,
                GapAtTargetPitLapComplete: row.GapAtTargetPitLapComplete));
        }

        return selections;
    }

    public static bool TryResolveSeriesPointAtLap(
        RaceTraceDriverSeries series,
        int decisionLap,
        out double x,
        out double y)
    {
        for (var i = 0; i < series.Xs.Count; i++)
        {
            if (Math.Abs(series.Xs[i] - decisionLap) < 0.0001)
            {
                x = series.Xs[i];
                y = series.Ys[i];
                return true;
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    public static string ResolveMarkerSymbol(string result)
    {
        return result switch
        {
            "Ahead" => "▲",
            "Marginal" => "■",
            "Behind" => "▼",
            _ => string.Empty
        };
    }

    private static bool IsResultAllowed(string result, RaceTracePredictionMarkerOptions options)
    {
        return result switch
        {
            "Ahead" => options.ShowAhead,
            "Marginal" => options.ShowMarginal,
            "Behind" => options.ShowBehind,
            _ => false
        };
    }

    private static bool TryResolveDriverNumber(
        string attackerToken,
        IReadOnlyDictionary<string, int> byCode,
        out int driverNumber)
    {
        if (int.TryParse(attackerToken, out driverNumber))
            return true;

        var key = attackerToken.Trim().ToUpperInvariant();
        if (byCode.TryGetValue(key, out driverNumber))
            return true;

        driverNumber = 0;
        return false;
    }
}

public sealed record RaceTracePredictionMarkerOptions(
    bool ShowMarkers,
    bool ShowGuides,
    bool OnlySelectedAttacker,
    bool ShowAhead,
    bool ShowMarginal,
    bool ShowBehind);

public sealed record RaceTracePredictionMarkerSelection(
    int AttackerDriverNumber,
    int DecisionLap,
    string Result,
    string AttackerToken,
    string TargetToken,
    string G0,
    string GapAtTargetPitLapComplete);
