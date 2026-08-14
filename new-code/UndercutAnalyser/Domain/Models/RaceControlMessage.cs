using System;
using System.Text.Json.Serialization;

namespace UndercutAnalyser.Domain.Models
{
    public sealed record RaceControlMessage
    {
        [JsonPropertyName("meeting_key")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int MeetingKey { get; init; }

        [JsonPropertyName("session_key")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int SessionKey { get; init; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; init; }

        [JsonPropertyName("driver_number")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? DriverNumber { get; init; }

        [JsonPropertyName("lap_number")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? LapNumber { get; init; }

        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;
    }
}
