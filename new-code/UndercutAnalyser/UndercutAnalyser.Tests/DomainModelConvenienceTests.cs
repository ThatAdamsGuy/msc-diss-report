using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Tests;

public sealed class DomainModelConvenienceTests
{
    #region EventMeeting convenience properties
    // These tests exist to protect lightweight mapping properties consumed by UI bindings,
    // where accidental changes can silently degrade displayed labels or dates.

    [Fact]
    public void EventMeeting_RaceName_MapsToMeetingOfficialName()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "FORMULA 1 TEST GRAND PRIX" };

        Assert.Equal("FORMULA 1 TEST GRAND PRIX", meeting.RaceName);
    }

    [Fact]
    public void EventMeeting_CircuitName_MapsToCircuitShortName()
    {
        var meeting = new EventMeeting { CircuitShortName = "Suzuka" };

        Assert.Equal("Suzuka", meeting.CircuitName);
    }

    [Fact]
    public void EventMeeting_Date_ReturnsDateStartWhenPresent()
    {
        var start = new DateTime(2025, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        var meeting = new EventMeeting { DateStart = start };

        Assert.Equal(start, meeting.Date);
    }

    [Fact]
    public void EventMeeting_Date_ReturnsNullWhenDateStartIsDefault()
    {
        var meeting = new EventMeeting { DateStart = default };

        Assert.Null(meeting.Date);
    }

    #endregion

    #region Driver convenience properties
    // These tests exist to lock full-name formatting used across driver selection and display.

    [Fact]
    public void Driver_FullName_ConcatenatesFirstAndLastName()
    {
        var driver = new Driver { FirstName = "Max", LastName = "Verstappen" };

        Assert.Equal("Max Verstappen", driver.FullName);
    }

    [Fact]
    public void Driver_FullName_FallsBackToLastNameWhenFirstNameMissing()
    {
        var driver = new Driver { FirstName = string.Empty, LastName = "Norris" };

        Assert.Equal("Norris", driver.FullName);
    }

    #endregion
}
