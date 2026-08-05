using System;

namespace UndercutAnalyser.Domain.Models
{
    public sealed record EventRace
    {
        public int Year { get; init; }
        public int Round { get; init; }
        public string RaceName { get; init; } = string.Empty;
        public string CircuitName { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public DateTime? Date { get; init; }
        public string Url { get; init; } = string.Empty;

        public override string ToString() => $"{RaceName} ({Year})";
    }
}
