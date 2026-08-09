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
    ///   Lap  | Attacking driver                 | Target driver
    ///   n    | Pit lap (old tyre + full pit loss) | Standard lap (old tyre, no pit loss)
    ///   n+1  | Out lap (new tyre, W + R if set)  | Pit lap if r=1; else standard lap
    ///   n+2  | Standard lap                      | Out lap if r=1; pit lap if r=2; etc.
    ///   ...
    ///   n+r+1| Standard lap                      | Out lap (new tyre, W + R if set)
    ///   n+r+2| Standard lap                      | Standard lap  ← both drivers normal lap complete endpoint
    ///
    /// The three comparison gaps are calculated using:
    ///   G(n) = G0 + attackerElapsed − targetElapsed
    ///
    ///   GapAtTargetPitLapCompleteSeconds         — TargetPitLapComplete: end of lap n+r
    ///   GapAtTargetOutLapCompleteSeconds         — TargetOutLapComplete: end of lap n+r+1
    ///   GapAtBothDriversNormalLapCompleteSeconds — BothDriversNormalLapComplete: end of lap n+r+2
    ///
    /// Sign convention: negative = attacking driver ahead.
    /// </summary>
    public sealed class PitSequencePredictor : IPitSequencePredictor
    {
        private readonly ILapTimePredictor _lapPredictor;

        /// <summary>
        /// Creates the sequence predictor with a lap-time predictor dependency.
        /// </summary>
        public PitSequencePredictor(ILapTimePredictor lapPredictor)
        {
            _lapPredictor = lapPredictor;
        }

        /// <summary>
        /// Runs the full attacking-driver/target-driver pit sequence from lap n through the
        /// BothDriversNormalLapComplete endpoint and returns gaps, gain/loss deltas,
        /// classification, and warnings.
        /// </summary>
        public PredictionResult Predict(PredictionRequest req)
        {
            var warnings = new List<string>();
            Validate(req, warnings);

            var p = req.ModelParameters;
            int n = req.DecisionLap;
            int r = Math.Max(1, req.TargetResponseLaps);

            // Key lap numbers
            int attackerPitLap  = n;
            int attackerOutLap  = n + 1;
            int targetPitLap    = n + r;
            int targetOutLap    = n + r + 1;

            // Comparison endpoints
            // TargetPitLapComplete:         end of target pit lap (n + r)
            // TargetOutLapComplete:         end of target out lap (n + r + 1)
            // BothDriversNormalLapComplete: following normal lap  (n + r + 2)
            int targetPitLapCompleteLap = targetPitLap;
            int targetOutLapCompleteLap = targetOutLap;
            int bothDriversNormalLapCompleteLap = targetOutLap + 1;

            // Running tyre state for each driver
            var attackerCompound = req.Attacker.CurrentCompound;
            var attackerAge      = Math.Max(0, req.Attacker.CurrentTyreAgeLaps);
            var targetCompound   = req.Target.CurrentCompound;
            var targetAge        = Math.Max(0, req.Target.CurrentTyreAgeLaps);

            // Cumulative predicted time within the prediction window
            double attackerElapsed = 0.0;
            double targetElapsed   = 0.0;

            var attackerLaps = new List<PredictedLap>();
            var targetLaps   = new List<PredictedLap>();

            double gapAtTargetPitLapComplete = double.NaN;
            double gapAtTargetOutLapComplete = double.NaN;
            double gapAtBothDriversNormalLapComplete = double.NaN;

            for (int lap = n; lap <= bothDriversNormalLapCompleteLap; lap++)
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

                if (lap == targetPitLapCompleteLap)         gapAtTargetPitLapComplete = gap;
                if (lap == targetOutLapCompleteLap)         gapAtTargetOutLapComplete = gap;
                if (lap == bothDriversNormalLapCompleteLap) gapAtBothDriversNormalLapComplete = gap;
            }

            // Classify at TargetPitLapComplete: end of target pit lap (n + r).
            // For r=1 this is n+1 (the standard undercut success point).
            var classificationGap = gapAtTargetPitLapComplete;
            var classification = Classify(classificationGap, p.MarginalThresholdSeconds);

            double g0 = req.InitialAttackerGapToTargetSeconds;

            return new PredictionResult(
                AttackerLaps:                               attackerLaps,
                TargetLaps:                                 targetLaps,
                InitialGapSeconds:                          g0,
                GapAtTargetPitLapCompleteSeconds:           gapAtTargetPitLapComplete,
                GapAtTargetOutLapCompleteSeconds:           gapAtTargetOutLapComplete,
                GapAtBothDriversNormalLapCompleteSeconds:   gapAtBothDriversNormalLapComplete,
                DeltaGAtTargetPitLapCompleteSeconds:        gapAtTargetPitLapComplete - g0,
                DeltaGAtTargetOutLapCompleteSeconds:        gapAtTargetOutLapComplete - g0,
                DeltaGAtBothDriversNormalLapCompleteSeconds: gapAtBothDriversNormalLapComplete - g0,
                Classification:                             classification,
                MarginalThresholdSeconds:                   p.MarginalThresholdSeconds,
                Warnings:                                   warnings);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a signed primary gap into Ahead/Marginal/Behind using a symmetric
        /// marginal threshold around zero.
        /// </summary>
        private static UndercutClassification Classify(double gap, double threshold)
        {
            if (double.IsNaN(gap)) return UndercutClassification.PredictedBehind;

            if (gap < -threshold)  return UndercutClassification.PredictedAhead;
            if (gap >  threshold)  return UndercutClassification.PredictedBehind;
            return UndercutClassification.PredictedMarginal;
        }

        /// <summary>
        /// Performs lightweight input sanity checks and records warnings for values that
        /// are unusual or outside the intended modelling envelope.
        /// </summary>
        private static void Validate(PredictionRequest req, List<string> warnings)
        {
            if (req.TargetResponseLaps < 1)
            {
                warnings.Add($"TargetResponseLaps is {req.TargetResponseLaps}; must be at least 1. Clamped to 1.");
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
                warnings.Add("Attacker current tyre age is negative — clamped to 0.");
            }

            if (req.Target.CurrentTyreAgeLaps < 0)
            {
                warnings.Add("Target current tyre age is negative — clamped to 0.");
            }
        }
    }
}
