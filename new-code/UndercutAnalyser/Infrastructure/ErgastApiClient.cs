using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Infrastructure
{
    /// <summary>
    /// Minimal Ergast API client for fetching race events per season.
    /// Ergast API: http://ergast.com/mrd/
    /// </summary>
    public sealed class ErgastApiClient : IEventDataProvider
    {
        private readonly HttpClient _http;

        public ErgastApiClient(HttpClient? http = null)
        {
            _http = http ?? new HttpClient
            {
                BaseAddress = new Uri("http://ergast.com/api/f1/")
            };
        }

        public async Task<IReadOnlyList<EventRace>> GetRacesBySeasonAsync(int year)
        {
            // Ergast returns races for a season at /{season}.json
            var url = $"{year}.json?limit=100";
            using var resp = await _http.GetAsync(url).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);

            try
            {
                using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                var races = new List<EventRace>();

                // navigate to MRData -> RaceTable -> Races
                if (doc.RootElement.TryGetProperty("MRData", out var mrData)
                    && mrData.TryGetProperty("RaceTable", out var raceTable)
                    && raceTable.TryGetProperty("Races", out var racesEl)
                )
                {
                    foreach (var r in racesEl.EnumerateArray())
                    {
                        var race = new EventRace();
                        if (r.TryGetProperty("season", out var seasonEl) && seasonEl.ValueKind == JsonValueKind.String && int.TryParse(seasonEl.GetString(), out var s))
                            race = race with { Year = s };
                        if (r.TryGetProperty("round", out var roundEl) && roundEl.ValueKind == JsonValueKind.String && int.TryParse(roundEl.GetString(), out var round))
                            race = race with { Round = round };
                        if (r.TryGetProperty("raceName", out var nameEl))
                            race = race with { RaceName = nameEl.GetString() ?? string.Empty };
                        if (r.TryGetProperty("Circuit", out var circuitEl)
                            && circuitEl.TryGetProperty("circuitName", out var circuitNameEl))
                            race = race with { CircuitName = circuitNameEl.GetString() ?? string.Empty };
                        if (r.TryGetProperty("Circuit", out var circ2)
                            && circ2.TryGetProperty("Location", out var locEl)
                            && locEl.TryGetProperty("locality", out var localityEl))
                            race = race with { Location = localityEl.GetString() ?? string.Empty };
                        if (r.TryGetProperty("date", out var dateEl) && DateTime.TryParse(dateEl.GetString(), out var dt))
                            race = race with { Date = dt };
                        if (r.TryGetProperty("url", out var urlEl))
                            race = race with { Url = urlEl.GetString() ?? string.Empty };

                        races.Add(race);
                    }
                }

                return races;
            }
            catch (JsonException)
            {
                return Array.Empty<EventRace>();
            }
        }
    }
}
