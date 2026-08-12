using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowRaceTraceLegendServiceTests
{
    [Theory]
    [InlineData("Norris", true, "━ Norris")]
    [InlineData("Piastri", false, "┅ Piastri")]
    public void BuildLegendLabel_UsesExpectedStylePrefix(string driverName, bool isSolidLine, string expected)
    {
        var result = RaceTraceWorkflowService.BuildLegendLabel(driverName, isSolidLine);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyToggle_UpdatesOnlyRequestedDriver()
    {
        var current = new Dictionary<int, bool>
        {
            [4] = true,
            [81] = false
        };

        var next = RaceTraceWorkflowService.ApplyToggle(current, driverNumber: 81, isVisible: true);

        Assert.True(next[4]);
        Assert.True(next[81]);
    }

}
