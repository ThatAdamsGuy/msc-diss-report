using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Tests;

public sealed class PredictedLapTests
{
    [Fact]
    public void Record_StoresAllConstructorValues()
    {
        var breakdown = new LapPredictionBreakdown(
            ReferencePaceSeconds: 90.0,
            CompoundOffsetSeconds: 0.1,
            DegradationSeconds: 0.2,
            WarmUpSeconds: 0.3,
            TrafficSeconds: 0.0,
            PitLossSeconds: 22.0);

        var lap = new PredictedLap(
            DriverCode: "NOR",
            LapNumber: 18,
            IsPitLap: true,
            IsOutLap: false,
            Compound: TyreCompound.Hard,
            TyreAgeAtStart: 12,
            Breakdown: breakdown,
            CumulativePredictionTimeSeconds: 112.6);

        Assert.Equal("NOR", lap.DriverCode);
        Assert.Equal(18, lap.LapNumber);
        Assert.True(lap.IsPitLap);
        Assert.False(lap.IsOutLap);
        Assert.Equal(TyreCompound.Hard, lap.Compound);
        Assert.Equal(12, lap.TyreAgeAtStart);
        Assert.Equal(breakdown, lap.Breakdown);
        Assert.Equal(112.6, lap.CumulativePredictionTimeSeconds, 10);
    }
}
