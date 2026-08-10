using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Event-load data operations required by MainWindow event selection flow.
/// </summary>
public interface IMainWindowEventDataClient
{
    /// <summary>
    /// Loads session metadata for a selected meeting.
    /// </summary>
    Task<IReadOnlyList<EventSession>> GetSessionsByMeetingKeyAsync(int meetingKey);

    /// <summary>
    /// Loads lap timing rows for a selected session.
    /// </summary>
    Task<IReadOnlyList<EventLap>> GetLapsBySessionKeyAsync(int sessionKey);

    /// <summary>
    /// Loads drivers for the selected meeting/session pair.
    /// </summary>
    Task<IReadOnlyList<Driver>> GetDriversByMeetingAndSessionAsync(int meetingKey, int sessionKey);

    /// <summary>
    /// Loads race-control messages for a selected session.
    /// </summary>
    Task<IReadOnlyList<RaceControlMessage>> GetRaceControlMessagesBySessionKeyAsync(int sessionKey);

    /// <summary>
    /// Loads stint metadata for a selected session.
    /// </summary>
    Task<IReadOnlyList<EventStint>> GetStintsBySessionKeyAsync(int sessionKey);
}
