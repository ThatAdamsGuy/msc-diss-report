using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Tests;

public sealed class EventSessionTests
{
    [Fact]
    public void DefaultInitializers_AreStableForBinding()
    {
        var session = new EventSession();

        Assert.Equal(string.Empty, session.SessionType);
        Assert.Equal(string.Empty, session.SessionName);
        Assert.Equal(string.Empty, session.CircuitShortName);
        Assert.Equal(string.Empty, session.CountryCode);
        Assert.Equal(string.Empty, session.CountryName);
        Assert.Equal(string.Empty, session.Location);
        Assert.Equal(string.Empty, session.GmtOffset);
        Assert.False(session.IsCancelled);
        Assert.Null(session.DateStart);
        Assert.Null(session.DateEnd);
    }
}
