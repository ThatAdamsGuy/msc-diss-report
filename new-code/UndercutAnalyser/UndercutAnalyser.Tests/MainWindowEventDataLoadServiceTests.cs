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

        var result = await MainWindowEventDataLoadService.LoadAsync(client, meetingKey: 1, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.False(result.HasRaceSession);
        Assert.Empty(result.Laps);
        Assert.Null(result.Reference);
    }

    [Fact]
    public async Task LoadAsync_FallsBackToFirstSession_WhenNoRaceNamedSession()
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

        var result = await MainWindowEventDataLoadService.LoadAsync(client, meetingKey: 1, fuelSecondsPer10Kg: 0.3, fuelKg: 110);

        Assert.True(result.HasRaceSession);
        Assert.Single(result.Laps);
        Assert.Single(result.Drivers);
        Assert.NotNull(result.Reference);
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
}
