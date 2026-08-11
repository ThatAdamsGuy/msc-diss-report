using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowLogicTests
{
    [Theory]
    [InlineData("Ahead", true, true, true, true)]
    [InlineData("Ahead", false, true, true, false)]
    [InlineData("Marginal", true, true, false, true)]
    [InlineData("Marginal", true, false, true, false)]
    [InlineData("Behind", true, true, true, true)]
    [InlineData("Behind", true, true, false, false)]
    [InlineData("Error: x", false, false, false, true)]
    public void ShouldShowResult_RespectsTogglesForKnownClasses_AndShowsUnknownByDefault(
        string result,
        bool showAhead,
        bool showMarginal,
        bool showBehind,
        bool expected)
    {
        var actual = WorkspaceWorkflowService.ShouldShowResult(result, showAhead, showMarginal, showBehind);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOpportunities_CountsAheadAndMarginalOnly()
    {
        var count = WorkspaceWorkflowService.CountOpportunities(["Ahead", "Marginal", "Behind", "Error: x", "ahead"]);
        Assert.Equal(3, count);
    }

    [Fact]
    public void BuildMainScanStatusText_UsesExpectedFormat()
    {
        var text = WorkspaceWorkflowService.BuildMainScanStatusText(shownCount: 7, scannedCount: 20, opportunityCount: 1);
        Assert.Equal("7 shown (20 scanned) - 1 opportunity found", text);
    }

    [Fact]
    public void BuildSingleScanStatusText_UsesExpectedFormat()
    {
        var text = WorkspaceWorkflowService.BuildSingleScanStatusText("1 single-driver scenario scanned.", shownCount: 1, opportunityCount: 0);
        Assert.Equal("1 single-driver scenario scanned. - 1 shown, 0 opportunities found", text);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    [InlineData("", "")]
    public void EscapeCsv_EscapesOnlyWhenRequired(string input, string expected)
    {
        var actual = WorkspaceWorkflowService.EscapeCsv(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("soft", "SOFT")]
    [InlineData("Medium", "MEDIUM")]
    [InlineData("hard", "HARD")]
    [InlineData("", "UNKNOWN")]
    [InlineData("custom", "CUSTOM")]
    public void NormalizeCompoundText_NormalizesKnownAndUnknownValues(string input, string expected)
    {
        var actual = WorkspaceWorkflowService.NormalizeCompoundText(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveTyreStateAtLap_UsesActiveStintAndComputesAge()
    {
        var stints = new List<EventStint>
        {
            new() { DriverNumber = 4, StintNumber = 2, LapStart = 10, LapEnd = 20, Compound = "soft", TyreAgeAtStart = 3 }
        };

        var (compound, age) = WorkspaceWorkflowService.ResolveTyreStateAtLap(stints, [], driverNumber: 4, lapNumber: 12);

        Assert.Equal("SOFT", compound);
        Assert.Equal(5, age);
    }

    [Fact]
    public void ResolveTyreStateAtLap_FallsBackToMostRecentKnownStint_WhenNoRangeMatch()
    {
        var stints = new List<EventStint>
        {
            new() { DriverNumber = 4, StintNumber = 1, LapStart = 5, LapEnd = 8, Compound = "medium", TyreAgeAtStart = 0 }
        };

        var (compound, age) = WorkspaceWorkflowService.ResolveTyreStateAtLap(stints, [], driverNumber: 4, lapNumber: 10);

        Assert.Equal("MEDIUM", compound);
        Assert.Equal(5, age);
    }

    [Fact]
    public void ResolveTyreStateAtLap_ReturnsUnknownAndPitOutBasedAge_WhenNoStintKnown()
    {
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 4, LapNumber = 3, IsPitOutLap = true },
            new() { DriverNumber = 4, LapNumber = 7, IsPitOutLap = true }
        };

        var (compound, age) = WorkspaceWorkflowService.ResolveTyreStateAtLap([], laps, driverNumber: 4, lapNumber: 10);

        Assert.Equal("UNKNOWN", compound);
        Assert.Equal(3, age);
    }
}
