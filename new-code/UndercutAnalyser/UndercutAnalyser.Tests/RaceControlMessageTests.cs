using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Tests;

public sealed class RaceControlMessageTests
{
    [Fact]
    public void DefaultInitializers_AreStableForBinding()
    {
        var message = new RaceControlMessage();

        Assert.Equal(string.Empty, message.Category);
        Assert.Equal(string.Empty, message.Message);
        Assert.Null(message.Date);
        Assert.Null(message.DriverNumber);
        Assert.Null(message.LapNumber);
    }
}
