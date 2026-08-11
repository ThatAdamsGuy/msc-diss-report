using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Loads and shapes selected-event race data for MainWindow.
/// </summary>
public static partial class EventWorkflowService
{
    /// <summary>
    /// Loads event race data for a selected meeting key, including session resolution and reference calculation.
    /// </summary>
    public static async Task<MainWindowEventDataLoadResult> LoadAsync(
        IMainWindowEventDataClient client,
        int meetingKey,
        double fuelSecondsPer10Kg,
        double fuelKg)
    {
        var sessions = await client.GetSessionsByMeetingKeyAsync(meetingKey).ConfigureAwait(false);

        var raceSession = sessions.FirstOrDefault(s =>
            string.Equals(s.SessionName, "Race", StringComparison.OrdinalIgnoreCase) ||
            (s.SessionName?.IndexOf("Race", StringComparison.OrdinalIgnoreCase) >= 0));

        if (raceSession is null)
            return MainWindowEventDataLoadResult.NoSessionFound();

        if (raceSession.IsCancelled)
            return MainWindowEventDataLoadResult.SessionCancelled();

        var sessionKey = raceSession.SessionKey;
        var laps = await client.GetLapsBySessionKeyAsync(sessionKey).ConfigureAwait(false);
        var drivers = await client.GetDriversByMeetingAndSessionAsync(meetingKey, sessionKey).ConfigureAwait(false);
        var raceControlMessages = await client.GetRaceControlMessagesBySessionKeyAsync(sessionKey).ConfigureAwait(false);
        var stints = await client.GetStintsBySessionKeyAsync(sessionKey).ConfigureAwait(false);

        var reference = ReferenceLapTimeCalculator.Calculate(
            laps,
            raceControlMessages,
            fuelSecondsPer10Kg,
            fuelKg);

        return MainWindowEventDataLoadResult.Success(
            laps,
            drivers,
            stints,
            raceControlMessages,
            reference);
    }
}

/// <summary>
/// MainWindow event-load outcome and payload.
/// </summary>
public sealed record MainWindowEventDataLoadResult(
    bool HasRaceSession,
    bool IsSessionCancelled,
    IReadOnlyList<EventLap> Laps,
    IReadOnlyList<Driver> Drivers,
    IReadOnlyList<EventStint> Stints,
    IReadOnlyList<RaceControlMessage> RaceControlMessages,
    ReferenceLapTimeResult? Reference)
{
    /// <summary>
    /// Creates a successful event-load result.
    /// </summary>
    public static MainWindowEventDataLoadResult Success(
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<Driver> drivers,
        IReadOnlyList<EventStint> stints,
        IReadOnlyList<RaceControlMessage> raceControlMessages,
        ReferenceLapTimeResult reference) =>
        new(
            HasRaceSession: true,
            IsSessionCancelled: false,
            Laps: laps,
            Drivers: drivers,
            Stints: stints,
            RaceControlMessages: raceControlMessages,
            Reference: reference);

    /// <summary>
    /// Creates a no-session result for selected meeting.
    /// </summary>
    public static MainWindowEventDataLoadResult NoSessionFound() =>
        new(
            HasRaceSession: false,
            IsSessionCancelled: false,
            Laps: Array.Empty<EventLap>(),
            Drivers: Array.Empty<Driver>(),
            Stints: Array.Empty<EventStint>(),
            RaceControlMessages: Array.Empty<RaceControlMessage>(),
            Reference: null);

    /// <summary>
    /// Creates a cancelled-session result for selected meeting.
    /// </summary>
    public static MainWindowEventDataLoadResult SessionCancelled() =>
        new(
            HasRaceSession: false,
            IsSessionCancelled: true,
            Laps: Array.Empty<EventLap>(),
            Drivers: Array.Empty<Driver>(),
            Stints: Array.Empty<EventStint>(),
            RaceControlMessages: Array.Empty<RaceControlMessage>(),
            Reference: null);
}
