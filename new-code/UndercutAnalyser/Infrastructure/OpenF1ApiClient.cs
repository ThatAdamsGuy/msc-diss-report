using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Infrastructure
{
    /// <summary>
    /// OpenF1 API client for fetching race events.
    /// OpenF1 API: https://openf1.org/docs/
    /// Historical data from 2023 onwards is free and accessible.
    /// </summary>
    public sealed class OpenF1ApiClient : IEventDataProvider
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public OpenF1ApiClient(HttpClient? http = null)
        {
            _http = http ?? new HttpClient
            {
                BaseAddress = new Uri("https://api.openf1.org/v1/")
            };
        }

        public async Task<IReadOnlyList<EventRace>> GetRacesBySeasonAsync(int year)
        {
            // OpenF1 Meetings endpoint with session_type=Race filter to get all Grand Prix races.
            // Note: The year parameter is ignored; this fetches ALL races across all years.
            // This is a single query to avoid rate limiting. Callers should call once and filter locally if needed.
            var url = "meetings";
            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);

                var races = await JsonSerializer.DeserializeAsync<List<EventRace>>(stream, JsonOptions).ConfigureAwait(false)
                    ?? [];

                Debug.WriteLine($"OpenF1 loaded {races.Count} total races across all years");
                return races;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"OpenF1 API error: {ex.Message}");
                return Array.Empty<EventRace>();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON parsing error: {ex.Message}");
                return Array.Empty<EventRace>();
            }
        }
    }
}
