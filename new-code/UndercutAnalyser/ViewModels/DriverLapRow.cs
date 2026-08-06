using System.Collections.Generic;
using System.Text.Json;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.ViewModels
{
    public sealed class DriverLapRow
    {
        public string DriverName { get; }
        private readonly Dictionary<int, EventLap> _lapsByNumber;

        public DriverLapRow(string driverName, Dictionary<int, EventLap> lapsByNumber)
        {
            DriverName = driverName;
            _lapsByNumber = lapsByNumber;
        }

        // Indexer used by DataGrid bindings.
        // Keys:
        // - "<lap number>" -> lap duration (3 d.p.)
        // - "detail:<lap number>" -> full lap JSON for tooltip
        public string this[string lapKey]
        {
            get
            {
                if (lapKey.StartsWith("detail:", System.StringComparison.OrdinalIgnoreCase))
                {
                    var detailKey = lapKey.Substring("detail:".Length);
                    if (int.TryParse(detailKey, out var detailLap))
                    {
                        return GetLapDetail(detailLap);
                    }
                    return string.Empty;
                }

                if (!int.TryParse(lapKey, out var lap)) return string.Empty;
                if (_lapsByNumber.TryGetValue(lap, out var el) && el.LapDuration != null)
                {
                    return el.LapDuration.Value.ToString("F3");
                }
                return string.Empty;
            }
        }

        public string GetLapDetail(int lapNumber)
        {
            if (_lapsByNumber.TryGetValue(lapNumber, out var el))
            {
                return JsonSerializer.Serialize(el, new JsonSerializerOptions { WriteIndented = true });
            }
            return string.Empty;
        }
    }
}
