using System.Text.Json.Serialization;

namespace UndercutAnalyser.Domain.Models
{
    public sealed record Driver
    {
        [JsonPropertyName("driver_key")]
        public int DriverKey { get; init; }

        [JsonPropertyName("driver_number")]
        public int DriverNumber { get; init; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; init; } = string.Empty;

        [JsonPropertyName("last_name")]
        public string LastName { get; init; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; init; } = string.Empty;

        [JsonPropertyName("broadcast_name")]
        public string BroadcastName { get; init; } = string.Empty;

        [JsonPropertyName("team_name")]
        public string TeamName { get; init; } = string.Empty;

        [JsonPropertyName("team_colour")]
        public string TeamColour { get; init; } = string.Empty;

        public string FullName => string.IsNullOrEmpty(FirstName) ? LastName : FirstName + " " + LastName;
    }
}