using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictionPresentationServiceTests
{
    [Fact]
    public void Build_MapsClassificationAndRows_WithFormattedTermsAndGap()
    {
        var result = CreatePredictionResult(UndercutClassification.PredictedAhead);

        var view = MainWindowPredictionPresentationService.Build(result, attackerCode: "NOR", targetCode: "PIA");

        Assert.Equal("UNDERCUT PREDICTED SUCCESSFUL", view.ClassificationText);
        Assert.Equal("G₀ = +1.000 s", view.InitialGapLabel);
        Assert.Equal(2, view.Rows.Count);

        var attackerRow = view.Rows[0];
        Assert.Equal("NOR", attackerRow.DriverLabel);
        Assert.Equal("Pit", attackerRow.LapType);
        Assert.Equal("+0.100", attackerRow.CompoundOffset);
        Assert.Equal("", attackerRow.Gap);

        var targetRow = view.Rows[1];
        Assert.Equal("PIA", targetRow.DriverLabel);
        Assert.Equal("Normal", targetRow.LapType);
        Assert.Equal("-0.010", targetRow.Traffic);
        Assert.Equal("+1.040 s", targetRow.Gap);
    }

    private static PredictionResult CreatePredictionResult(UndercutClassification classification)
    {
        var attackerLap = new PredictedLap(
            DriverCode: "NOR",
            LapNumber: 12,
            IsPitLap: true,
            IsOutLap: false,
            Compound: TyreCompound.Soft,
            TyreAgeAtStart: 8,
            Breakdown: new LapPredictionBreakdown(90.0, 0.1, 0.2, 0.0, 0.0, 22.0),
            CumulativePredictionTimeSeconds: 112.3);

        var targetLap = new PredictedLap(
            DriverCode: "PIA",
            LapNumber: 12,
            IsPitLap: false,
            IsOutLap: false,
            Compound: TyreCompound.Medium,
            TyreAgeAtStart: 10,
            Breakdown: new LapPredictionBreakdown(90.2, 0.0, 0.15, 0.05, -0.01, 0.0),
            CumulativePredictionTimeSeconds: 112.26);

        return new PredictionResult(
            AttackerLaps: [attackerLap],
            TargetLaps: [targetLap],
            InitialGapSeconds: 1.0,
            GapAtTargetPitLapCompleteSeconds: 0.8,
            GapAtTargetOutLapCompleteSeconds: 0.6,
            GapAtBothDriversNormalLapCompleteSeconds: 0.5,
            DeltaGAtTargetPitLapCompleteSeconds: -0.2,
            DeltaGAtTargetOutLapCompleteSeconds: -0.4,
            DeltaGAtBothDriversNormalLapCompleteSeconds: -0.5,
            Classification: classification,
            MarginalThresholdSeconds: 0.25,
            Warnings: []);
    }
}
