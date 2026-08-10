using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictionCsvExportServiceTests
{
    [Fact]
    public void BuildCsv_UsesExpectedHeaderAndMetricRows()
    {
        var result = new PredictionResult(
            AttackerLaps: [],
            TargetLaps: [],
            InitialGapSeconds: 1.23456,
            GapAtTargetPitLapCompleteSeconds: 0.78901,
            GapAtTargetOutLapCompleteSeconds: 0.45678,
            GapAtBothDriversNormalLapCompleteSeconds: 0.12345,
            DeltaGAtTargetPitLapCompleteSeconds: -0.445,
            DeltaGAtTargetOutLapCompleteSeconds: -0.556,
            DeltaGAtBothDriversNormalLapCompleteSeconds: -0.667,
            Classification: UndercutClassification.PredictedMarginal,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);

        var csv = MainWindowPredictionCsvExportService.BuildCsv(result, "Australian GP (2025)");

        Assert.Contains("# Prediction result", csv);
        Assert.Contains("# Event,Australian GP (2025)", csv);
        Assert.Contains("# Classification,PredictedMarginal", csv);
        Assert.Contains("Metric,Value", csv);
        Assert.Contains("InitialGapSeconds,1.235", csv);
        Assert.Contains("GapAtTargetPitLapCompleteSeconds,0.789", csv);
        Assert.Contains("GapAtTargetOutLapCompleteSeconds,0.457", csv);
        Assert.Contains("GapAtBothDriversNormalLapCompleteSeconds,0.123", csv);
    }
}
