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
    /// Thin HTTP client for OpenF1 endpoints used by this tool.
    /// Methods return empty collections on recoverable API/deserialisation failures and log errors.
    /// </summary>
    public sealed class OpenF1RaceDataClient : IEventDataProvider, IMainWindowEventDataClient
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
                        Converters = { new DateTimeNullableJsonConverter(), new FloatNullableJsonConverter() }
        };

        /// <summary>
        /// Creates the OpenF1 client, optionally reusing a caller-provided HttpClient.
        /// </summary>
        public OpenF1RaceDataClient(HttpClient? http = null)
        {
            _http = http ?? new HttpClient
            {
                BaseAddress = new Uri("https://api.openf1.org/v1/")
            };
        }

        // IEventDataProvider implementation - reuse meetings endpoint
        /// <summary>
        /// Returns all meetings for a season year from OpenF1.
        /// </summary>
        public async Task<IReadOnlyList<EventMeeting>> GetRacesBySeasonAsync(int year)
        {
            var url = "meetings";
            var started = DateTimeOffset.UtcNow;
            LogHttpStart("GetRacesBySeasonAsync", url, $"season {year}");

            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var errorBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    LogHttpFailure("GetRacesBySeasonAsync", url, started, resp, $"season {year}", errorBody);
                }

                resp.EnsureSuccessStatusCode();

                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var races = await JsonSerializer.DeserializeAsync<List<EventMeeting>>(stream, JsonOptions).ConfigureAwait(false)
                    ?? new List<EventMeeting>();

                LogHttpSuccess("GetRacesBySeasonAsync", url, started, resp, $"season {year}");
                return races;
            }
            catch (HttpRequestException ex)
            {
                LogHttpException("GetRacesBySeasonAsync", url, started, ex, $"season {year}");
                throw;
            }
            catch (TaskCanceledException ex)
            {
                LogHttpException("GetRacesBySeasonAsync", url, started, ex, $"season {year}");
                throw;
            }
        }

        private static void LogHttpStart(string operation, string url, string context)
        {
            System.Diagnostics.Debug.WriteLine($"OpenF1 {operation} GET {url} started ({context})");
        }

        private static void LogHttpSuccess(string operation, string url, DateTimeOffset started, HttpResponseMessage response, string context)
        {
            System.Diagnostics.Debug.WriteLine(
                $"OpenF1 {operation} GET {url} succeeded ({context}) after {(DateTimeOffset.UtcNow - started).TotalMilliseconds:N0} ms: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        private static void LogHttpFailure(string operation, string url, DateTimeOffset started, HttpResponseMessage response, string context, string body)
        {
            System.Diagnostics.Debug.WriteLine(
                $"OpenF1 {operation} GET {url} failed ({context}) after {(DateTimeOffset.UtcNow - started).TotalMilliseconds:N0} ms: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        private static void LogHttpException(string operation, string url, DateTimeOffset started, Exception ex, string context)
        {
            System.Diagnostics.Debug.WriteLine(
                $"OpenF1 {operation} GET {url} exception ({context}) after {(DateTimeOffset.UtcNow - started).TotalMilliseconds:N0} ms: {ex.GetType().Name}: {ex.Message}");
        }

        /// <summary>
        /// Loads session metadata for a given meeting (practice, qualifying, race, etc.).
        /// </summary>
        public async Task<IReadOnlyList<EventSession>> GetSessionsByMeetingKeyAsync(int meetingKey)
        {
            var url = $"sessions?meeting_key={meetingKey}";
            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var sessions = await JsonSerializer.DeserializeAsync<List<EventSession>>(stream, JsonOptions).ConfigureAwait(false)
                    ?? new List<EventSession>();
                return sessions;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"GetSessionsByMeetingKeyAsync meetingKey={meetingKey}");
                return Array.Empty<EventSession>();
            }
        }

        /// <summary>
        /// Loads lap-level timing rows for one session key.
        /// </summary>
        public async Task<IReadOnlyList<EventLap>> GetLapsBySessionKeyAsync(int sessionKey)
        {
            var url = $"laps?session_key={sessionKey}";
            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var laps = await JsonSerializer.DeserializeAsync<List<EventLap>>(stream, JsonOptions).ConfigureAwait(false)
                    ?? new List<EventLap>();
                return laps;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"GetLapsBySessionKeyAsync sessionKey={sessionKey}");
                return Array.Empty<EventLap>();
            }
        }

        /// <summary>
        /// Loads driver roster information scoped to meeting and session.
        /// </summary>
        public async Task<IReadOnlyList<Driver>> GetDriversByMeetingAndSessionAsync(int meetingKey, int sessionKey)
        {
            var url = $"drivers?meeting_key={meetingKey}&session_key={sessionKey}";
            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var drivers = await JsonSerializer.DeserializeAsync<List<Driver>>(stream, JsonOptions).ConfigureAwait(false)
                    ?? new List<Driver>();
                return drivers;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"GetDriversByMeetingAndSessionAsync meetingKey={meetingKey} sessionKey={sessionKey}");
                return Array.Empty<Driver>();
            }
        }

        /// <summary>
        /// Loads race-control messages (including SC/VSC events) for one session.
        /// </summary>
        public async Task<IReadOnlyList<RaceControlMessage>> GetRaceControlMessagesBySessionKeyAsync(int sessionKey)
        {
            var url = $"race_control?session_key={sessionKey}";
            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var messages = await JsonSerializer.DeserializeAsync<List<RaceControlMessage>>(stream, JsonOptions).ConfigureAwait(false)
                    ?? new List<RaceControlMessage>();
                return messages;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"GetRaceControlMessagesBySessionKeyAsync sessionKey={sessionKey}");
                return Array.Empty<RaceControlMessage>();
            }
        }

        /// <summary>
        /// Loads stint metadata (compound and lap range) for one session.
        /// </summary>
        public async Task<IReadOnlyList<EventStint>> GetStintsBySessionKeyAsync(int sessionKey)
        {
            var url = $"stints?session_key={sessionKey}";
            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var stints = await JsonSerializer.DeserializeAsync<List<EventStint>>(stream, JsonOptions).ConfigureAwait(false)
                    ?? new List<EventStint>();
                return stints;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"GetStintsBySessionKeyAsync sessionKey={sessionKey}");
                return Array.Empty<EventStint>();
            }
        }
    }
}
