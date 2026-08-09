using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using UndercutAnalyser.Infrastructure;

namespace UndercutAnalyser.Tests;

public sealed class OpenF1RaceDataClientTests
{
    #region Endpoint routing and request composition
    // These tests exist to ensure each public method hits the expected OpenF1 path and query
    // contract. If URLs drift, data-loading silently breaks even when model logic is correct.

    [Fact]
    public async Task GetRacesBySeasonAsync_UsesMeetingsEndpoint()
    {
        var sut = CreateClient(new RoutingHandler(req =>
        {
            Assert.Equal("https://api.openf1.org/v1/meetings", req.RequestUri!.ToString());
            return JsonResponse("[]");
        }));

        _ = await sut.GetRacesBySeasonAsync(2025);
    }

    [Fact]
    public async Task GetSessionsByMeetingKeyAsync_UsesMeetingKeyQueryParameter()
    {
        var sut = CreateClient(new RoutingHandler(req =>
        {
            Assert.Equal("https://api.openf1.org/v1/sessions?meeting_key=1234", req.RequestUri!.ToString());
            return JsonResponse("[]");
        }));

        _ = await sut.GetSessionsByMeetingKeyAsync(1234);
    }

    [Fact]
    public async Task GetLapsBySessionKeyAsync_UsesSessionKeyQueryParameter()
    {
        var sut = CreateClient(new RoutingHandler(req =>
        {
            Assert.Equal("https://api.openf1.org/v1/laps?session_key=77", req.RequestUri!.ToString());
            return JsonResponse("[]");
        }));

        _ = await sut.GetLapsBySessionKeyAsync(77);
    }

    [Fact]
    public async Task GetDriversByMeetingAndSessionAsync_UsesBothQueryParameters()
    {
        var sut = CreateClient(new RoutingHandler(req =>
        {
            Assert.Equal("https://api.openf1.org/v1/drivers?meeting_key=77&session_key=88", req.RequestUri!.ToString());
            return JsonResponse("[]");
        }));

        _ = await sut.GetDriversByMeetingAndSessionAsync(77, 88);
    }

    [Fact]
    public async Task GetRaceControlMessagesBySessionKeyAsync_UsesSessionKeyQueryParameter()
    {
        var sut = CreateClient(new RoutingHandler(req =>
        {
            Assert.Equal("https://api.openf1.org/v1/race_control?session_key=55", req.RequestUri!.ToString());
            return JsonResponse("[]");
        }));

        _ = await sut.GetRaceControlMessagesBySessionKeyAsync(55);
    }

    [Fact]
    public async Task GetStintsBySessionKeyAsync_UsesSessionKeyQueryParameter()
    {
        var sut = CreateClient(new RoutingHandler(req =>
        {
            Assert.Equal("https://api.openf1.org/v1/stints?session_key=900", req.RequestUri!.ToString());
            return JsonResponse("[]");
        }));

        _ = await sut.GetStintsBySessionKeyAsync(900);
    }

    #endregion

    #region Success-path deserialization behavior
    // These tests exist to verify resilient JSON parsing into domain models, including
    // converter-backed nullable fields used by lap/reference calculations.

