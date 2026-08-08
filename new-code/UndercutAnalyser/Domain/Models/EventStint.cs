using System.Text.Json.Serialization;

namespace UndercutAnalyser.Domain.Models
{
    public sealed record EventStint
    {
        [JsonPropertyName("meeting_key")]
        public int MeetingKey { get; init; }

        [JsonPropertyName("session_key")]
        public int SessionKey { get; init; }

        [JsonPropertyName("stint_number")]
        public int StintNumber { get; init; }

        [JsonPropertyName("driver_number")]
        public int DriverNumber { get; init; }

        [JsonPropertyName("lap_start")]
        public int LapStart { get; init; }

        [JsonPropertyName("lap_end")]
        public int? LapEnd { get; init; }

        [JsonPropertyName("compound")]
        public string Compound { get; init; } = string.Empty;

        [JsonPropertyName("tyre_age_at_start")]
        public int TyreAgeAtStart { get; init; }
    }
}
