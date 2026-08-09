using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Tests;

public sealed class TyreAndModelParameterTests
{
    #region Tyre compound parsing and normalization
    // These tests exist to protect ingestion of OpenF1 compound strings so upstream
    // formatting differences (case/whitespace/nulls) do not change model behavior.

    [Theory]
    [InlineData("SOFT", TyreCompound.Soft)]
    [InlineData("MEDIUM", TyreCompound.Medium)]
    [InlineData("HARD", TyreCompound.Hard)]
    [InlineData("INTERMEDIATE", TyreCompound.Intermediate)]
    [InlineData("WET", TyreCompound.Wet)]
    [InlineData("  soft  ", TyreCompound.Soft)]
    [InlineData("mEdIuM", TyreCompound.Medium)]
    public void FromOpenF1String_RecognizedValues_MapToExpectedEnum(string input, TyreCompound expected)
    {
        var result = TyreCompoundParser.FromOpenF1String(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ULTRASOFT")]
    [InlineData("C5")]
    public void FromOpenF1String_UnrecognizedOrMissingValues_ReturnUnknown(string? input)
    {
        var result = TyreCompoundParser.FromOpenF1String(input);

        Assert.Equal(TyreCompound.Unknown, result);
    }

    #endregion

    #region Tyre compound classification and display helpers
    // These tests exist to lock down UI-facing labels and dry/wet eligibility checks
    // that gate whether prediction warnings should be emitted.

    [Theory]
    [InlineData(TyreCompound.Soft, "S")]
    [InlineData(TyreCompound.Medium, "M")]
    [InlineData(TyreCompound.Hard, "H")]
    [InlineData(TyreCompound.Intermediate, "I")]
    [InlineData(TyreCompound.Wet, "W")]
    [InlineData(TyreCompound.Unknown, "?")]
    public void ToShortLabel_ReturnsExpectedLabel(TyreCompound compound, string expectedLabel)
    {
        var result = TyreCompoundParser.ToShortLabel(compound);

        Assert.Equal(expectedLabel, result);
    }

    [Theory]
    [InlineData(TyreCompound.Soft, true)]
    [InlineData(TyreCompound.Medium, true)]
    [InlineData(TyreCompound.Hard, true)]
    [InlineData(TyreCompound.Intermediate, false)]
    [InlineData(TyreCompound.Wet, false)]
    [InlineData(TyreCompound.Unknown, false)]
    public void IsDryCompound_IdentifiesSupportedDryCompounds(TyreCompound compound, bool expected)
    {
        var result = TyreCompoundParser.IsDryCompound(compound);

        Assert.Equal(expected, result);
    }

    #endregion

    #region LapModelParameters defaults and lookup semantics
    // These tests exist to protect the default modeling baseline and ensure compound/rate
    // lookups remain resilient when unsupported compounds are requested.

    [Fact]
    public void CreateDefault_UsesExpectedEngineeringBaselineValues()
    {
        var model = LapModelParameters.CreateDefault();

        Assert.Equal(0.0, model.GetCompoundOffset(TyreCompound.Soft));
        Assert.Equal(0.1, model.GetCompoundOffset(TyreCompound.Medium));
        Assert.Equal(0.2, model.GetCompoundOffset(TyreCompound.Hard));

        Assert.Equal(0.10, model.GetDegradationRate(TyreCompound.Soft), 10);
        Assert.Equal(0.07, model.GetDegradationRate(TyreCompound.Medium), 10);
        Assert.Equal(0.04, model.GetDegradationRate(TyreCompound.Hard), 10);

        Assert.Equal(0.3, model.WarmUp.OutLapPenaltySeconds, 10);
        Assert.Equal(22.0, model.PitLaneLossSeconds, 10);
        Assert.Equal(0.25, model.MarginalThresholdSeconds, 10);

        Assert.False(model.Traffic.ApplyToAttacker);
        Assert.False(model.Traffic.ApplyToTarget);
        Assert.Equal(0.3, model.Traffic.PenaltySeconds, 10);
    }

    [Fact]
    public void GetCompoundOffset_UnknownCompound_ReturnsZeroFallback()
    {
        var model = new LapModelParameters(
            CompoundOffsetsSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.1
            },
            DegradationRatesSecondsPerLap: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.2
            },
            WarmUp: WarmUpModelParameters.None(),
            PitLaneLossSeconds: 20.0,
            MarginalThresholdSeconds: 0.3,
            Traffic: TrafficModelParameters.None());

        Assert.Equal(0.0, model.GetCompoundOffset(TyreCompound.Hard));
        Assert.Equal(0.0, model.GetCompoundOffset(TyreCompound.Unknown));
    }

    [Fact]
    public void GetDegradationRate_UnknownCompound_ReturnsZeroFallback()
    {
        var model = new LapModelParameters(
            CompoundOffsetsSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.1
            },
            DegradationRatesSecondsPerLap: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.2
            },
            WarmUp: WarmUpModelParameters.None(),
            PitLaneLossSeconds: 20.0,
            MarginalThresholdSeconds: 0.3,
            Traffic: TrafficModelParameters.None());

        Assert.Equal(0.0, model.GetDegradationRate(TyreCompound.Hard));
        Assert.Equal(0.0, model.GetDegradationRate(TyreCompound.Unknown));
    }

    [Fact]
    public void GetCompoundOffset_AndRate_ReturnConfiguredValuesWithoutSanitization()
    {
        var model = new LapModelParameters(
            CompoundOffsetsSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = -0.2
            },
            DegradationRatesSecondsPerLap: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = -0.05
            },
            WarmUp: WarmUpModelParameters.None(),
            PitLaneLossSeconds: 20.0,
            MarginalThresholdSeconds: 0.3,
            Traffic: TrafficModelParameters.None());

        Assert.Equal(-0.2, model.GetCompoundOffset(TyreCompound.Soft), 10);
        Assert.Equal(-0.05, model.GetDegradationRate(TyreCompound.Soft), 10);
    }

    #endregion

    #region Warm-up and traffic helper factories
    // These tests exist to pin helper constructors used in UI defaults and tests,
    // ensuring convenience APIs map to correct internal flags/values.

    [Fact]
    public void WarmUp_Default_AndNone_ReturnExpectedPenalties()
    {
        var @default = WarmUpModelParameters.Default();
        var none = WarmUpModelParameters.None();

        Assert.Equal(0.3, @default.OutLapPenaltySeconds, 10);
        Assert.Equal(0.0, none.OutLapPenaltySeconds, 10);
    }

    [Fact]
    public void Traffic_None_ReturnsNoTrafficForEitherDriver()
    {
        var traffic = TrafficModelParameters.None();

        Assert.False(traffic.ApplyToAttacker);
        Assert.False(traffic.ApplyToTarget);
        Assert.Equal(0.0, traffic.PenaltySeconds, 10);
    }

    [Fact]
    public void Traffic_AttackerOnly_MapsFlagsAndPenaltyCorrectly()
    {
        var traffic = TrafficModelParameters.AttackerOnly(0.6);

        Assert.True(traffic.ApplyToAttacker);
        Assert.False(traffic.ApplyToTarget);
        Assert.Equal(0.6, traffic.PenaltySeconds, 10);
    }

    [Fact]
    public void Traffic_TargetOnly_MapsFlagsAndPenaltyCorrectly()
    {
        var traffic = TrafficModelParameters.TargetOnly(0.9);

        Assert.False(traffic.ApplyToAttacker);
        Assert.True(traffic.ApplyToTarget);
        Assert.Equal(0.9, traffic.PenaltySeconds, 10);
    }

    #endregion
}
