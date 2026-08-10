using System;
using System.Collections.Generic;
using System.Linq;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services
{
    /// <summary>
    /// Inclusive UTC time window used to represent SC/VSC active intervals.
    /// </summary>
    public sealed record TimeWindow(DateTime StartUtc, DateTime EndUtc);

    /// <summary>
    /// Summary output of reference-lap derivation, including diagnostics on excluded laps.
    /// </summary>
    public sealed record ReferenceLapTimeResult(
        int TotalLapRows,
        int MaxSessionLapNumber,
        int IncludedLaps,
        int ExcludedPitOutLaps,
        int ExcludedPitInLaps,
        int ExcludedSafetyCarLaps,
        double FuelEffectPerLapSeconds,
        double SumLapTimeSeconds,
        double? AverageLapTimeSeconds,
        IReadOnlyList<TimeWindow> SafetyCarWindows);

    /// <summary>
    /// Computes a constant reference lap from historical session laps after excluding
    /// pit-out, pit-in, and SC/VSC-affected laps, with optional fuel correction.
    /// </summary>
    public static class ReferenceLapTimeCalculator
    {
        /// <summary>
        /// Builds the reference-lap result used by race-trace visualisation and default
        /// pace baselining.
        /// </summary>
        public static ReferenceLapTimeResult Calculate(
            IReadOnlyList<EventLap> laps,
            IReadOnlyList<RaceControlMessage> raceControlMessages,
            double secondsPer10Kg,
            double fuelKg)
        {
            var windows = RaceTimingDomainLogic.BuildSafetyCarWindows(raceControlMessages);

            var totalLapRows = laps.Count;
            var maxSessionLapNumber = laps.Count > 0 ? laps.Max(l => l.LapNumber) : 0;
            var totalFuelEffectSeconds = secondsPer10Kg * (fuelKg / 10.0);
            var fuelEffectPerLapSeconds = maxSessionLapNumber > 0 ? totalFuelEffectSeconds / maxSessionLapNumber : 0.0;

            var includedLaps = 0;
            var excludedPitOutLaps = 0;
            var excludedPitInLaps = 0;
            var excludedSafetyCarLaps = 0;
            var sumLapTimeSeconds = 0.0;

            var pitInLaps = RaceTimingDomainLogic.BuildPitInLapLookup(laps);

            foreach (var lap in laps)
            {
                if (lap.IsPitOutLap)
                {
                    excludedPitOutLaps++;
                    continue;
                }

                if (pitInLaps.Contains((lap.DriverNumber, lap.LapNumber)))
                {
                    excludedPitInLaps++;
                    continue;
                }

                if (lap.LapDuration is null || lap.DateStart is null)
                {
                    continue;
                }

                var lapStartUtc = RaceTimingDomainLogic.AsUtc(lap.DateStart.Value);
                var lapEndUtc = lapStartUtc.AddSeconds(lap.LapDuration.Value);

                if (RaceTimingDomainLogic.IsInAnyWindow(lapStartUtc, lapEndUtc, windows))
                {
                    excludedSafetyCarLaps++;
                    continue;
                }

                var fuelAdjustmentSeconds = fuelEffectPerLapSeconds * Math.Max(0, maxSessionLapNumber - lap.LapNumber);
                sumLapTimeSeconds += lap.LapDuration.Value - fuelAdjustmentSeconds;
                includedLaps++;
            }

            double? average = includedLaps > 0 ? sumLapTimeSeconds / includedLaps : null;

            return new ReferenceLapTimeResult(
                TotalLapRows: totalLapRows,
                MaxSessionLapNumber: maxSessionLapNumber,
                IncludedLaps: includedLaps,
                ExcludedPitOutLaps: excludedPitOutLaps,
                ExcludedPitInLaps: excludedPitInLaps,
                ExcludedSafetyCarLaps: excludedSafetyCarLaps,
                FuelEffectPerLapSeconds: fuelEffectPerLapSeconds,
                SumLapTimeSeconds: sumLapTimeSeconds,
                AverageLapTimeSeconds: average,
                SafetyCarWindows: windows);
        }

    }
}
