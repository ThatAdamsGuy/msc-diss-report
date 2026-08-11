using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class PitSequencePredictorTests
{
    #region Sequence construction and tyre-state progression
    // These tests exist to protect the lap-by-lap orchestration logic: which lap is pit/out/normal,
    // when compounds switch, and how tyre age advances for each driver.

    [Fact]
    public void Predict_ResponseOne_ConstructsExpectedLapWindowAndMarkers()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(decisionLap: 20, targetResponseLaps: 1);

        var result = sut.Predict(request);

        Assert.Equal(4, result.AttackerLaps.Count);
        Assert.Equal(4, result.TargetLaps.Count);

        Assert.Equal(new[] { 20, 21, 22, 23 }, result.AttackerLaps.Select(l => l.LapNumber).ToArray());
        Assert.Equal(new[] { 20, 21, 22, 23 }, result.TargetLaps.Select(l => l.LapNumber).ToArray());

        Assert.True(result.AttackerLaps[0].IsPitLap);
        Assert.True(result.AttackerLaps[1].IsOutLap);
        Assert.False(result.AttackerLaps[2].IsPitLap || result.AttackerLaps[2].IsOutLap);
        Assert.False(result.AttackerLaps[3].IsPitLap || result.AttackerLaps[3].IsOutLap);

        Assert.True(result.TargetLaps[1].IsPitLap);
        Assert.True(result.TargetLaps[2].IsOutLap);
        Assert.False(result.TargetLaps[0].IsPitLap || result.TargetLaps[0].IsOutLap);
        Assert.False(result.TargetLaps[3].IsPitLap || result.TargetLaps[3].IsOutLap);
    }

    [Fact]
    public void Predict_ResponseTwo_SwitchesCompoundsAndAgesOnCorrectLaps()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(
            decisionLap: 50,
            targetResponseLaps: 2,
            attackerCurrentCompound: TyreCompound.Hard,
            attackerCurrentAge: 14,
            targetCurrentCompound: TyreCompound.Medium,
            targetCurrentAge: 8,
            attackerReplacementCompound: TyreCompound.Soft,
            attackerReplacementAge: 1,
            targetReplacementCompound: TyreCompound.Hard,
            targetReplacementAge: 3);

        _ = sut.Predict(request);

        var attackerCalls = lapPredictor.Calls.Where(c => c.DriverCode == "ATT").ToArray();
        var targetCalls = lapPredictor.Calls.Where(c => c.DriverCode == "TGT").ToArray();

        Assert.Equal(5, attackerCalls.Length);
        Assert.Equal(5, targetCalls.Length);

        Assert.Equal((TyreCompound.Hard, 14), (attackerCalls[0].Compound, attackerCalls[0].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Soft, 1), (attackerCalls[1].Compound, attackerCalls[1].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Soft, 2), (attackerCalls[2].Compound, attackerCalls[2].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Soft, 3), (attackerCalls[3].Compound, attackerCalls[3].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Soft, 4), (attackerCalls[4].Compound, attackerCalls[4].TyreAgeAtStart));

        Assert.Equal((TyreCompound.Medium, 8), (targetCalls[0].Compound, targetCalls[0].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Medium, 9), (targetCalls[1].Compound, targetCalls[1].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Medium, 10), (targetCalls[2].Compound, targetCalls[2].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Hard, 3), (targetCalls[3].Compound, targetCalls[3].TyreAgeAtStart));
        Assert.Equal((TyreCompound.Hard, 4), (targetCalls[4].Compound, targetCalls[4].TyreAgeAtStart));
    }

    [Fact]
    public void Predict_NegativeCurrentTyreAges_AreClampedToZeroBeforeFirstLap()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(attackerCurrentAge: -2, targetCurrentAge: -9);

        _ = sut.Predict(request);

        var firstAttackerCall = lapPredictor.Calls.First(c => c.DriverCode == "ATT");
        var firstTargetCall = lapPredictor.Calls.First(c => c.DriverCode == "TGT");

        Assert.Equal(0, firstAttackerCall.TyreAgeAtStart);
        Assert.Equal(0, firstTargetCall.TyreAgeAtStart);
    }

    [Fact]
    public void Predict_NegativeAttackerReplacementTyreAge_IsClampedToZeroOnOutLap()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(attackerReplacementAge: -3, targetResponseLaps: 1);

        _ = sut.Predict(request);

        var attackerOutLapCall = lapPredictor.Calls.First(c => c.DriverCode == "ATT" && c.IsOutLap);
        Assert.Equal(0, attackerOutLapCall.TyreAgeAtStart);
    }

    [Fact]
    public void Predict_NegativeTargetReplacementTyreAge_IsClampedToZeroOnOutLap()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(targetReplacementAge: -4, targetResponseLaps: 1);

        _ = sut.Predict(request);

        var targetOutLapCall = lapPredictor.Calls.First(c => c.DriverCode == "TGT" && c.IsOutLap);
        Assert.Equal(0, targetOutLapCall.TyreAgeAtStart);
    }

    #endregion

    #region Gap, delta, and classification outcomes
    // These tests exist to verify all reported endpoint metrics and DeltaG fields
    // are computed from cumulative elapsed times with the documented sign convention.

    [Fact]
    public void Predict_ComputesGapEndpointsAndDeltasFromCumulativeElapsedTimes()
    {
        var lapPredictor = new RecordingLapPredictor(input =>
        {
            if (input.DriverCode == "ATT")
            {
                if (input.IsPitLap) return Breakdown(total: 10.0);
                if (input.IsOutLap) return Breakdown(total: 20.0);
                return Breakdown(total: 30.0);
            }

            if (input.IsPitLap) return Breakdown(total: 40.0);
            if (input.IsOutLap) return Breakdown(total: 50.0);
            return Breakdown(total: 60.0);
        });

        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(decisionLap: 7, targetResponseLaps: 1, initialGapSeconds: 5.0);

        var result = sut.Predict(request);

        Assert.Equal(-65.0, result.GapAtTargetPitLapCompleteSeconds, 10);
        Assert.Equal(-85.0, result.GapAtTargetOutLapCompleteSeconds, 10);
        Assert.Equal(-115.0, result.GapAtBothDriversNormalLapCompleteSeconds, 10);

        Assert.Equal(-70.0, result.DeltaGAtTargetPitLapCompleteSeconds, 10);
        Assert.Equal(-90.0, result.DeltaGAtTargetOutLapCompleteSeconds, 10);
        Assert.Equal(-120.0, result.DeltaGAtBothDriversNormalLapCompleteSeconds, 10);
    }

    [Theory]
    [InlineData(-0.30, UndercutClassification.PredictedAhead)]
    [InlineData(0.10, UndercutClassification.PredictedMarginal)]
    [InlineData(0.30, UndercutClassification.PredictedBehind)]
    public void Predict_ClassifiesUsingTargetPitLapCompleteGapAgainstSymmetricThreshold(double initialGapSeconds, UndercutClassification expected)
    {
        var lapPredictor = new RecordingLapPredictor(_ => Breakdown(total: 10.0));
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(
            initialGapSeconds: initialGapSeconds,
            targetResponseLaps: 1,
            marginalThreshold: 0.25);

        var result = sut.Predict(request);

        Assert.Equal(initialGapSeconds, result.GapAtTargetPitLapCompleteSeconds, 10);
        Assert.Equal(expected, result.Classification);
    }

    [Fact]
    public void Predict_UsesConfiguredMarginalThresholdInResult()
    {
        var lapPredictor = new RecordingLapPredictor(_ => Breakdown(total: 15.0));
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(initialGapSeconds: 0.0, marginalThreshold: 0.4);

        var result = sut.Predict(request);

        Assert.Equal(0.4, result.MarginalThresholdSeconds, 10);
    }

    [Fact]
    public void Predict_NegativeMarginalThreshold_IsClampedToZeroInResultAndWarnings()
    {
        var lapPredictor = new RecordingLapPredictor(_ => Breakdown(total: 10.0));
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(initialGapSeconds: 0.10, targetResponseLaps: 1, marginalThreshold: -0.25);

        var result = sut.Predict(request);

        Assert.Equal(0.0, result.MarginalThresholdSeconds, 10);
        Assert.Equal(UndercutClassification.PredictedBehind, result.Classification);
        Assert.Contains(result.Warnings, w => w.Contains("Marginal threshold is negative", StringComparison.Ordinal));
    }

    [Fact]
    public void Predict_ClassificationUsesTargetPitLapCompleteGapEvenWhenLaterGapsChangeDirection()
    {
        var lapPredictor = new RecordingLapPredictor(input =>
        {
            if (input.DriverCode == "ATT")
            {
                if (input.IsPitLap) return Breakdown(total: 100.0);
                if (input.IsOutLap) return Breakdown(total: 100.0);
                return Breakdown(total: 1.0);
            }

            if (input.IsPitLap) return Breakdown(total: 1.0);
            if (input.IsOutLap) return Breakdown(total: 300.0);
            return Breakdown(total: 1.0);
        });

        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(initialGapSeconds: 0.0, targetResponseLaps: 1, marginalThreshold: 0.25);

        var result = sut.Predict(request);

        Assert.True(result.GapAtTargetPitLapCompleteSeconds > 0.25);   // behind at TargetPitLapComplete endpoint
        Assert.True(result.GapAtTargetOutLapCompleteSeconds < -0.25);  // but ahead later
        Assert.Equal(UndercutClassification.PredictedBehind, result.Classification);
    }

    [Fact]
    public void Predict_InitialGapIsPreservedAndDeltaMatchesGapMinusInitialAtEachEndpoint()
    {
        var lapPredictor = new RecordingLapPredictor(_ => Breakdown(total: 10.0));
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(initialGapSeconds: 2.75, targetResponseLaps: 2);

        var result = sut.Predict(request);

        Assert.Equal(2.75, result.InitialGapSeconds, 10);
        Assert.Equal(result.GapAtTargetPitLapCompleteSeconds - result.InitialGapSeconds, result.DeltaGAtTargetPitLapCompleteSeconds, 10);
        Assert.Equal(result.GapAtTargetOutLapCompleteSeconds - result.InitialGapSeconds, result.DeltaGAtTargetOutLapCompleteSeconds, 10);
        Assert.Equal(result.GapAtBothDriversNormalLapCompleteSeconds - result.InitialGapSeconds, result.DeltaGAtBothDriversNormalLapCompleteSeconds, 10);
    }

    #endregion

    #region Traffic forwarding and lap-predictor integration contract
    // These tests exist to verify that PitSequencePredictor forwards the correct flags/state
    // into ILapTimePredictor for every lap and for both drivers.

    [Fact]
    public void Predict_ForwardsTrafficPenaltyOnlyOnOutLapOfFlaggedDriver()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(targetResponseLaps: 2, trafficAttacker: true, trafficTarget: true);

        _ = sut.Predict(request);

        var attackerCalls = lapPredictor.Calls.Where(c => c.DriverCode == "ATT").ToArray();
        var targetCalls = lapPredictor.Calls.Where(c => c.DriverCode == "TGT").ToArray();

        Assert.False(attackerCalls[0].ApplyTrafficPenalty);
        Assert.True(attackerCalls[1].ApplyTrafficPenalty);
        Assert.False(attackerCalls[2].ApplyTrafficPenalty);
        Assert.False(attackerCalls[3].ApplyTrafficPenalty);
        Assert.False(attackerCalls[4].ApplyTrafficPenalty);

        Assert.False(targetCalls[0].ApplyTrafficPenalty);
        Assert.False(targetCalls[1].ApplyTrafficPenalty);
        Assert.False(targetCalls[2].ApplyTrafficPenalty);
        Assert.True(targetCalls[3].ApplyTrafficPenalty);
        Assert.False(targetCalls[4].ApplyTrafficPenalty);
    }

    [Fact]
    public void Predict_CallsLapPredictorOncePerDriverPerLapInWindow()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(targetResponseLaps: 3);

        _ = sut.Predict(request);

        var expectedLapCountPerDriver = 6; // r + 3 when r=3
        Assert.Equal(expectedLapCountPerDriver * 2, lapPredictor.Calls.Count);
        Assert.Equal(expectedLapCountPerDriver, lapPredictor.Calls.Count(c => c.DriverCode == "ATT"));
        Assert.Equal(expectedLapCountPerDriver, lapPredictor.Calls.Count(c => c.DriverCode == "TGT"));
    }

    #endregion

    #region Validation warnings and guard behavior
    // These tests exist to ensure model misuse is surfaced clearly through warnings and
    // response-lap guardrails without crashing the prediction flow.

    [Fact]
    public void Predict_TargetResponseLapsLessThanOne_IsClampedAndWarned()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(targetResponseLaps: 0);

        var result = sut.Predict(request);

        Assert.Contains(result.Warnings, w => w.Contains("TargetResponseLaps is 0", StringComparison.Ordinal));
        Assert.Equal(4, result.AttackerLaps.Count); // clamped r=1 => r+3 laps
        Assert.Equal(4, result.TargetLaps.Count);
    }

    [Fact]
    public void Predict_ValidationWarnings_ReportAllConfiguredRiskSignals()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest(
            attackerReferencePace: 0.0,
            targetReferencePace: -1.0,
            pitLoss: -5.0,
            attackerCurrentAge: -1,
            targetCurrentAge: -2,
            attackerReplacementCompound: TyreCompound.Wet,
            targetReplacementCompound: TyreCompound.Intermediate,
            attackerReplacementAge: -3,
            targetReplacementAge: -4);

        var result = sut.Predict(request);

        Assert.Contains(result.Warnings, w => w.Contains("Attacker reference pace", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Target reference pace", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Pit lane loss is negative", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Attacker replacement tyre compound", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Target replacement tyre compound", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Attacker current tyre age is negative", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Target current tyre age is negative", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Attacker replacement tyre age is negative", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("Target replacement tyre age is negative", StringComparison.Ordinal));
    }

    [Fact]
    public void Predict_HealthyInputs_EmitNoWarnings()
    {
        var lapPredictor = new RecordingLapPredictor();
        var sut = new PitSequencePredictor(lapPredictor);
        var request = CreateRequest();

        var result = sut.Predict(request);

        Assert.Empty(result.Warnings);
    }

    #endregion

    private static PredictionRequest CreateRequest(
        int decisionLap = 20,
        int targetResponseLaps = 1,
        double initialGapSeconds = 1.5,
        double attackerReferencePace = 90.0,
        double targetReferencePace = 90.5,
        TyreCompound attackerCurrentCompound = TyreCompound.Medium,
        int attackerCurrentAge = 10,
        TyreCompound targetCurrentCompound = TyreCompound.Hard,
        int targetCurrentAge = 12,
        TyreCompound attackerReplacementCompound = TyreCompound.Soft,
        int attackerReplacementAge = 0,
        TyreCompound targetReplacementCompound = TyreCompound.Medium,
        int targetReplacementAge = 0,
        bool trafficAttacker = false,
        bool trafficTarget = false,
        double trafficPenalty = 0.3,
        double pitLoss = 22.0,
        double marginalThreshold = 0.25)
    {
        var model = new LapModelParameters(
            CompoundOffsetsSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.0,
                [TyreCompound.Medium] = 0.5,
                [TyreCompound.Hard] = 1.0
            },
            DegradationRatesSecondsPerLap: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 0.10,
                [TyreCompound.Medium] = 0.07,
                [TyreCompound.Hard] = 0.04
            },
            WarmUpPenaltiesSeconds: new Dictionary<TyreCompound, double>
            {
                [TyreCompound.Soft] = 1.2,
                [TyreCompound.Medium] = 1.2,
                [TyreCompound.Hard] = 1.2
            },
            PitLaneLossSeconds: pitLoss,
            MarginalThresholdSeconds: marginalThreshold,
            Traffic: new TrafficModelParameters(
                ApplyToAttacker: trafficAttacker,
                ApplyToTarget: trafficTarget,
                PenaltySeconds: trafficPenalty));

        return new PredictionRequest(
            EventName: "Test Event",
            DecisionLap: decisionLap,
            InitialAttackerGapToTargetSeconds: initialGapSeconds,
            Attacker: new DriverPredictionState(
                DriverCode: "ATT",
                DisplayName: "Attacker",
                Position: 2,
                ReferencePaceSeconds: attackerReferencePace,
                CurrentCompound: attackerCurrentCompound,
                CurrentTyreAgeLaps: attackerCurrentAge),
            Target: new DriverPredictionState(
                DriverCode: "TGT",
                DisplayName: "Target",
                Position: 1,
                ReferencePaceSeconds: targetReferencePace,
                CurrentCompound: targetCurrentCompound,
                CurrentTyreAgeLaps: targetCurrentAge),
            AttackerReplacementTyre: new TyreSetSpecification(attackerReplacementCompound, attackerReplacementAge),
            TargetReplacementTyre: new TyreSetSpecification(targetReplacementCompound, targetReplacementAge),
            TargetResponseLaps: targetResponseLaps,
            ModelParameters: model);
    }

    private static LapPredictionBreakdown Breakdown(double total) =>
        new(
            ReferencePaceSeconds: total,
            CompoundOffsetSeconds: 0.0,
            DegradationSeconds: 0.0,
            WarmUpSeconds: 0.0,
            TrafficSeconds: 0.0,
            PitLossSeconds: 0.0);

    private sealed class RecordingLapPredictor : ILapTimePredictor
    {
        private readonly Func<LapPredictionInput, LapPredictionBreakdown> _predictFn;

        public RecordingLapPredictor(Func<LapPredictionInput, LapPredictionBreakdown>? predictFn = null)
        {
            _predictFn = predictFn ?? (_ => Breakdown(total: 10.0));
        }

        public List<LapPredictionInput> Calls { get; } = new();

        public LapPredictionBreakdown Predict(LapPredictionInput input)
        {
            Calls.Add(input);
            return _predictFn(input);
        }
    }
}
