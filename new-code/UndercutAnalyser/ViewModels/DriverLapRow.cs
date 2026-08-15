using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.ViewModels
{
    public enum RawDataValueMetric
    {
        LapTime,
        SectorOneTime,
        SectorTwoTime
    }

    /// <summary>
    /// One row in the Raw Data table, exposing lap cells via dynamic string keys.
    /// </summary>
    public sealed class DriverLapRow
    {
        public string DriverName { get; }

        private readonly Dictionary<int, EventLap> _lapsByNumber;
        private readonly Dictionary<int, string> _compoundByLap;
        private readonly HashSet<int> _pitLaps;
        private readonly IReadOnlyList<int> _orderedLapNumbers;
        private readonly bool _isReferenceRow;
        private readonly double? _referenceLapSeconds;
        private readonly bool _cumulativeMode;
        private readonly RawDataValueMetric _metric;

        /// <summary>
        /// Creates a row backed by lap/stint dictionaries for fast per-cell lookup.
        /// </summary>
        public DriverLapRow(
            string driverName,
            Dictionary<int, EventLap> lapsByNumber,
            Dictionary<int, string> compoundByLap,
            HashSet<int> pitLaps,
            IReadOnlyList<int> orderedLapNumbers,
            bool cumulativeMode,
            RawDataValueMetric metric = RawDataValueMetric.LapTime,
            bool isReferenceRow = false,
            double? referenceLapSeconds = null)
        {
            DriverName = driverName;
            _lapsByNumber = lapsByNumber;
            _compoundByLap = compoundByLap;
            _pitLaps = pitLaps;
            _orderedLapNumbers = orderedLapNumbers;
            _isReferenceRow = isReferenceRow;
            _referenceLapSeconds = referenceLapSeconds;
            _cumulativeMode = cumulativeMode;
            _metric = metric;
        }

        // Keys:
        // - "<lap number>" -> lap time value
        // - "detail:<lap number>" -> tooltip
        // - "bg:<lap number>" -> background brush
        // - "border:<lap number>" -> border brush
        // - "thickness:<lap number>" -> border thickness
        public object this[string lapKey]
        {
            get
            {
                if (lapKey.StartsWith("detail:", StringComparison.OrdinalIgnoreCase))
                {
                    var detailKey = lapKey.Substring("detail:".Length);
                    return int.TryParse(detailKey, out var detailLap) ? GetLapDetail(detailLap) : string.Empty;
                }

                if (lapKey.StartsWith("bg:", StringComparison.OrdinalIgnoreCase))
                {
                    var bgKey = lapKey.Substring("bg:".Length);
                    return int.TryParse(bgKey, out var bgLap) ? GetBackground(bgLap) : Brushes.Transparent;
                }

                if (lapKey.StartsWith("border:", StringComparison.OrdinalIgnoreCase))
                {
                    var borderKey = lapKey.Substring("border:".Length);
                    return int.TryParse(borderKey, out var borderLap) ? GetBorderBrush(borderLap) : Brushes.Transparent;
                }

                if (lapKey.StartsWith("thickness:", StringComparison.OrdinalIgnoreCase))
                {
                    var thicknessKey = lapKey.Substring("thickness:".Length);
                    return int.TryParse(thicknessKey, out var thicknessLap) ? GetBorderThickness(thicknessLap) : new Thickness(0);
                }

                return int.TryParse(lapKey, out var lap) ? GetLapText(lap) : string.Empty;
            }
        }

        /// <summary>
        /// Returns formatted cell text for a lap (individual or cumulative view).
        /// </summary>
        private string GetLapText(int lapNumber)
        {
            var value = GetLapValue(lapNumber);
            return value.HasValue ? value.Value.ToString("F3") : string.Empty;
        }

        /// <summary>
        /// Returns numeric cell value according to current mode (individual or cumulative).
        /// </summary>
        private double? GetLapValue(int lapNumber)
        {
            if (_cumulativeMode)
            {
                if (!_isReferenceRow && !_lapsByNumber.ContainsKey(lapNumber))
                {
                    return null;
                }

                if (_metric == RawDataValueMetric.LapTime)
                {
                    double cumulativeLapTime = 0;
                    var hasAnyLapTime = false;

                    foreach (var lap in _orderedLapNumbers.Where(l => l <= lapNumber))
                    {
                        var part = GetIndividualLapValue(lap);
                        if (part.HasValue)
                        {
                            cumulativeLapTime += part.Value;
                            hasAnyLapTime = true;
                        }
                    }

                    return hasAnyLapTime ? cumulativeLapTime : null;
                }

                double cumulativeToPriorLap = 0;
                foreach (var lap in _orderedLapNumbers.Where(l => l < lapNumber))
                {
                    if (_lapsByNumber.TryGetValue(lap, out var priorLap) && priorLap.LapDuration.HasValue)
                    {
                        cumulativeToPriorLap += priorLap.LapDuration.Value;
                    }
                }

                var currentPartial = GetIndividualLapValue(lapNumber);
                if (!currentPartial.HasValue)
                {
                    return null;
                }

                return cumulativeToPriorLap + currentPartial.Value;
            }

            return GetIndividualLapValue(lapNumber);
        }

        /// <summary>
        /// Returns single-lap value either from reference row constant or raw lap duration.
        /// </summary>
        private double? GetIndividualLapValue(int lapNumber)
        {
            if (_isReferenceRow)
            {
                return _metric == RawDataValueMetric.LapTime ? _referenceLapSeconds : null;
            }

            if (!_lapsByNumber.TryGetValue(lapNumber, out var lap))
            {
                return null;
            }

            return _metric switch
            {
                RawDataValueMetric.LapTime => lap.LapDuration,
                RawDataValueMetric.SectorOneTime => lap.DurationSector1,
                RawDataValueMetric.SectorTwoTime => lap.LapTimeAtSectorTwoLine,
                _ => lap.LapDuration
            };
        }

        /// <summary>
        /// Builds tooltip detail text for a lap cell, including raw lap JSON and inferred metadata.
        /// </summary>
        public string GetLapDetail(int lapNumber)
        {
            if (_isReferenceRow)
            {
                var lapValue = GetLapValue(lapNumber);
                return lapValue.HasValue
                    ? $"Reference Lap\nLap {lapNumber}: {lapValue.Value:F3} s"
                    : "Reference Lap";
            }

            if (_lapsByNumber.TryGetValue(lapNumber, out var lap))
            {
                var detail = JsonSerializer.Serialize(lap, new JsonSerializerOptions { WriteIndented = true });

                var compoundText = _compoundByLap.TryGetValue(lapNumber, out var compound) && !string.IsNullOrWhiteSpace(compound)
                    ? compound
                    : "Unknown";

                var pitText = _pitLaps.Contains(lapNumber) ? "Yes" : "No";

                return $"{detail}\n\nMatched compound: {compoundText}\nInferred pit lap: {pitText}";
            }

            return string.Empty;
        }

        /// <summary>
        /// Chooses cell background (reference row shading and pit-lap highlighting).
        /// </summary>
        private Brush GetBackground(int lapNumber)
        {
            if (_isReferenceRow)
            {
                return Brushes.Gainsboro;
            }

            return _pitLaps.Contains(lapNumber) ? Brushes.Orange : Brushes.Transparent;
        }

        /// <summary>
        /// Chooses tyre-compound border colour for a lap cell.
        /// </summary>
        private Brush GetBorderBrush(int lapNumber)
        {
            if (_compoundByLap.TryGetValue(lapNumber, out var compound))
            {
                if (string.IsNullOrWhiteSpace(compound) || string.Equals(compound, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.Black;
                }

                if (string.Equals(compound, "SOFT", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.Red;
                }

                if (string.Equals(compound, "MEDIUM", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.Yellow;
                }

                if (string.Equals(compound, "HARD", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.White;
                }

                return Brushes.Transparent;
            }

            return Brushes.Transparent;
        }

        /// <summary>
        /// Returns border thickness to visually emphasise laps with known compounds.
        /// </summary>
        private Thickness GetBorderThickness(int lapNumber)
        {
            if (_compoundByLap.ContainsKey(lapNumber))
            {
                return new Thickness(2);
            }

            return new Thickness(0);
        }
    }
}
