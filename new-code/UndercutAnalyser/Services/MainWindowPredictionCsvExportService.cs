using System.Globalization;
using System.Text;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds deterministic CSV text for exported manual prediction results.
/// </summary>
public static class MainWindowPredictionCsvExportService
{
    /// <summary>
    /// Creates CSV content for a prediction result and selected event display name.
    /// </summary>
    public static string BuildCsv(PredictionResult result, string eventDisplayName)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine("# Prediction result");
        sb.AppendLine($"# Event,{eventDisplayName}");
        sb.AppendLine($"# Classification,{result.Classification}");
        sb.AppendLine();
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"InitialGapSeconds,{result.InitialGapSeconds.ToString("F3", inv)}");
        sb.AppendLine($"GapAtTargetPitLapCompleteSeconds,{result.GapAtTargetPitLapCompleteSeconds.ToString("F3", inv)}");
        sb.AppendLine($"GapAtTargetOutLapCompleteSeconds,{result.GapAtTargetOutLapCompleteSeconds.ToString("F3", inv)}");
        sb.AppendLine($"GapAtBothDriversNormalLapCompleteSeconds,{result.GapAtBothDriversNormalLapCompleteSeconds.ToString("F3", inv)}");

        return sb.ToString();
    }
}
