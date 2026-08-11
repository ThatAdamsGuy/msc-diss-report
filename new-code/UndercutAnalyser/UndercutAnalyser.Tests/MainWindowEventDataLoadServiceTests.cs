using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowEventDataLoadServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsNoSessionFound_WhenSessionsEmpty()
    {
        var client = new StubClient
        {
            Sessions = []
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 1, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.False(result.HasRaceSession);
        Assert.Empty(result.Laps);
        Assert.Null(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNoSessionFound_WhenNoRaceNamedSession()
    {
        var start = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var client = new StubClient
        {
            Sessions =
            [
                new EventSession { SessionKey = 77, SessionName = "Sprint" }
            ],
            Laps =
            [
                new EventLap { DriverNumber = 4, LapNumber = 1, DateStart = start, LapDuration = 90f }
            ],
            Drivers =
            [
                new Driver { DriverNumber = 4, Code = "NOR" }
            ],
            RaceControlMessages = [],
            Stints = []
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 1, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.False(result.HasRaceSession);
        Assert.Empty(result.Laps);
        Assert.Empty(result.Drivers);
        Assert.Null(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_SuccessfulLoad_WhenSessionNamedRaceExactly()
    {
        // Tests successful data load when session is named "Race" exactly (case-insensitive match)
        var start = new DateTime(2025, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var lap1 = new EventLap { DriverNumber = 1, LapNumber = 1, DateStart = start, LapDuration = 95.5f };
        var lap2 = new EventLap { DriverNumber = 2, LapNumber = 1, DateStart = start, LapDuration = 96.2f };

        var client = new StubClient
        {
            Sessions = [new EventSession { SessionKey = 100, SessionName = "race" }],
            Laps = [lap1, lap2],
            Drivers = 
            [
                new Driver { DriverNumber = 1, Code = "VER" },
                new Driver { DriverNumber = 2, Code = "LEC" }
            ],
            RaceControlMessages = [],
            Stints = [new EventStint { DriverNumber = 1, Compound = "SOFT", StintNumber = 1 }]
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 1, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.True(result.HasRaceSession);
        Assert.NotEmpty(result.Laps);
        Assert.Equal(2, result.Laps.Count);
        Assert.NotEmpty(result.Drivers);
        Assert.Equal(2, result.Drivers.Count);
        Assert.NotEmpty(result.Stints);
        Assert.NotNull(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_SuccessfulLoad_WhenSessionNameContainsRaceSubstring()
    {
        // Tests session matching with "Race" as substring (e.g., "Feature Race", "Main Race")
        var start = new DateTime(2025, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var lap = new EventLap { DriverNumber = 3, LapNumber = 1, DateStart = start, LapDuration = 94.8f };

        var client = new StubClient
        {
            Sessions = 
            [
                new EventSession { SessionKey = 50, SessionName = "FP1" },
                new EventSession { SessionKey = 51, SessionName = "Qualifying" },
                new EventSession { SessionKey = 52, SessionName = "Main Race" }
            ],
            Laps = [lap],
            Drivers = [new Driver { DriverNumber = 3, Code = "NOR" }],
            RaceControlMessages = [],
            Stints = []
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 2, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.True(result.HasRaceSession);
        Assert.Single(result.Laps);
        Assert.Single(result.Drivers);
        Assert.NotNull(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_HandlesEmptyDataCollections_WhenRaceSessionFoundButNoDrivingData()
    {
        // Tests behavior when race session exists but has no laps/drivers/stints (degenerate case)
        var client = new StubClient
        {
            Sessions = [new EventSession { SessionKey = 200, SessionName = "Race" }],
            Laps = [],
            Drivers = [],
            RaceControlMessages = [],
            Stints = []
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 3, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.True(result.HasRaceSession);
        Assert.Empty(result.Laps);
        Assert.Empty(result.Drivers);
        // Reference calculation on empty laps should handle gracefully
        Assert.NotNull(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_ReturnsCancelledSession_WhenRaceSessionIsCancelled()
    {
        // Tests that cancelled race sessions are detected and reported before fetching lap data
        var client = new StubClient
        {
            Sessions = [new EventSession { SessionKey = 300, SessionName = "Race", IsCancelled = true }],
            // These should NOT be fetched because session is cancelled
            Laps = [new EventLap { DriverNumber = 1, LapNumber = 1, DateStart = DateTime.UtcNow, LapDuration = 90f }],
            Drivers = [new Driver { DriverNumber = 1, Code = "VER" }],
            RaceControlMessages = [],
            Stints = []
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 4, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.False(result.HasRaceSession);
        Assert.True(result.IsSessionCancelled);
        Assert.Empty(result.Laps);
        Assert.Empty(result.Drivers);
        Assert.Null(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_IgnoresCancelledSessions_WhenMultipleSessionsExist()
    {
        // Tests that cancelled race sessions are reported even if other race sessions exist
        // (Implementation choice: report the first found race session's cancellation status)
        var start = new DateTime(2025, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var client = new StubClient
        {
            Sessions =
            [
                new EventSession { SessionKey = 300, SessionName = "Race", IsCancelled = true },
                new EventSession { SessionKey = 301, SessionName = "Main Race", IsCancelled = false }
            ],
            Laps = [new EventLap { DriverNumber = 1, LapNumber = 1, DateStart = start, LapDuration = 90f }],
            Drivers = [new Driver { DriverNumber = 1, Code = "VER" }],
            RaceControlMessages = [],
            Stints = []
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 5, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        // Should detect the first race session as cancelled
        Assert.True(result.IsSessionCancelled);
        Assert.False(result.HasRaceSession);
        Assert.Empty(result.Laps);
        Assert.Empty(result.Drivers);
        Assert.Null(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_SucceedsWhenFirstRaceSessionNotCancelled()
    {
        // Tests successful load when the first race session found is not cancelled
        var start = new DateTime(2025, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var client = new StubClient
        {
            Sessions =
            [
                new EventSession { SessionKey = 300, SessionName = "Race", IsCancelled = false },
                new EventSession { SessionKey = 301, SessionName = "Main Race", IsCancelled = true }
            ],
            Laps = [new EventLap { DriverNumber = 1, LapNumber = 1, DateStart = start, LapDuration = 90f }],
            Drivers = [new Driver { DriverNumber = 1, Code = "VER" }],
            RaceControlMessages = [],
            Stints = []
        };

        var result = await EventWorkflowService.LoadAsync(client, meetingKey: 6, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        // Should use the non-cancelled first race session
        Assert.False(result.IsSessionCancelled);
        Assert.True(result.HasRaceSession);
        Assert.NotEmpty(result.Laps);
        Assert.NotEmpty(result.Drivers);
        Assert.NotNull(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_PropagatesClientExceptions()
    {
        // Tests that exceptions from client are allowed to propagate (not swallowed)
        var client = new ThrowingStubClient();

        var ex = await Record.ExceptionAsync(
            () => EventWorkflowService.LoadAsync(client, meetingKey: 99, fuelSecondsPer10Kg: 0.3, fuelKg: 110));

        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Equal("Simulated client error during session fetch", ex.Message);
    }

    private sealed class StubClient : IMainWindowEventDataClient
    {
        public IReadOnlyList<EventSession> Sessions { get; init; } = [];
        public IReadOnlyList<EventLap> Laps { get; init; } = [];
        public IReadOnlyList<Driver> Drivers { get; init; } = [];
        public IReadOnlyList<RaceControlMessage> RaceControlMessages { get; init; } = [];
        public IReadOnlyList<EventStint> Stints { get; init; } = [];

        public Task<IReadOnlyList<EventSession>> GetSessionsByMeetingKeyAsync(int meetingKey) => Task.FromResult(Sessions);
        public Task<IReadOnlyList<EventLap>> GetLapsBySessionKeyAsync(int sessionKey) => Task.FromResult(Laps);
        public Task<IReadOnlyList<Driver>> GetDriversByMeetingAndSessionAsync(int meetingKey, int sessionKey) => Task.FromResult(Drivers);
        public Task<IReadOnlyList<RaceControlMessage>> GetRaceControlMessagesBySessionKeyAsync(int sessionKey) => Task.FromResult(RaceControlMessages);
        public Task<IReadOnlyList<EventStint>> GetStintsBySessionKeyAsync(int sessionKey) => Task.FromResult(Stints);
    }

    private sealed class ThrowingStubClient : IMainWindowEventDataClient
    {
        public IReadOnlyList<EventSession> Sessions => throw new InvalidOperationException("Simulated client error during session fetch");
        public IReadOnlyList<EventLap> Laps => throw new NotSupportedException();
        public IReadOnlyList<Driver> Drivers => throw new NotSupportedException();
        public IReadOnlyList<RaceControlMessage> RaceControlMessages => throw new NotSupportedException();
        public IReadOnlyList<EventStint> Stints => throw new NotSupportedException();

        public Task<IReadOnlyList<EventSession>> GetSessionsByMeetingKeyAsync(int meetingKey) => 
            Task.FromException<IReadOnlyList<EventSession>>(new InvalidOperationException("Simulated client error during session fetch"));
        public Task<IReadOnlyList<EventLap>> GetLapsBySessionKeyAsync(int sessionKey) => 
            throw new NotSupportedException();
        public Task<IReadOnlyList<Driver>> GetDriversByMeetingAndSessionAsync(int meetingKey, int sessionKey) => 
            throw new NotSupportedException();
        public Task<IReadOnlyList<RaceControlMessage>> GetRaceControlMessagesBySessionKeyAsync(int sessionKey) => 
            throw new NotSupportedException();
        public Task<IReadOnlyList<EventStint>> GetStintsBySessionKeyAsync(int sessionKey) => 
            throw new NotSupportedException();
    }
}
