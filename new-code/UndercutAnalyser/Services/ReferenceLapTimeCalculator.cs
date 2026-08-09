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
        int ExcludedSafetyCarLaps,
        double FuelEffectPerLapSeconds,
        double SumLapTimeSeconds,
        double? AverageLapTimeSeconds,
        IReadOnlyList<TimeWindow> SafetyCarWindows);

    /// <summary>
    /// Computes a constant reference lap from historical session laps after excluding
    /// pit-out and SC/VSC-affected laps, with optional fuel correction.
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
            var windows = BuildSafetyCarWindows(raceControlMessages);

            var totalLapRows = laps.Count;
            var maxSessionLapNumber = laps.Count > 0 ? laps.Max(l => l.LapNumber) : 0;
            var totalFuelEffectSeconds = secondsPer10Kg * (fuelKg / 10.0);
            var fuelEffectPerLapSeconds = maxSessionLapNumber > 0 ? totalFuelEffectSeconds / maxSessionLapNumber : 0.0;

            var includedLaps = 0;
            var excludedPitOutLaps = 0;
            var excludedSafetyCarLaps = 0;
            var sumLapTimeSeconds = 0.0;

            foreach (var lap in laps)
            {
                if (lap.IsPitOutLap)
                {
                    excludedPitOutLaps++;
                    continue;
                }

                if (lap.LapDuration is null || lap.DateStart is null)
                {
                    continue;
                }

                var lapStartUtc = AsUtc(lap.DateStart.Value);
                var lapEndUtc = lapStartUtc.AddSeconds(lap.LapDuration.Value);

                if (IsInAnyWindow(lapStartUtc, lapEndUtc, windows))
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
                ExcludedSafetyCarLaps: excludedSafetyCarLaps,
                FuelEffectPerLapSeconds: fuelEffectPerLapSeconds,
                SumLapTimeSeconds: sumLapTimeSeconds,
                AverageLapTimeSeconds: average,
                SafetyCarWindows: windows);
        }

        /// <summary>
        /// Converts race-control SC/VSC messages into UTC active windows for lap filtering.
        /// </summary>
        private static IReadOnlyList<TimeWindow> BuildSafetyCarWindows(IReadOnlyList<RaceControlMessage> raceControlMessages)
        {
            var windows = new List<TimeWindow>();
            DateTime? vscStart = null;
            DateTime? scStart = null;

            foreach (var message in raceControlMessages
                         .Where(m => string.Equals(m.Category, "SafetyCar", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(m.Category, "Safety Car", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(m => m.Date))
            {
                if (message.Date is null)
                {
                    continue;
                }

                var timestamp = AsUtc(message.Date.Value);
                var text = (message.Message ?? string.Empty).Trim().ToUpperInvariant();

                if (text.Contains("VSC DEPLOYED", StringComparison.Ordinal))
                {
                    vscStart = timestamp;
                    continue;
                }

                if (text.Contains("VSC ENDING", StringComparison.Ordinal))
                {
                    if (vscStart.HasValue)
                    {
                        windows.Add(new TimeWindow(vscStart.Value, timestamp));
                        vscStart = null;
                    }

                    continue;
                }

                if (text.Contains("SAFETY CAR DEPLOYED", StringComparison.Ordinal))
                {
                    scStart = timestamp;
                    continue;
                }

                if (text.Contains("SAFETY CAR IN THIS LAP", StringComparison.Ordinal))
                {
                    if (scStart.HasValue)
                    {
                        windows.Add(new TimeWindow(scStart.Value, timestamp));
                        scStart = null;
                    }
                }
            }

            var maxKnownDate = raceControlMessages
                .Where(m => m.Date.HasValue)
                .Select(m => AsUtc(m.Date!.Value))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            if (vscStart.HasValue)
            {
                windows.Add(new TimeWindow(vscStart.Value, maxKnownDate));
            }

            if (scStart.HasValue)
            {
                windows.Add(new TimeWindow(scStart.Value, maxKnownDate));
            }

            return windows;
        }

        /// <summary>
        /// Returns true when any part of the lap interval overlaps an SC/VSC window.
        /// </summary>
        private static bool IsInAnyWindow(DateTime lapStartUtc, DateTime lapEndUtc, IReadOnlyList<TimeWindow> windows)
        {
            foreach (var window in windows)
            {
                if (lapStartUtc <= window.EndUtc
                    && lapEndUtc >= window.StartUtc)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Normalises incoming DateTime values to UTC so all overlap checks are consistent.
        /// </summary>
        private static DateTime AsUtc(DateTime input)
        {
            if (input.Kind == DateTimeKind.Utc)
            {
                return input;
            }

            if (input.Kind == DateTimeKind.Local)
            {
                return input.ToUniversalTime();
            }

            return DateTime.SpecifyKind(input, DateTimeKind.Utc);
        }
    }
}
