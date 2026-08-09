using System;
using System.Text.Json.Serialization;

namespace UndercutAnalyser.Domain.Models
{
    /// <summary>
    /// One lap timing record for one driver from OpenF1.
    /// This is raw session data used to derive decision-state inputs (gaps, tyre state, pace).
    /// </summary>
    public sealed record EventLap
    {
        [JsonPropertyName("meeting_key")]
        public int MeetingKey { get; init; }

        [JsonPropertyName("session_key")]
        public int SessionKey { get; init; }

        [JsonPropertyName("driver_number")]
        public int DriverNumber { get; init; }

        [JsonPropertyName("lap_number")]
        public int LapNumber { get; init; }

        [JsonPropertyName("date_start")]
        public DateTime? DateStart { get; init; }

        [JsonPropertyName("duration_sector_1")]
        public float? DurationSector1 { get; init; }

        [JsonPropertyName("duration_sector_2")]
        public float? DurationSector2 { get; init; }

        [JsonPropertyName("duration_sector_3")]
        public float? DurationSector3 { get; init; }

        /// <summary>
        /// Elapsed time from lap start to Sector Line Two crossing (S1 + S2).
        /// Used as the decision-point timestamp when deriving initial attacker-target gap.
        /// </summary>
        public float? LapTimeAtSectorTwoLine { get => DurationSector1 + DurationSector2; }

        [JsonPropertyName("i1_speed")]
        public int? I1Speed { get; init; }

        [JsonPropertyName("i2_speed")]
        public int? I2Speed { get; init; }

        [JsonPropertyName("is_pit_out_lap")]
        public bool IsPitOutLap { get; init; }

        [JsonPropertyName("lap_duration")]
        public float? LapDuration { get; init; }

        [JsonPropertyName("segments_sector_1")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int?[] SegmentsSector1 { get; init; } = [];

        [JsonPropertyName("segments_sector_2")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int?[] SegmentsSector2 { get; init; } = [];

        [JsonPropertyName("segments_sector_3")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int?[] SegmentsSector3 { get; init; } = [];

        [JsonPropertyName("st_speed")]
        public int? StSpeed { get; init; }
    }
}
