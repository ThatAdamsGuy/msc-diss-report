using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class LapTimePredictorTests
{
    // These constants keep arithmetic stable and make each term contribution easy to verify by inspection.
    private const double ReferencePace = 90.0;
    private const double PitLoss = 22.0;
    private const double WarmUpPenalty = 1.8;
    private const double TrafficPenalty = 0.7;

    #region Baseline equation structure
    // These tests exist to protect the core equation layout and ensure the predictor never
    // silently drops or reorders the six terms used to compute a lap.

    [Fact]
    public void Predict_NormalLap_UsesReferenceOffsetAndDegradationOnly()
    {
        var predictor = new LapTimePredictor();
        var model = CreateModel(
            softOffset: 0.0,
            mediumOffset: 0.6,
            hardOffset: 1.2,
            softDeg: 0.10,
            mediumDeg: 0.08,
            hardDeg: 0.05,
            warmUpPenalty: WarmUpPenalty,
            pitLoss: PitLoss,
            trafficPenalty: TrafficPenalty);

        var input = CreateInput(
            model,
            compound: TyreCompound.Medium,
            tyreAgeAtStart: 7,
            isPitLap: false,
            isOutLap: false,
            applyTrafficPenalty: true);

        var result = predictor.Predict(input);

        Assert.Equal(ReferencePace, result.ReferencePaceSeconds);
        Assert.Equal(0.6, result.CompoundOffsetSeconds);
        Assert.Equal(0.56, result.DegradationSeconds, 10);
        Assert.Equal(0.0, result.WarmUpSeconds);
        Assert.Equal(0.0, result.TrafficSeconds);
        Assert.Equal(0.0, result.PitLossSeconds);
        Assert.Equal(91.16, result.TotalSeconds, 10);
    }

    [Fact]
    public void Predict_AllTermsPresent_AddsEveryContributionIntoTotal()
    {
        var predictor = new LapTimePredictor();
        var model = CreateModel(
            softOffset: 0.0,
            mediumOffset: 0.5,
            hardOffset: 1.0,
            softDeg: 0.1,
            mediumDeg: 0.07,
            hardDeg: 0.04,
            warmUpPenalty: WarmUpPenalty,
            pitLoss: PitLoss,
            trafficPenalty: TrafficPenalty);

        var input = CreateInput(
            model,
            compound: TyreCompound.Hard,
            tyreAgeAtStart: 4,
            isPitLap: true,
            isOutLap: true,
            applyTrafficPenalty: true);

        var result = predictor.Predict(input);

        Assert.Equal(ReferencePace, result.ReferencePaceSeconds);
        Assert.Equal(1.0, result.CompoundOffsetSeconds);
        Assert.Equal(0.16, result.DegradationSeconds, 10);
        Assert.Equal(WarmUpPenalty, result.WarmUpSeconds);
        Assert.Equal(TrafficPenalty, result.TrafficSeconds);
        Assert.Equal(PitLoss, result.PitLossSeconds);
        Assert.Equal(115.66, result.TotalSeconds, 10);
    }

    #endregion

    #region Warm-up and traffic gating
    // These tests exist to verify conditional penalties are only applied under the exact
    // scenarios intended by the model (out lap and explicit traffic flag).

    [Fact]
    public void Predict_WarmUpPenalty_AppliesOnlyOnOutLap()
    {
        var predictor = new LapTimePredictor();
        var model = CreateDefaultModel();

        var outLapResult = predictor.Predict(CreateInput(model, TyreCompound.Soft, 2, isPitLap: false, isOutLap: true, applyTrafficPenalty: false));
        var normalLapResult = predictor.Predict(CreateInput(model, TyreCompound.Soft, 2, isPitLap: false, isOutLap: false, applyTrafficPenalty: false));

        Assert.Equal(WarmUpPenalty, outLapResult.WarmUpSeconds);
        Assert.Equal(0.0, normalLapResult.WarmUpSeconds);
    }

    [Theory]
    [InlineData(true, true, TrafficPenalty)]
    [InlineData(true, false, 0.0)]
    [InlineData(false, true, 0.0)]
    [InlineData(false, false, 0.0)]
    public void Predict_TrafficPenalty_AppliesOnlyWhenOutLapAndFlagged(bool isOutLap, bool applyTrafficPenalty, double expectedTraffic)
    {
        var predictor = new LapTimePredictor();
        var model = CreateDefaultModel();

        var result = predictor.Predict(CreateInput(model, TyreCompound.Soft, 3, isPitLap: false, isOutLap: isOutLap, applyTrafficPenalty: applyTrafficPenalty));

        Assert.Equal(expectedTraffic, result.TrafficSeconds);
    }

    [Theory]
    [InlineData(true, PitLoss)]
    [InlineData(false, 0.0)]
    public void Predict_PitLoss_AppliesOnlyOnPitLap(bool isPitLap, double expectedPitLoss)
    {
        var predictor = new LapTimePredictor();
        var model = CreateDefaultModel();

        var result = predictor.Predict(CreateInput(model, TyreCompound.Soft, 3, isPitLap: isPitLap, isOutLap: false, applyTrafficPenalty: false));

        Assert.Equal(expectedPitLoss, result.PitLossSeconds);
    }

    #endregion

    #region Compound and degradation behavior
    // These tests exist to protect per-compound lookup behavior and linear tyre-age degradation,
    // both of which are central to realistic race simulation.

    [Theory]
    [InlineData(TyreCompound.Soft, 0.0, 0.10)]
    [InlineData(TyreCompound.Medium, 0.5, 0.07)]
    [InlineData(TyreCompound.Hard, 1.0, 0.04)]
    public void Predict_UsesCompoundSpecificOffsetAndRate(TyreCompound compound, double expectedOffset, double expectedRate)
    {
        var predictor = new LapTimePredictor();
        var model = CreateDefaultModel();

        var result = predictor.Predict(CreateInput(model, compound, tyreAgeAtStart: 5, isPitLap: false, isOutLap: false, applyTrafficPenalty: false));

        Assert.Equal(expectedOffset, result.CompoundOffsetSeconds);
        Assert.Equal(expectedRate * 5, result.DegradationSeconds, 10);
    }

    [Fact]
    public void Predict_UnknownCompound_UsesZeroOffsetAndZeroDegradation()
    {
        var predictor = new LapTimePredictor();
        var model = CreateDefaultModel();

        var result = predictor.Predict(CreateInput(model, TyreCompound.Unknown, tyreAgeAtStart: 12, isPitLap: false, isOutLap: false, applyTrafficPenalty: false));

        Assert.Equal(0.0, result.CompoundOffsetSeconds);
        Assert.Equal(0.0, result.DegradationSeconds);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 0.07)]
    [InlineData(10, 0.7)]
    public void Predict_Degradation_IsLinearWithTyreAgeAtStart(int tyreAgeAtStart, double expectedDegradation)
    {
        var predictor = new LapTimePredictor();
        var model = CreateDefaultModel();

        var result = predictor.Predict(CreateInput(model, TyreCompound.Medium, tyreAgeAtStart, isPitLap: false, isOutLap: false, applyTrafficPenalty: false));

        Assert.Equal(expectedDegradation, result.DegradationSeconds, 10);
    }

    [Fact]
    public void Predict_NegativeTyreAge_IsNotSanitizedByLapPredictor()
    {
        var predictor = new LapTimePredictor();
        var model = CreateDefaultModel();

        var result = predictor.Predict(CreateInput(model, TyreCompound.Soft, tyreAgeAtStart: -3, isPitLap: false, isOutLap: false, applyTrafficPenalty: false));

        Assert.Equal(-0.3, result.DegradationSeconds, 10);
    }

    #endregion

    #region Configuration passthrough robustness
    // These tests exist to ensure the predictor remains deterministic even with unusual
    // but technically permitted configuration values.

    [Fact]
    public void Predict_ZeroWarmUpAndTrafficModel_ProducesNoConditionalPenalties()
    {
        var predictor = new LapTimePredictor();
        var model = CreateModel(
            softOffset: 0.0,
            mediumOffset: 0.5,
            hardOffset: 1.0,
            softDeg: 0.1,
            mediumDeg: 0.07,
            hardDeg: 0.04,
            warmUpPenalty: 0.0,
            pitLoss: PitLoss,
            trafficPenalty: 0.0);

        var result = predictor.Predict(CreateInput(model, TyreCompound.Soft, 8, isPitLap: false, isOutLap: true, applyTrafficPenalty: true));

        Assert.Equal(0.0, result.WarmUpSeconds);
        Assert.Equal(0.0, result.TrafficSeconds);
    }

    [Fact]
    public void Predict_NegativePitLoss_IsPassedThroughIntoResult()
    {
        var predictor = new LapTimePredictor();
        var model = CreateModel(
            softOffset: 0.0,
            mediumOffset: 0.5,
            hardOffset: 1.0,
            softDeg: 0.1,
            mediumDeg: 0.07,
            hardDeg: 0.04,
            warmUpPenalty: WarmUpPenalty,
            pitLoss: -3.5,
            trafficPenalty: TrafficPenalty);

        var result = predictor.Predict(CreateInput(model, TyreCompound.Soft, 0, isPitLap: true, isOutLap: false, applyTrafficPenalty: false));

        Assert.Equal(-3.5, result.PitLossSeconds);
        Assert.Equal(ReferencePace - 3.5, result.TotalSeconds, 10);
    }

    #endregion

    private static LapModelParameters CreateDefaultModel() => CreateModel(
        softOffset: 0.0,
        mediumOffset: 0.5,
        hardOffset: 1.0,
        softDeg: 0.1,
        mediumDeg: 0.07,
        hardDeg: 0.04,
        warmUpPenalty: WarmUpPenalty,
        pitLoss: PitLoss,
        trafficPenalty: TrafficPenalty);

    private static LapModelParameters CreateModel(
        double softOffset,
        double mediumOffset,
        double hardOffset,
        double softDeg,
        double mediumDeg,
        double hardDeg,
        double warmUpPenalty,
        double pitLoss,
        double trafficPenalty) =>
        new(
            CompoundOffsetsSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = softOffset,
                [TyreCompound.Medium] = mediumOffset,
                [TyreCompound.Hard] = hardOffset
            },
            DegradationRatesSecondsPerLap: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = softDeg,
                [TyreCompound.Medium] = mediumDeg,
                [TyreCompound.Hard] = hardDeg
            },
            WarmUp: new WarmUpModelParameters(warmUpPenalty),
            PitLaneLossSeconds: pitLoss,
            MarginalThresholdSeconds: 0.25,
            Traffic: new TrafficModelParameters(
                ApplyToAttacker: true,
                ApplyToTarget: true,
                PenaltySeconds: trafficPenalty));

    private static LapPredictionInput CreateInput(
        LapModelParameters model,
        TyreCompound compound,
        int tyreAgeAtStart,
        bool isPitLap,
        bool isOutLap,
        bool applyTrafficPenalty) =>
        new(
            DriverCode: "HAM",
            ReferencePaceSeconds: ReferencePace,
            Compound: compound,
            TyreAgeAtStart: tyreAgeAtStart,
            IsPitLap: isPitLap,
            IsOutLap: isOutLap,
            ApplyTrafficPenalty: applyTrafficPenalty,
            ModelParameters: model);
}
