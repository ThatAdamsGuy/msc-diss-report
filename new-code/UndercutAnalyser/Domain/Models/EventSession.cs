using System;
using System.Text.Json.Serialization;

namespace UndercutAnalyser.Domain.Models
{
    public sealed record EventSession
    {
        [JsonPropertyName("session_key")]
        public int SessionKey { get; init; }

        [JsonPropertyName("session_type")]
        public string SessionType { get; init; } = string.Empty;

        [JsonPropertyName("session_name")]
        public string SessionName { get; init; } = string.Empty;

        [JsonPropertyName("date_start")]
        public DateTime? DateStart { get; init; }

        [JsonPropertyName("date_end")]
        public DateTime? DateEnd { get; init; }

        [JsonPropertyName("meeting_key")]
        public int MeetingKey { get; init; }

        [JsonPropertyName("circuit_key")]
        public int CircuitKey { get; init; }

        [JsonPropertyName("circuit_short_name")]
        public string CircuitShortName { get; init; } = string.Empty;

        [JsonPropertyName("country_key")]
        public int CountryKey { get; init; }

        [JsonPropertyName("country_code")]
        public string CountryCode { get; init; } = string.Empty;

        [JsonPropertyName("country_name")]
        public string CountryName { get; init; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; init; } = string.Empty;

        [JsonPropertyName("gmt_offset")]
        public string GmtOffset { get; init; } = string.Empty;

        [JsonPropertyName("year")]
        public int Year { get; init; }

        [JsonPropertyName("is_cancelled")]
        public bool IsCancelled { get; init; }
    }
}
