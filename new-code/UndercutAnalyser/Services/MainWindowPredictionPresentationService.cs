using System.Globalization;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds render-ready prediction presentation data from model output.
/// </summary>
public static class MainWindowPredictionPresentationService
{
    /// <summary>
    /// Projects a prediction result into UI-ready classification text, G0 label text, and per-lap display rows.
    /// </summary>
    public static PredictionPresentationView Build(PredictionResult result, string attackerCode, string targetCode)
    {
        var classificationText = result.Classification switch
        {
            UndercutClassification.PredictedAhead => "UNDERCUT PREDICTED SUCCESSFUL",
            UndercutClassification.PredictedMarginal => "UNDERCUT MARGINAL",
            _ => "UNDERCUT NOT PREDICTED"
        };

        var g0Label = $"G₀ = {result.InitialGapSeconds:+0.000;-0.000;0.000} s";

        var attackerByLap = result.AttackerLaps.ToDictionary(l => l.LapNumber, l => l.CumulativePredictionTimeSeconds);
        var rows = new List<PredictionPresentationLapRow>();

        var allLaps = result.AttackerLaps.Select(l => (Lap: l, IsAttacker: true))
            .Concat(result.TargetLaps.Select(l => (Lap: l, IsAttacker: false)))
            .OrderBy(x => x.Lap.LapNumber)
            .ThenBy(x => x.IsAttacker ? 0 : 1);

        foreach (var (lap, isAttacker) in allLaps)
        {
            var breakdown = lap.Breakdown;
            string gapText = string.Empty;
            if (!isAttacker && attackerByLap.TryGetValue(lap.LapNumber, out var attackerElapsed))
            {
                var gap = result.InitialGapSeconds + attackerElapsed - lap.CumulativePredictionTimeSeconds;
                gapText = FormatGap(gap);
            }

            rows.Add(new PredictionPresentationLapRow(
                DriverLabel: isAttacker ? attackerCode : targetCode,
                LapNumber: lap.LapNumber,
                LapType: lap.IsPitLap ? "Pit" : lap.IsOutLap ? "Out" : "Normal",
                Compound: lap.Compound.ToString(),
                TyreAge: lap.TyreAgeAtStart,
                Base: breakdown.ReferencePaceSeconds.ToString("F3", CultureInfo.InvariantCulture),
                CompoundOffset: FormatTerm(breakdown.CompoundOffsetSeconds),
                Degradation: FormatTerm(breakdown.DegradationSeconds),
                WarmUp: FormatTerm(breakdown.WarmUpSeconds),
                Traffic: FormatTerm(breakdown.TrafficSeconds),
                PitLoss: FormatTerm(breakdown.PitLossSeconds),
                Total: breakdown.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture),
                Cumulative: lap.CumulativePredictionTimeSeconds.ToString("F3", CultureInfo.InvariantCulture),
                Gap: gapText));
        }

        return new PredictionPresentationView(classificationText, g0Label, rows);
    }

    private static string FormatTerm(double seconds)
    {
        return seconds switch
        {
            > 0 => $"+{seconds:0.000}",
            < 0 => $"{seconds:0.000}",
            _ => "0.000"
        };
    }

    private static string FormatGap(double gap)
    {
        return $"{gap:+0.000;-0.000;0.000} s";
    }
}

/// <summary>
/// Render-ready prediction panel data.
/// </summary>
public sealed record PredictionPresentationView(
    string ClassificationText,
    string InitialGapLabel,
    IReadOnlyList<PredictionPresentationLapRow> Rows);

/// <summary>
/// Render-ready prediction table row.
/// </summary>
public sealed record PredictionPresentationLapRow(
    string DriverLabel,
    int LapNumber,
    string LapType,
    string Compound,
    int TyreAge,
    string Base,
    string CompoundOffset,
    string Degradation,
    string WarmUp,
    string Traffic,
    string PitLoss,
    string Total,
    string Cumulative,
    string Gap);