    [Fact]
    public async Task GetRacesBySeasonAsync_DeserializesMeetingPayload()
    {
        const string json = """
        [
          {
            "meeting_key": 100,
            "meeting_name": "Bahrain Grand Prix",
            "meeting_official_name": "FORMULA 1 GULF AIR BAHRAIN GRAND PRIX 2025",
            "circuit_short_name": "Sakhir",
            "location": "Sakhir",
            "year": 2025
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var races = await sut.GetRacesBySeasonAsync(2025);

        var race = Assert.Single(races);
        Assert.Equal(100, race.MeetingKey);
        Assert.Equal("FORMULA 1 GULF AIR BAHRAIN GRAND PRIX 2025", race.MeetingOfficialName);
        Assert.Equal("Sakhir", race.CircuitShortName);
        Assert.Equal(2025, race.Year);
    }

    [Fact]
    public async Task GetSessionsByMeetingKeyAsync_DeserializesSessionPayload()
    {
        const string json = """
        [
          {
            "session_key": 11,
            "session_type": "Race",
            "session_name": "Race",
            "meeting_key": 22,
            "circuit_short_name": "Monza",
            "country_name": "Italy",
            "year": 2024
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var sessions = await sut.GetSessionsByMeetingKeyAsync(22);

        var session = Assert.Single(sessions);
        Assert.Equal(11, session.SessionKey);
        Assert.Equal("Race", session.SessionType);
        Assert.Equal("Monza", session.CircuitShortName);
        Assert.Equal("Italy", session.CountryName);
        Assert.Equal(2024, session.Year);
    }

    [Fact]
    public async Task GetLapsBySessionKeyAsync_ParsesNullableDateAndFloatFieldsViaConverters()
    {
        const string json = """
        [
          {
            "lap_number": 14,
            "date_start": "2025-03-01T15:10:20Z",
            "lap_duration": "95.375",
            "is_pit_out_lap": false
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var laps = await sut.GetLapsBySessionKeyAsync(5001);

        var lap = Assert.Single(laps);
        Assert.Equal(14, lap.LapNumber);
        Assert.NotNull(lap.DateStart);
        Assert.Equal(DateTimeKind.Utc, lap.DateStart!.Value.Kind);
        Assert.Equal(95.375f, lap.LapDuration!.Value, 3);
        Assert.False(lap.IsPitOutLap);
    }

    [Fact]
    public async Task GetLapsBySessionKeyAsync_HandlesEpochDatesAndInvalidFloatAsNull()
    {
        const string json = """
        [
          {
            "lap_number": 3,
            "date_start": 1700000000,
            "lap_duration": "not-a-number",
            "is_pit_out_lap": true
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var laps = await sut.GetLapsBySessionKeyAsync(9);

        var lap = Assert.Single(laps);
        Assert.NotNull(lap.DateStart);
        Assert.Equal(DateTimeKind.Utc, lap.DateStart!.Value.Kind);
        Assert.Null(lap.LapDuration);
        Assert.True(lap.IsPitOutLap);
    }

    [Fact]
    public async Task GetDriversByMeetingAndSessionAsync_DeserializesDriverPayload()
    {
        const string json = """
        [
          {
            "driver_key": 900,
            "driver_number": 44,
            "first_name": "Lewis",
            "last_name": "Hamilton",
            "code": "HAM",
            "team_name": "Mercedes"
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var drivers = await sut.GetDriversByMeetingAndSessionAsync(1, 2);

        var driver = Assert.Single(drivers);
        Assert.Equal(900, driver.DriverKey);
        Assert.Equal(44, driver.DriverNumber);
        Assert.Equal("HAM", driver.Code);
        Assert.Equal("Mercedes", driver.TeamName);
        Assert.Equal("Lewis", driver.FirstName);
    }

    [Fact]
    public async Task GetRaceControlMessagesBySessionKeyAsync_DeserializesMessagePayload()
    {
        const string json = """
        [
          {
            "meeting_key": 1,
            "session_key": 2,
            "date": "2025-03-01T15:10:20Z",
            "category": "SafetyCar",
            "message": "VSC DEPLOYED"
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var messages = await sut.GetRaceControlMessagesBySessionKeyAsync(2);

        var message = Assert.Single(messages);
        Assert.Equal("SafetyCar", message.Category);
        Assert.Equal("VSC DEPLOYED", message.Message);
        Assert.NotNull(message.Date);
    }

    [Fact]
    public async Task GetRaceControlMessagesBySessionKeyAsync_ParsesEpochDateToUtc()
    {
        const string json = """
        [
          {
            "meeting_key": 1,
            "session_key": 2,
            "date": 1700000000,
            "category": "SafetyCar",
            "message": "SAFETY CAR DEPLOYED"
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var messages = await sut.GetRaceControlMessagesBySessionKeyAsync(2);

        var message = Assert.Single(messages);
        Assert.NotNull(message.Date);
        Assert.Equal(DateTimeKind.Utc, message.Date!.Value.Kind);
    }

    [Fact]
    public async Task GetStintsBySessionKeyAsync_DeserializesStintPayload()
    {
        const string json = """
        [
          {
            "meeting_key": 1,
            "session_key": 2,
            "stint_number": 3,
            "driver_number": 4,
            "lap_start": 10,
            "lap_end": 22,
            "compound": "MEDIUM",
            "tyre_age_at_start": 5
          }
        ]
        """;

        var sut = CreateClient(new RoutingHandler(_ => JsonResponse(json)));
        var stints = await sut.GetStintsBySessionKeyAsync(2);

        var stint = Assert.Single(stints);
        Assert.Equal(3, stint.StintNumber);
        Assert.Equal(4, stint.DriverNumber);
        Assert.Equal(10, stint.LapStart);
        Assert.Equal(22, stint.LapEnd);
        Assert.Equal("MEDIUM", stint.Compound);
    }

    [Fact]
    public async Task CatchProtectedMethods_NullJsonPayload_ReturnsEmptyCollections()
    {
        var sut = CreateClient(new RoutingHandler(_ => JsonResponse("null")));

        Assert.Empty(await sut.GetSessionsByMeetingKeyAsync(1));
        Assert.Empty(await sut.GetLapsBySessionKeyAsync(1));
        Assert.Empty(await sut.GetDriversByMeetingAndSessionAsync(1, 1));
        Assert.Empty(await sut.GetRaceControlMessagesBySessionKeyAsync(1));
        Assert.Empty(await sut.GetStintsBySessionKeyAsync(1));
    }

    #endregion

    #region Failure handling and defensive fallbacks
    // These tests exist to pin the intentional contract split: meetings call throws on failure,
    // while other endpoint methods degrade gracefully to empty results.

    [Fact]
    public async Task GetRacesBySeasonAsync_OnHttpError_ThrowsHttpRequestException()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error")
            }));

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetRacesBySeasonAsync(2025));
    }

    [Fact]
    public async Task GetRacesBySeasonAsync_OnMalformedJson_ThrowsJsonException()
    {
        var sut = CreateClient(new RoutingHandler(_ => JsonResponse("{ invalid")));

        await Assert.ThrowsAsync<JsonException>(() => sut.GetRacesBySeasonAsync(2025));
    }

    [Fact]
    public async Task GetRacesBySeasonAsync_OnEmptyBody_ThrowsJsonException()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            }));

        await Assert.ThrowsAsync<JsonException>(() => sut.GetRacesBySeasonAsync(2025));
    }

    [Fact]
    public async Task GetRacesBySeasonAsync_OnNullJson_ReturnsEmptyCollection()
    {
        var sut = CreateClient(new RoutingHandler(_ => JsonResponse("null")));

        var races = await sut.GetRacesBySeasonAsync(2025);

        Assert.Empty(races);
    }

    [Fact]
    public async Task GetSessionsByMeetingKeyAsync_OnHttpError_ReturnsEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream failure")
            }));

        var sessions = await sut.GetSessionsByMeetingKeyAsync(1);

        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetLapsBySessionKeyAsync_OnHttpError_ReturnsEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("down")
            }));

        var laps = await sut.GetLapsBySessionKeyAsync(1);

        Assert.Empty(laps);
    }

    [Fact]
    public async Task GetDriversByMeetingAndSessionAsync_OnHttpError_ReturnsEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("missing")
            }));

        var drivers = await sut.GetDriversByMeetingAndSessionAsync(1, 2);

        Assert.Empty(drivers);
    }

    [Fact]
    public async Task GetRaceControlMessagesBySessionKeyAsync_OnHttpError_ReturnsEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
            {
                Content = new StringContent("timeout")
            }));

        var messages = await sut.GetRaceControlMessagesBySessionKeyAsync(1);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetStintsBySessionKeyAsync_OnHttpError_ReturnsEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("auth")
            }));

        var stints = await sut.GetStintsBySessionKeyAsync(1);

        Assert.Empty(stints);
    }

    [Fact]
    public async Task CatchProtectedMethods_OnMalformedJson_ReturnEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ => JsonResponse("{ not valid json")));

        Assert.Empty(await sut.GetSessionsByMeetingKeyAsync(10));
        Assert.Empty(await sut.GetLapsBySessionKeyAsync(10));
        Assert.Empty(await sut.GetDriversByMeetingAndSessionAsync(10, 10));
        Assert.Empty(await sut.GetRaceControlMessagesBySessionKeyAsync(10));
        Assert.Empty(await sut.GetStintsBySessionKeyAsync(10));
    }

    [Fact]
    public async Task CatchProtectedMethods_OnEmptyBody_ReturnEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            }));

        Assert.Empty(await sut.GetSessionsByMeetingKeyAsync(10));
        Assert.Empty(await sut.GetLapsBySessionKeyAsync(10));
        Assert.Empty(await sut.GetDriversByMeetingAndSessionAsync(10, 10));
        Assert.Empty(await sut.GetRaceControlMessagesBySessionKeyAsync(10));
        Assert.Empty(await sut.GetStintsBySessionKeyAsync(10));
    }

    [Fact]
    public async Task CatchProtectedMethods_OnTransportException_ReturnEmpty()
    {
        var sut = CreateClient(new RoutingHandler(_ => throw new HttpRequestException("connection dropped")));

        Assert.Empty(await sut.GetSessionsByMeetingKeyAsync(4));
        Assert.Empty(await sut.GetLapsBySessionKeyAsync(4));
        Assert.Empty(await sut.GetDriversByMeetingAndSessionAsync(4, 7));
        Assert.Empty(await sut.GetRaceControlMessagesBySessionKeyAsync(4));
        Assert.Empty(await sut.GetStintsBySessionKeyAsync(4));
    }

    #endregion

    private static OpenF1RaceDataClient CreateClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openf1.org/v1/")
        };

        return new OpenF1RaceDataClient(http);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = _responseFactory(request);
            return Task.FromResult(response);
        }
    }
}
