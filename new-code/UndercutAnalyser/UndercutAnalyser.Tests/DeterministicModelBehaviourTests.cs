using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class DeterministicModelBehaviourTests
{
    private const double Tol = 10;

    #region Additive term effects and complementary attacker/target impacts

    [Fact]
    public void TargetDegradationIncrease_MovesGapInFavourOfAttacker_WithExactEffect()
    {
        var baseline = PredictWithModel(
            CreateModel(mediumDeg: 0.10, hardDeg: 0.05),
            attackerCurrentCompound: TyreCompound.Hard,
            attackerReplacementCompound: TyreCompound.Hard,
            targetCurrentCompound: TyreCompound.Medium,
            targetReplacementCompound: TyreCompound.Hard,
            targetCurrentAge: 3,
            targetResponseLaps: 1);

        var changed = PredictWithModel(
            CreateModel(mediumDeg: 0.30, hardDeg: 0.05),
            attackerCurrentCompound: TyreCompound.Hard,
            attackerReplacementCompound: TyreCompound.Hard,
            targetCurrentCompound: TyreCompound.Medium,
            targetReplacementCompound: TyreCompound.Hard,
            targetCurrentAge: 3,
            targetResponseLaps: 1);

        var expectedDelta = 0.20 * (3 + 4); // target on medium for laps n and n+1

        Assert.Equal(baseline.GapAtTargetPitLapCompleteSeconds - expectedDelta, changed.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.True(changed.GapAtTargetPitLapCompleteSeconds < baseline.GapAtTargetPitLapCompleteSeconds);
    }

    [Fact]
    public void AttackerDegradationIncrease_MovesGapAgainstAttacker_WithExactEffect()
    {
        var baseline = PredictWithModel(
            CreateModel(mediumDeg: 0.10, hardDeg: 0.05),
            attackerCurrentCompound: TyreCompound.Medium,
            attackerReplacementCompound: TyreCompound.Hard,
            targetCurrentCompound: TyreCompound.Hard,
            targetReplacementCompound: TyreCompound.Hard,
            attackerCurrentAge: 2,
            targetResponseLaps: 1);

        var changed = PredictWithModel(
            CreateModel(mediumDeg: 0.30, hardDeg: 0.05),
            attackerCurrentCompound: TyreCompound.Medium,
            attackerReplacementCompound: TyreCompound.Hard,
            targetCurrentCompound: TyreCompound.Hard,
            targetReplacementCompound: TyreCompound.Hard,
            attackerCurrentAge: 2,
            targetResponseLaps: 1);

        var expectedDelta = 0.20 * 2; // attacker medium only on pit lap at age 2 before switching to hard

        Assert.Equal(baseline.GapAtTargetPitLapCompleteSeconds + expectedDelta, changed.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.True(changed.GapAtTargetPitLapCompleteSeconds > baseline.GapAtTargetPitLapCompleteSeconds);
    }

    [Fact]
    public void AttackerWarmUpPenaltyIncrease_IncreasesAttackerOutLap_AndMovesGapAgainstAttacker()
    {
        var baseline = PredictWithModel(CreateModel(softWarmUp: 1.0));
        var changed = PredictWithModel(CreateModel(softWarmUp: 2.5));

        Assert.Equal(1.5, changed.AttackerLaps[1].Breakdown.WarmUpSeconds - baseline.AttackerLaps[1].Breakdown.WarmUpSeconds, Tol);
        Assert.Equal(1.5, changed.GapAtTargetPitLapCompleteSeconds - baseline.GapAtTargetPitLapCompleteSeconds, Tol);
    }

    [Fact]
    public void TargetWarmUpPenaltyIncrease_IncreasesTargetOutLap_AndMovesGapInFavourOfAttacker()
    {
        var baseline = PredictWithModel(CreateModel(mediumWarmUp: 1.0));
        var changed = PredictWithModel(CreateModel(mediumWarmUp: 2.5));

        Assert.Equal(1.5, changed.TargetLaps[2].Breakdown.WarmUpSeconds - baseline.TargetLaps[2].Breakdown.WarmUpSeconds, Tol);
        Assert.Equal(-1.5, changed.GapAtTargetOutLapCompleteSeconds - baseline.GapAtTargetOutLapCompleteSeconds, Tol);
    }

    [Fact]
    public void AttackerTrafficPenaltyIncrease_IncreasesAttackerOutLap_AndMovesGapAgainstAttacker()
    {
        var baseline = PredictWithModel(CreateModel(trafficPenalty: 0.2, trafficAttacker: true));
        var changed = PredictWithModel(CreateModel(trafficPenalty: 0.9, trafficAttacker: true));

        Assert.Equal(0.7, changed.AttackerLaps[1].Breakdown.TrafficSeconds - baseline.AttackerLaps[1].Breakdown.TrafficSeconds, Tol);
        Assert.Equal(0.7, changed.GapAtTargetPitLapCompleteSeconds - baseline.GapAtTargetPitLapCompleteSeconds, Tol);
    }

    [Fact]
    public void TargetTrafficPenaltyIncrease_IncreasesTargetOutLap_AndMovesGapInFavourOfAttacker()
    {
        var baseline = PredictWithModel(CreateModel(trafficPenalty: 0.2, trafficTarget: true));
        var changed = PredictWithModel(CreateModel(trafficPenalty: 0.9, trafficTarget: true));

        Assert.Equal(0.7, changed.TargetLaps[2].Breakdown.TrafficSeconds - baseline.TargetLaps[2].Breakdown.TrafficSeconds, Tol);
        Assert.Equal(-0.7, changed.GapAtTargetOutLapCompleteSeconds - baseline.GapAtTargetOutLapCompleteSeconds, Tol);
    }

    [Fact]
    public void AttackerCompoundOffsetIncrease_MovesGapAgainstAttacker()
    {
        var baseline = PredictWithModel(CreateModel(softOffset: 0.0));
        var changed = PredictWithModel(CreateModel(softOffset: 0.8));

        Assert.Equal(0.8, changed.AttackerLaps[1].Breakdown.CompoundOffsetSeconds - baseline.AttackerLaps[1].Breakdown.CompoundOffsetSeconds, Tol);
        Assert.True(changed.GapAtTargetPitLapCompleteSeconds > baseline.GapAtTargetPitLapCompleteSeconds);
    }

    [Fact]
    public void TargetCompoundOffsetIncrease_MovesGapInFavourOfAttacker()
    {
        var baseline = PredictWithModel(CreateModel(mediumOffset: 0.5));
        var changed = PredictWithModel(CreateModel(mediumOffset: 1.1));

        Assert.Equal(0.6, changed.TargetLaps[1].Breakdown.CompoundOffsetSeconds - baseline.TargetLaps[1].Breakdown.CompoundOffsetSeconds, Tol);
        Assert.True(changed.GapAtTargetPitLapCompleteSeconds < baseline.GapAtTargetPitLapCompleteSeconds);
    }

    [Fact]
    public void FasterAttackerReferencePace_MovesGapInFavourOfAttacker_WithExactEffect()
    {
        var baseline = PredictWithModel(CreateModel(), attackerReference: 90.0, targetReference: 90.0);
        var changed = PredictWithModel(CreateModel(), attackerReference: 89.5, targetReference: 90.0);

        Assert.Equal(-0.5, changed.AttackerLaps[0].Breakdown.ReferencePaceSeconds - baseline.AttackerLaps[0].Breakdown.ReferencePaceSeconds, Tol);
        Assert.Equal(-1.0, changed.GapAtTargetPitLapCompleteSeconds - baseline.GapAtTargetPitLapCompleteSeconds, Tol); // two attacker laps to primary endpoint
    }

    [Fact]
    public void FasterTargetReferencePace_MovesGapAgainstAttacker_WithExactEffect()
    {
        var baseline = PredictWithModel(CreateModel(), attackerReference: 90.0, targetReference: 90.0);
        var changed = PredictWithModel(CreateModel(), attackerReference: 90.0, targetReference: 89.5);

        Assert.Equal(-0.5, changed.TargetLaps[0].Breakdown.ReferencePaceSeconds - baseline.TargetLaps[0].Breakdown.ReferencePaceSeconds, Tol);
        Assert.Equal(+1.0, changed.GapAtTargetPitLapCompleteSeconds - baseline.GapAtTargetPitLapCompleteSeconds, Tol); // two target laps to primary endpoint
    }

    #endregion

    #region Initial gap behaviour and classification boundaries

    [Fact]
    public void IncreasingInitialGap_ShiftsAllGapsBySameAmount_AndKeepsDeltaGUnchanged()
    {
        var baseResult = PredictWithModel(CreateModel(), initialGap: 0.1);
        var shifted = PredictWithModel(CreateModel(), initialGap: 0.6);

        Assert.Equal(0.5, shifted.GapAtTargetPitLapCompleteSeconds - baseResult.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(0.5, shifted.GapAtTargetOutLapCompleteSeconds - baseResult.GapAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(0.5, shifted.GapAtBothDriversNormalLapCompleteSeconds - baseResult.GapAtBothDriversNormalLapCompleteSeconds, Tol);

        Assert.Equal(baseResult.DeltaGAtTargetPitLapCompleteSeconds, shifted.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(baseResult.DeltaGAtTargetOutLapCompleteSeconds, shifted.DeltaGAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(baseResult.DeltaGAtBothDriversNormalLapCompleteSeconds, shifted.DeltaGAtBothDriversNormalLapCompleteSeconds, Tol);
    }

    [Fact]
    public void DecreasingInitialGap_ShiftsAllGapsBySameAmount_AndKeepsDeltaGUnchanged()
    {
        var baseResult = PredictWithModel(CreateModel(), initialGap: 0.6);
        var shifted = PredictWithModel(CreateModel(), initialGap: 0.1);

        Assert.Equal(-0.5, shifted.GapAtTargetPitLapCompleteSeconds - baseResult.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(-0.5, shifted.GapAtTargetOutLapCompleteSeconds - baseResult.GapAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(-0.5, shifted.GapAtBothDriversNormalLapCompleteSeconds - baseResult.GapAtBothDriversNormalLapCompleteSeconds, Tol);

        Assert.Equal(baseResult.DeltaGAtTargetPitLapCompleteSeconds, shifted.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(baseResult.DeltaGAtTargetOutLapCompleteSeconds, shifted.DeltaGAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(baseResult.DeltaGAtBothDriversNormalLapCompleteSeconds, shifted.DeltaGAtBothDriversNormalLapCompleteSeconds, Tol);
    }

    [Theory]
    [InlineData(-0.30, UndercutClassification.PredictedAhead)]
    [InlineData(-0.25, UndercutClassification.PredictedMarginal)]
    [InlineData(-0.01, UndercutClassification.PredictedMarginal)]
    [InlineData(0.00, UndercutClassification.PredictedMarginal)]
    [InlineData(0.01, UndercutClassification.PredictedMarginal)]
    [InlineData(0.25, UndercutClassification.PredictedMarginal)]
    [InlineData(0.30, UndercutClassification.PredictedBehind)]
    public void ClassificationBoundaries_MatchThresholdConvention(double primaryGap, UndercutClassification expected)
    {
        var result = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 0.0,
            marginalThreshold: 0.25,
            attackerLapTotals: [10, 10, 10, 10],
            targetLapTotals: [10, 10 - primaryGap, 10, 10]);

        Assert.Equal(primaryGap, result.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(expected, result.Classification);
    }

    [Theory]
    [InlineData(-0.001, UndercutClassification.PredictedAhead)]
    [InlineData(0.0, UndercutClassification.PredictedMarginal)]
    [InlineData(0.001, UndercutClassification.PredictedBehind)]
    public void ZeroEpsilon_HasNoNonZeroMarginalInterval(double primaryGap, UndercutClassification expected)
    {
        var result = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 0.0,
            marginalThreshold: 0.0,
            attackerLapTotals: [10, 10, 10, 10],
            targetLapTotals: [10, 10 - primaryGap, 10, 10]);

        Assert.Equal(expected, result.Classification);
    }

    #endregion

    #region Response offset, transitions, ages, and invariants

    [Fact]
    public void ResponseOffset_OneTwoThree_ChangesWindowAndPitTimingCorrectly()
    {
        var r1 = PredictWithScriptedLaps(targetResponseLaps: 1, initialGap: 0.0, marginalThreshold: 0.25, attackerLapTotals: [10, 10, 10, 10], targetLapTotals: [12, 12, 12, 12]);
        var r2 = PredictWithScriptedLaps(targetResponseLaps: 2, initialGap: 0.0, marginalThreshold: 0.25, attackerLapTotals: [10, 10, 10, 10, 10], targetLapTotals: [12, 12, 12, 12, 12]);
        var r3 = PredictWithScriptedLaps(targetResponseLaps: 3, initialGap: 0.0, marginalThreshold: 0.25, attackerLapTotals: [10, 10, 10, 10, 10, 10], targetLapTotals: [12, 12, 12, 12, 12, 12]);

        Assert.Equal(4, r1.AttackerLaps.Count);
        Assert.Equal(5, r2.AttackerLaps.Count);
        Assert.Equal(6, r3.AttackerLaps.Count);

        Assert.True(r1.TargetLaps.Single(l => l.LapNumber == 2).IsPitLap);
        Assert.True(r2.TargetLaps.Single(l => l.LapNumber == 3).IsPitLap);
        Assert.True(r3.TargetLaps.Single(l => l.LapNumber == 4).IsPitLap);

        Assert.True(r1.GapAtTargetPitLapCompleteSeconds > r2.GapAtTargetPitLapCompleteSeconds);
        Assert.True(r2.GapAtTargetPitLapCompleteSeconds > r3.GapAtTargetPitLapCompleteSeconds);
    }

    [Fact]
    public void PositiveDegradationAndTyreAge_IncreaseLapTimeLinearly()
    {
        var predictor = new LapTimePredictor();
        var model = CreateModel(mediumDeg: 0.2);

        var a2 = predictor.Predict(new LapPredictionInput("D", 90, TyreCompound.Medium, 2, false, false, false, model));
        var a5 = predictor.Predict(new LapPredictionInput("D", 90, TyreCompound.Medium, 5, false, false, false, model));

        Assert.Equal(0.6, a5.TotalSeconds - a2.TotalSeconds, Tol);
    }

    [Fact]
    public void ZeroDegradation_MakesTyreAgeInvariant()
    {
        var predictor = new LapTimePredictor();
        var model = CreateModel(mediumDeg: 0.0);

        var a2 = predictor.Predict(new LapPredictionInput("D", 90, TyreCompound.Medium, 2, false, false, false, model));
        var a9 = predictor.Predict(new LapPredictionInput("D", 90, TyreCompound.Medium, 9, false, false, false, model));

        Assert.Equal(a2.TotalSeconds, a9.TotalSeconds, Tol);
    }

    [Fact]
    public void ReplacementTyreNonZeroAge_StartsAtSpecifiedAge_AndProgresses()
    {
        var result = PredictWithModel(CreateModel(), attackerReplacementAge: 3, targetResponseLaps: 2);

        var attacker = result.AttackerLaps;
        Assert.Equal(3, attacker[1].TyreAgeAtStart);
        Assert.Equal(4, attacker[2].TyreAgeAtStart);
        Assert.Equal(5, attacker[3].TyreAgeAtStart);
        Assert.Equal(6, attacker[4].TyreAgeAtStart);
    }

    [Fact]
    public void AttackerTyreStateTransition_OccursExactlyOnOutLap()
    {
        var result = PredictWithModel(
            CreateModel(),
            attackerCurrentCompound: TyreCompound.Hard,
            attackerCurrentAge: 8,
            attackerReplacementCompound: TyreCompound.Soft,
            attackerReplacementAge: 2,
            targetResponseLaps: 1);

        Assert.Equal((TyreCompound.Hard, 8), (result.AttackerLaps[0].Compound, result.AttackerLaps[0].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Soft, 2), (result.AttackerLaps[1].Compound, result.AttackerLaps[1].TyreAgeAtStart));
    }

    [Fact]
    public void TargetTyreStateTransition_OccursAtCorrectOffset_ForR1AndR2()
    {
        var r1 = PredictWithModel(CreateModel(), targetCurrentCompound: TyreCompound.Hard, targetCurrentAge: 6, targetReplacementCompound: TyreCompound.Medium, targetReplacementAge: 1, targetResponseLaps: 1);
        var r2 = PredictWithModel(CreateModel(), targetCurrentCompound: TyreCompound.Hard, targetCurrentAge: 6, targetReplacementCompound: TyreCompound.Medium, targetReplacementAge: 1, targetResponseLaps: 2);

        Assert.Equal((TyreCompound.Hard, true), (r1.TargetLaps[1].Compound, r1.TargetLaps[1].IsPitLap));
        Assert.Equal((TyreCompound.Medium, true), (r1.TargetLaps[2].Compound, r1.TargetLaps[2].IsOutLap));

        Assert.Equal((TyreCompound.Hard, true), (r2.TargetLaps[2].Compound, r2.TargetLaps[2].IsPitLap));
        Assert.Equal((TyreCompound.Medium, true), (r2.TargetLaps[3].Compound, r2.TargetLaps[3].IsOutLap));
    }

    [Fact]
    public void InitialGapInvariant_HoldsAtFirstComputedStep()
    {
        var result = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 1.2,
            marginalThreshold: 0.25,
            attackerLapTotals: [10, 10, 10, 10],
            targetLapTotals: [10, 10, 10, 10]);

        Assert.Equal(1.2, result.GapAtTargetPitLapCompleteSeconds, Tol);
    }

    [Fact]
    public void CumulativeGapAndDeltaEquations_HoldAcrossAllEndpoints()
    {
        var result = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 2.0,
            marginalThreshold: 0.25,
            attackerLapTotals: [10, 20, 30, 40],
            targetLapTotals: [13, 19, 35, 41]);

        // cumulative diffs: lap1 -3, lap2 +1, lap3 -5, lap4 -1
        // G after lap2 (primary) = 2 + (-3 + 1) = 0
        // G after lap3 (secondary) = 2 + (-3 + 1 - 5) = -5
        // G after lap4 (tertiary) = 2 + (-3 + 1 - 5 - 1) = -6
        Assert.Equal(0.0, result.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(-5.0, result.GapAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(-6.0, result.GapAtBothDriversNormalLapCompleteSeconds, Tol);

        Assert.Equal(result.GapAtTargetPitLapCompleteSeconds - result.InitialGapSeconds, result.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(result.GapAtTargetOutLapCompleteSeconds - result.InitialGapSeconds, result.DeltaGAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(result.GapAtBothDriversNormalLapCompleteSeconds - result.InitialGapSeconds, result.DeltaGAtBothDriversNormalLapCompleteSeconds, Tol);
    }

    [Fact]
    public void IdenticalPerformance_KeepsGapConstant_AndDeltaZero()
    {
        var result = PredictWithScriptedLaps(
            targetResponseLaps: 2,
            initialGap: 1.7,
            marginalThreshold: 0.25,
            attackerLapTotals: [10, 10, 10, 10, 10],
            targetLapTotals: [10, 10, 10, 10, 10]);

        Assert.Equal(1.7, result.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(1.7, result.GapAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(1.7, result.GapAtBothDriversNormalLapCompleteSeconds, Tol);

        Assert.Equal(0.0, result.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(0.0, result.DeltaGAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(0.0, result.DeltaGAtBothDriversNormalLapCompleteSeconds, Tol);
    }

    [Fact]
    public void SignConvention_AttackerFaster_DecreasesGapByExactAmount()
    {
        var result = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 0.0,
            marginalThreshold: 0.25,
            attackerLapTotals: [89.5, 90.0, 90.0, 90.0],
            targetLapTotals: [90.0, 90.0, 90.0, 90.0]);

        Assert.Equal(-0.5, result.GapAtTargetPitLapCompleteSeconds, Tol);
    }

    [Fact]
    public void SignConvention_TargetFaster_IncreasesGapByExactAmount()
    {
        var result = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 0.0,
            marginalThreshold: 0.25,
            attackerLapTotals: [90.0, 90.0, 90.0, 90.0],
            targetLapTotals: [89.5, 90.0, 90.0, 90.0]);

        Assert.Equal(+0.5, result.GapAtTargetPitLapCompleteSeconds, Tol);
    }

    [Fact]
    public void AttackerTargetSignSymmetry_ProducesEqualOppositeDelta()
    {
        var a = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 0.0,
            marginalThreshold: 0.25,
            attackerLapTotals: [9.0, 10.0, 10.0, 10.0],
            targetLapTotals: [10.0, 10.0, 10.0, 10.0]);

        var b = PredictWithScriptedLaps(
            targetResponseLaps: 1,
            initialGap: 0.0,
            marginalThreshold: 0.25,
            attackerLapTotals: [10.0, 10.0, 10.0, 10.0],
            targetLapTotals: [9.0, 10.0, 10.0, 10.0]);

        Assert.Equal(-1.0, a.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(+1.0, b.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(a.DeltaGAtTargetPitLapCompleteSeconds, -b.DeltaGAtTargetPitLapCompleteSeconds, Tol);
    }

    [Fact]
    public void CompleteProductionPredictionPath_MinimalSyntheticScenario_MatchesIndependentGapDeltaAndClassification()
    {
        var model = CreateModel(
            softOffset: 0.0,
            mediumOffset: 0.0,
            hardOffset: 0.0,
            softDeg: 0.0,
            mediumDeg: 0.0,
            hardDeg: 0.0,
            softWarmUp: 0.0,
            mediumWarmUp: 0.0,
            hardWarmUp: 0.0,
            pitLoss: 10.0,
            marginalThreshold: 0.25);

        var result = PredictWithModel(
            model: model,
            initialGap: 1.5,
            targetResponseLaps: 1,
            attackerReference: 100.0,
            targetReference: 101.0,
            attackerCurrentCompound: TyreCompound.Soft,
            attackerCurrentAge: 0,
            targetCurrentCompound: TyreCompound.Soft,
            targetCurrentAge: 0,
            attackerReplacementCompound: TyreCompound.Soft,
            attackerReplacementAge: 0,
            targetReplacementCompound: TyreCompound.Soft,
            targetReplacementAge: 0);

        Assert.Equal(110.0, result.AttackerLaps[0].Breakdown.TotalSeconds, Tol);
        Assert.Equal(100.0, result.AttackerLaps[1].Breakdown.TotalSeconds, Tol);
        Assert.Equal(101.0, result.TargetLaps[0].Breakdown.TotalSeconds, Tol);
        Assert.Equal(111.0, result.TargetLaps[1].Breakdown.TotalSeconds, Tol);

        Assert.Equal(-0.5, result.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(-1.5, result.GapAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(-2.5, result.GapAtBothDriversNormalLapCompleteSeconds, Tol);

        Assert.Equal(-2.0, result.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(-3.0, result.DeltaGAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(-4.0, result.DeltaGAtBothDriversNormalLapCompleteSeconds, Tol);

        Assert.Equal(UndercutClassification.PredictedAhead, result.Classification);
    }

    [Fact]
    public void PredictingSameRequestMultipleTimes_ProducesIdenticalOutputs()
    {
        var request = CreateRequest(
            model: CreateModel(),
            initialGap: 1.1,
            targetResponseLaps: 2,
            attackerReference: 90.2,
            targetReference: 90.7,
            attackerCurrentCompound: TyreCompound.Medium,
            attackerCurrentAge: 6,
            targetCurrentCompound: TyreCompound.Hard,
            targetCurrentAge: 9,
            attackerReplacementCompound: TyreCompound.Soft,
            attackerReplacementAge: 1,
            targetReplacementCompound: TyreCompound.Medium,
            targetReplacementAge: 2);

        var predictor = new PitSequencePredictor(new LapTimePredictor());

        var first = predictor.Predict(request);
        var second = predictor.Predict(request);
        var third = predictor.Predict(request);

        AssertPredictionEquivalent(first, second);
        AssertPredictionEquivalent(second, third);
    }

    #endregion

    private static void AssertPredictionEquivalent(PredictionResult expected, PredictionResult actual)
    {
        Assert.Equal(expected.InitialGapSeconds, actual.InitialGapSeconds, Tol);
        Assert.Equal(expected.GapAtTargetPitLapCompleteSeconds, actual.GapAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(expected.GapAtTargetOutLapCompleteSeconds, actual.GapAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(expected.GapAtBothDriversNormalLapCompleteSeconds, actual.GapAtBothDriversNormalLapCompleteSeconds, Tol);
        Assert.Equal(expected.DeltaGAtTargetPitLapCompleteSeconds, actual.DeltaGAtTargetPitLapCompleteSeconds, Tol);
        Assert.Equal(expected.DeltaGAtTargetOutLapCompleteSeconds, actual.DeltaGAtTargetOutLapCompleteSeconds, Tol);
        Assert.Equal(expected.DeltaGAtBothDriversNormalLapCompleteSeconds, actual.DeltaGAtBothDriversNormalLapCompleteSeconds, Tol);
        Assert.Equal(expected.Classification, actual.Classification);
        Assert.Equal(expected.MarginalThresholdSeconds, actual.MarginalThresholdSeconds, Tol);
        Assert.Equal(expected.Warnings, actual.Warnings);

        Assert.Equal(expected.AttackerLaps.Count, actual.AttackerLaps.Count);
        Assert.Equal(expected.TargetLaps.Count, actual.TargetLaps.Count);

        for (var i = 0; i < expected.AttackerLaps.Count; i++)
            Assert.Equal(expected.AttackerLaps[i], actual.AttackerLaps[i]);

        for (var i = 0; i < expected.TargetLaps.Count; i++)
            Assert.Equal(expected.TargetLaps[i], actual.TargetLaps[i]);
    }

    private static PredictionResult PredictWithModel(
        LapModelParameters model,
        double initialGap = 0.0,
        int targetResponseLaps = 1,
        double attackerReference = 90.0,
        double targetReference = 90.0,
        TyreCompound attackerCurrentCompound = TyreCompound.Medium,
        int attackerCurrentAge = 2,
        TyreCompound targetCurrentCompound = TyreCompound.Medium,
        int targetCurrentAge = 3,
        TyreCompound attackerReplacementCompound = TyreCompound.Soft,
        int attackerReplacementAge = 0,
        TyreCompound targetReplacementCompound = TyreCompound.Medium,
        int targetReplacementAge = 0)
    {
        var req = CreateRequest(
            model: model,
            initialGap: initialGap,
            targetResponseLaps: targetResponseLaps,
            attackerReference: attackerReference,
            targetReference: targetReference,
            attackerCurrentCompound: attackerCurrentCompound,
            attackerCurrentAge: attackerCurrentAge,
            targetCurrentCompound: targetCurrentCompound,
            targetCurrentAge: targetCurrentAge,
            attackerReplacementCompound: attackerReplacementCompound,
            attackerReplacementAge: attackerReplacementAge,
            targetReplacementCompound: targetReplacementCompound,
            targetReplacementAge: targetReplacementAge);

        return new PitSequencePredictor(new LapTimePredictor()).Predict(req);
    }

    private static PredictionResult PredictWithScriptedLaps(
        int targetResponseLaps,
        double initialGap,
        double marginalThreshold,
        IReadOnlyList<double> attackerLapTotals,
        IReadOnlyList<double> targetLapTotals)
    {
        var callIndexByDriver = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ATT"] = 0,
            ["TGT"] = 0
        };

        var predictor = new ScriptedLapPredictor(input =>
        {
            var idx = callIndexByDriver[input.DriverCode];
            callIndexByDriver[input.DriverCode] = idx + 1;

            var total = input.DriverCode == "ATT"
                ? attackerLapTotals[idx]
                : targetLapTotals[idx];

            return new LapPredictionBreakdown(total, 0, 0, 0, 0, 0);
        });

        var req = CreateRequest(
            model: CreateModel(marginalThreshold: marginalThreshold),
            initialGap: initialGap,
            targetResponseLaps: targetResponseLaps);

        return new PitSequencePredictor(predictor).Predict(req);
    }

    private static PredictionRequest CreateRequest(
        LapModelParameters model,
        double initialGap = 0.0,
        int targetResponseLaps = 1,
        double attackerReference = 90.0,
        double targetReference = 90.0,
        TyreCompound attackerCurrentCompound = TyreCompound.Medium,
        int attackerCurrentAge = 2,
        TyreCompound targetCurrentCompound = TyreCompound.Medium,
        int targetCurrentAge = 3,
        TyreCompound attackerReplacementCompound = TyreCompound.Soft,
        int attackerReplacementAge = 0,
        TyreCompound targetReplacementCompound = TyreCompound.Medium,
        int targetReplacementAge = 0)
    {
        return new PredictionRequest(
            EventName: "Deterministic Test",
            DecisionLap: 1,
            InitialAttackerGapToTargetSeconds: initialGap,
            Attacker: new DriverPredictionState("ATT", "Attacker", 2, attackerReference, attackerCurrentCompound, attackerCurrentAge),
            Target: new DriverPredictionState("TGT", "Target", 1, targetReference, targetCurrentCompound, targetCurrentAge),
            AttackerReplacementTyre: new TyreSetSpecification(attackerReplacementCompound, attackerReplacementAge),
            TargetReplacementTyre: new TyreSetSpecification(targetReplacementCompound, targetReplacementAge),
            TargetResponseLaps: targetResponseLaps,
            ModelParameters: model);
    }

    private static LapModelParameters CreateModel(
        double softOffset = 0.0,
        double mediumOffset = 0.5,
        double hardOffset = 1.0,
        double softDeg = 0.10,
        double mediumDeg = 0.10,
        double hardDeg = 0.05,
        double softWarmUp = 1.2,
        double mediumWarmUp = 1.2,
        double hardWarmUp = 1.2,
        double pitLoss = 22.0,
        double marginalThreshold = 0.25,
        bool trafficAttacker = false,
        bool trafficTarget = false,
        double trafficPenalty = 0.3)
    {
        return new LapModelParameters(
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
            WarmUpPenaltiesSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = softWarmUp,
                [TyreCompound.Medium] = mediumWarmUp,
                [TyreCompound.Hard] = hardWarmUp
            },
            PitLaneLossSeconds: pitLoss,
            MarginalThresholdSeconds: marginalThreshold,
            Traffic: new TrafficModelParameters(trafficAttacker, trafficTarget, trafficPenalty));
    }

    private sealed class ScriptedLapPredictor : ILapTimePredictor
    {
        private readonly Func<LapPredictionInput, LapPredictionBreakdown> _predict;

        public ScriptedLapPredictor(Func<LapPredictionInput, LapPredictionBreakdown> predict)
        {
            _predict = predict;
        }

        public LapPredictionBreakdown Predict(LapPredictionInput input) => _predict(input);
    }
}
