using System;
using System.Text.Json.Serialization;

namespace UndercutAnalyser.Domain.Models
{
    /// <summary>
    /// Meeting-level metadata (event name, circuit, country and date window) from OpenF1.
    /// In this tool a meeting corresponds to a race weekend/event.
    /// </summary>
    public sealed record EventMeeting
    {
        [JsonPropertyName("meeting_key")]
        public int MeetingKey { get; init; }

        [JsonPropertyName("meeting_name")]
        public string MeetingName { get; init; } = string.Empty;

        [JsonPropertyName("meeting_official_name")]
        public string MeetingOfficialName { get; init; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; init; } = string.Empty;

        [JsonPropertyName("country_key")]
        public int CountryKey { get; init; }

        [JsonPropertyName("country_code")]
        public string CountryCode { get; init; } = string.Empty;

        [JsonPropertyName("country_name")]
        public string CountryName { get; init; } = string.Empty;

        [JsonPropertyName("country_flag")]
        public string CountryFlag { get; init; } = string.Empty;

        [JsonPropertyName("circuit_key")]
        public int CircuitKey { get; init; }

        [JsonPropertyName("circuit_short_name")]
        public string CircuitShortName { get; init; } = string.Empty;

        [JsonPropertyName("circuit_type")]
        public string CircuitType { get; init; } = string.Empty;

        [JsonPropertyName("circuit_info_url")]
        public string CircuitInfoUrl { get; init; } = string.Empty;

        [JsonPropertyName("circuit_image")]
        public string CircuitImage { get; init; } = string.Empty;

        [JsonPropertyName("gmt_offset")]
        public string GmtOffset { get; init; } = string.Empty;

        [JsonPropertyName("date_start")]
        public DateTime? DateStart { get; init; }

        [JsonPropertyName("date_end")]
        public DateTime? DateEnd { get; init; }

        [JsonPropertyName("year")]
        public int Year { get; init; }

        [JsonPropertyName("is_cancelled")]
        public bool IsCancelled { get; init; }

        // Convenience properties for UI binding
        /// <summary>Display race name used in selectors and headings.</summary>
        public string RaceName => MeetingOfficialName;

        /// <summary>Short circuit label for compact UI display.</summary>
        public string CircuitName => CircuitShortName;

        /// <summary>Primary event date shown in UI lists (meeting start date).</summary>
        public DateTime? Date => DateStart != default ? DateStart : null;
    }
}
