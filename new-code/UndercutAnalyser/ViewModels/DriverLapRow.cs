using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.ViewModels
{
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

        public DriverLapRow(
            string driverName,
            Dictionary<int, EventLap> lapsByNumber,
            Dictionary<int, string> compoundByLap,
            HashSet<int> pitLaps,
            IReadOnlyList<int> orderedLapNumbers,
            bool cumulativeMode,
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

        private string GetLapText(int lapNumber)
        {
            var value = GetLapValue(lapNumber);
            return value.HasValue ? value.Value.ToString("F3") : string.Empty;
        }

        private double? GetLapValue(int lapNumber)
        {
            if (_cumulativeMode)
            {
                if (!_isReferenceRow && !_lapsByNumber.ContainsKey(lapNumber))
                {
                    return null;
                }

                double cumulative = 0;
                var hasAny = false;

                foreach (var lap in _orderedLapNumbers.Where(l => l <= lapNumber))
                {
                    var part = GetIndividualLapValue(lap);
                    if (part.HasValue)
                    {
                        cumulative += part.Value;
                        hasAny = true;
                    }
                }

                return hasAny ? cumulative : null;
            }

            return GetIndividualLapValue(lapNumber);
        }

        private double? GetIndividualLapValue(int lapNumber)
        {
            if (_isReferenceRow)
            {
                return _referenceLapSeconds;
            }

            if (_lapsByNumber.TryGetValue(lapNumber, out var lap) && lap.LapDuration.HasValue)
            {
                return lap.LapDuration.Value;
            }

            return null;
        }

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

                var compoundText = _compoundByLap.TryGetValue(lapNumber, out var compound)
                    ? compound
                    : "Unknown";

                var pitText = _pitLaps.Contains(lapNumber) ? "Yes" : "No";

                return $"{detail}\n\nMatched compound: {compoundText}\nInferred pit lap: {pitText}";
            }

            return string.Empty;
        }

        private Brush GetBackground(int lapNumber)
        {
            if (_isReferenceRow)
            {
                return Brushes.Gainsboro;
            }

            return _pitLaps.Contains(lapNumber) ? Brushes.Orange : Brushes.Transparent;
        }

        private Brush GetBorderBrush(int lapNumber)
        {
            if (_compoundByLap.TryGetValue(lapNumber, out var compound))
            {
                if (compound.Equals("SOFT", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.Red;
                }

                if (compound.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.Yellow;
                }

                if (compound.Equals("HARD", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.White;
                }
            }

            return Brushes.Transparent;
        }

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
