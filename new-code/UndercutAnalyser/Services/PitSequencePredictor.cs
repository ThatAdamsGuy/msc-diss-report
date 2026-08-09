using System;
using System.Collections.Generic;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services
{
    /// <summary>
    /// Runs both drivers lap-by-lap through the pit sequence and calculates the three
    /// signed comparison gaps plus the undercut classification.
    ///
    /// Sequence (TargetResponseLaps = r, decision lap = n):
    ///
    ///   Lap  | Attacker                          | Target
    ///   n    | Pit lap (old tyre + full pit loss) | Standard lap (old tyre, no pit loss)
    ///   n+1  | Out lap (new tyre, W + R if set)  | Pit lap if r=1; else standard lap
    ///   n+2  | Standard lap                      | Out lap if r=1; pit lap if r=2; etc.
    ///   ...
    ///   n+r+1| Standard lap                      | Out lap (new tyre, W + R if set)
    ///   n+r+2| Standard lap                      | Standard lap  ← tertiary endpoint
    ///
    /// The three comparison gaps are calculated using:
    ///   G(n) = G0 + attackerElapsed − targetElapsed
    ///
    ///   GapAtN1 — end of lap n+1  (primary)
    ///   GapAtN2 — end of lap n+r+1 (target out lap done)
    ///   GapAtN3 — end of lap n+r+2 (both on first normal lap)
    ///
    /// Sign convention: negative = attacker ahead.
    /// </summary>
    public sealed class PitSequencePredictor : IPitSequencePredictor
    {
        private readonly ILapTimePredictor _lapPredictor;

        public PitSequencePredictor(ILapTimePredictor lapPredictor)
        {
            _lapPredictor = lapPredictor;
        }

        public PredictionResult Predict(PredictionRequest req)
        {
            var warnings = new List<string>();
            Validate(req, warnings);

            var p = req.ModelParameters;
            int n = req.DecisionLap;
            int r = req.TargetResponseLaps;

            // Key lap numbers
            int attackerPitLap  = n;
            int attackerOutLap  = n + 1;
            int targetPitLap    = n + r;
            int targetOutLap    = n + r + 1;

            // Comparison endpoints
            int primaryEndLap   = n + 1;           // GapAtN1
            int secondaryEndLap = n + r + 1;       // GapAtN2 — target out lap done
            int tertiaryEndLap  = n + r + 2;       // GapAtN3 — both on normal lap

            // Running tyre state for each driver
            var attackerCompound = req.Attacker.CurrentCompound;
            var attackerAge      = req.Attacker.CurrentTyreAgeLaps;
            var targetCompound   = req.Target.CurrentCompound;
            var targetAge        = req.Target.CurrentTyreAgeLaps;

            // Cumulative predicted time within the prediction window
            double attackerElapsed = 0.0;
            double targetElapsed   = 0.0;

            var attackerLaps = new List<PredictedLap>();
            var targetLaps   = new List<PredictedLap>();

            double gapAtN1 = double.NaN;
            double gapAtN2 = double.NaN;
            double gapAtN3 = double.NaN;

            for (int lap = n; lap <= tertiaryEndLap; lap++)
            {
                // ── Attacker ─────────────────────────────────────────────────────────
                bool attackerIsPitLap  = lap == attackerPitLap;
                bool attackerIsOutLap  = lap == attackerOutLap;

                // On the out lap, switch to the replacement tyre
                if (attackerIsOutLap)
                {
                    attackerCompound = req.AttackerReplacementTyre.Compound;
                    attackerAge      = req.AttackerReplacementTyre.InitialAgeLaps;
                }

                var attackerBreakdown = _lapPredictor.Predict(new LapPredictionInput(
                    DriverCode:         req.Attacker.DriverCode,
                    ReferencePaceSeconds: req.Attacker.ReferencePaceSeconds,
                    Compound:           attackerCompound,
                    TyreAgeAtStart:     attackerAge,
                    IsPitLap:           attackerIsPitLap,
                    IsOutLap:           attackerIsOutLap,
                    ApplyTrafficPenalty: attackerIsOutLap && p.Traffic.ApplyToAttacker,
                    ModelParameters:    p));

                attackerElapsed += attackerBreakdown.TotalSeconds;

                attackerLaps.Add(new PredictedLap(
                    DriverCode:                    req.Attacker.DriverCode,
                    LapNumber:                     lap,
                    IsPitLap:                      attackerIsPitLap,
                    IsOutLap:                      attackerIsOutLap,
                    Compound:                      attackerCompound,
                    TyreAgeAtStart:                attackerAge,
                    Breakdown:                     attackerBreakdown,
                    CumulativePredictionTimeSeconds: attackerElapsed));

                // Increment attacker tyre age (after the lap is complete)
                attackerAge++;

                // ── Target ───────────────────────────────────────────────────────────
                bool targetIsPitLap  = lap == targetPitLap;
                bool targetIsOutLap  = lap == targetOutLap;

                // On the out lap, switch to the replacement tyre
                if (targetIsOutLap)
                {
                    targetCompound = req.TargetReplacementTyre.Compound;
                    targetAge      = req.TargetReplacementTyre.InitialAgeLaps;
                }

                var targetBreakdown = _lapPredictor.Predict(new LapPredictionInput(
                    DriverCode:         req.Target.DriverCode,
                    ReferencePaceSeconds: req.Target.ReferencePaceSeconds,
                    Compound:           targetCompound,
                    TyreAgeAtStart:     targetAge,
                    IsPitLap:           targetIsPitLap,
                    IsOutLap:           targetIsOutLap,
                    ApplyTrafficPenalty: targetIsOutLap && p.Traffic.ApplyToTarget,
                    ModelParameters:    p));

                targetElapsed += targetBreakdown.TotalSeconds;

                targetLaps.Add(new PredictedLap(
                    DriverCode:                    req.Target.DriverCode,
                    LapNumber:                     lap,
                    IsPitLap:                      targetIsPitLap,
                    IsOutLap:                      targetIsOutLap,
                    Compound:                      targetCompound,
                    TyreAgeAtStart:                targetAge,
                    Breakdown:                     targetBreakdown,
                    CumulativePredictionTimeSeconds: targetElapsed));

                // Increment target tyre age (after the lap is complete)
                targetAge++;

                // ── Gap calculations ─────────────────────────────────────────────────
                // G(n) = G0 + attackerElapsed - targetElapsed
                // Negative = attacker ahead.
                double gap = req.InitialAttackerGapToTargetSeconds + attackerElapsed - targetElapsed;

                if (lap == primaryEndLap)   gapAtN1 = gap;
                if (lap == secondaryEndLap) gapAtN2 = gap;
                if (lap == tertiaryEndLap)  gapAtN3 = gap;
            }

            // Classify at the core pit-sequence endpoint: when the target out lap is complete.
            // This is always GapAtN2 in this predictor (works for r=1 and r>1).
            var classificationGap = gapAtN2;
            var classification = Classify(classificationGap, p.MarginalThresholdSeconds);

            double g0 = req.InitialAttackerGapToTargetSeconds;

            return new PredictionResult(
                AttackerLaps:             attackerLaps,
                TargetLaps:               targetLaps,
                InitialGapSeconds:        g0,
                GapAtN1Seconds:           gapAtN1,
                GapAtN2Seconds:           gapAtN2,
                GapAtN3Seconds:           gapAtN3,
                DeltaGAtN1Seconds:        gapAtN1 - g0,
                DeltaGAtN2Seconds:        gapAtN2 - g0,
                DeltaGAtN3Seconds:        gapAtN3 - g0,
                Classification:           classification,
                MarginalThresholdSeconds: p.MarginalThresholdSeconds,
                Warnings:                 warnings);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static UndercutClassification Classify(double gap, double threshold)
        {
            if (double.IsNaN(gap)) return UndercutClassification.PredictedBehind;

            if (gap < -threshold)  return UndercutClassification.PredictedAhead;
            if (gap >  threshold)  return UndercutClassification.PredictedBehind;
            return UndercutClassification.PredictedMarginal;
        }

        private static void Validate(PredictionRequest req, List<string> warnings)
        {
            if (req.TargetResponseLaps < 1)
            {
                warnings.Add($"TargetResponseLaps is {req.TargetResponseLaps}; must be at least 1. Treating as 1.");
            }

            if (req.Attacker.ReferencePaceSeconds <= 0)
            {
                warnings.Add($"Attacker reference pace is {req.Attacker.ReferencePaceSeconds:F3} s — must be positive.");
            }

            if (req.Target.ReferencePaceSeconds <= 0)
            {
                warnings.Add($"Target reference pace is {req.Target.ReferencePaceSeconds:F3} s — must be positive.");
            }

            if (req.ModelParameters.PitLaneLossSeconds < 0)
            {
                warnings.Add("Pit lane loss is negative — this is unusual and likely a configuration error.");
            }

            if (!req.AttackerReplacementTyre.Compound.IsDryCompound())
            {
                warnings.Add($"Attacker replacement tyre compound ({req.AttackerReplacementTyre.Compound}) is not a dry compound. Prediction may be unreliable.");
            }

            if (!req.TargetReplacementTyre.Compound.IsDryCompound())
            {
                warnings.Add($"Target replacement tyre compound ({req.TargetReplacementTyre.Compound}) is not a dry compound. Prediction may be unreliable.");
            }

            if (req.Attacker.CurrentTyreAgeLaps < 0)
            {
                warnings.Add("Attacker current tyre age is negative — treating as 0.");
            }

            if (req.Target.CurrentTyreAgeLaps < 0)
            {
                warnings.Add("Target current tyre age is negative — treating as 0.");
            }
        }
    }
}
