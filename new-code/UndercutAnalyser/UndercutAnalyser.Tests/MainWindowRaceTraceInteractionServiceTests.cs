using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowRaceTraceInteractionServiceTests
{
    [Fact]
    public void ApplyLeftClickSelection_AddsClicked_WhenNoFocusOrSelection()
    {
        var state = RaceTraceWorkflowService.ApplyLeftClickSelection(new HashSet<int>(), focusedDriverNumber: null, clickedDriverNumber: 4);

        Assert.Equal([4], state.SelectedDriverNumbers.OrderBy(x => x).ToArray());
        Assert.Null(state.FocusedDriverNumber);
    }

    [Fact]
    public void ApplyLeftClickSelection_RemovesClicked_WhenAlreadySelected()
    {
        var state = RaceTraceWorkflowService.ApplyLeftClickSelection(new HashSet<int> { 4, 81 }, focusedDriverNumber: null, clickedDriverNumber: 4);

        Assert.Equal([81], state.SelectedDriverNumbers.OrderBy(x => x).ToArray());
        Assert.Null(state.FocusedDriverNumber);
    }

    [Fact]
    public void ApplyLeftClickSelection_MergesFocusedAndClicked_WhenFocusedExists()
    {
        var state = RaceTraceWorkflowService.ApplyLeftClickSelection(new HashSet<int>(), focusedDriverNumber: 81, clickedDriverNumber: 4);

        Assert.Equal([4, 81], state.SelectedDriverNumbers.OrderBy(x => x).ToArray());
        Assert.Null(state.FocusedDriverNumber);
    }

    [Fact]
    public void ApplyRightClickFocus_OverridesSelection_WithSingleFocusedDriver()
    {
        var state = RaceTraceWorkflowService.ApplyRightClickFocus(new HashSet<int> { 4, 81 }, focusedDriverNumber: null, clickedDriverNumber: 63);

        Assert.Empty(state.SelectedDriverNumbers);
        Assert.Equal(63, state.FocusedDriverNumber);
    }

    [Fact]
    public void ApplyRightClickFocus_TogglesFocusedDriver_WhenNoSelectionExists()
    {
        var first = RaceTraceWorkflowService.ApplyRightClickFocus(new HashSet<int>(), focusedDriverNumber: null, clickedDriverNumber: 81);
        Assert.Equal(81, first.FocusedDriverNumber);

        var second = RaceTraceWorkflowService.ApplyRightClickFocus(new HashSet<int>(), focusedDriverNumber: 81, clickedDriverNumber: 81);
        Assert.Null(second.FocusedDriverNumber);
    }

    [Fact]
    public void PruneSelectedDrivers_RemovesDriversNotInSeries()
    {
        var selected = new HashSet<int> { 4, 81, 99 };
        var series = new List<RaceTraceDriverSeries>
        {
            CreateSeries(4),
            CreateSeries(81)
        };

        var pruned = RaceTraceWorkflowService.PruneSelectedDrivers(selected, series);

        Assert.Equal([4, 81], pruned.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void PruneFocusedDriver_ReturnsNull_WhenMissingFromSeries()
    {
        var series = new List<RaceTraceDriverSeries> { CreateSeries(4) };

        var pruned = RaceTraceWorkflowService.PruneFocusedDriver(81, series);

        Assert.Null(pruned);
    }

    [Theory]
    [InlineData(4, 0.90)]
    [InlineData(81, null)]
    [InlineData(63, null)]
    public void ResolveDimmingMix_WithSelectionAndHover_KeepsSelectedAndHoveredPrimary(int driverNumber, double? expected)
    {
        var mix = RaceTraceWorkflowService.ResolveDimmingMix(
            driverNumber,
            selectedDriverNumbers: new HashSet<int> { 81 },
            focusedDriverNumber: null,
            hoveredDriverNumber: 63);

        Assert.Equal(expected, mix);
    }

    [Theory]
    [InlineData(4, 0.90)]
    [InlineData(81, null)]
    public void ResolveDimmingMix_WithSelectionOnly_DimsNonSelected(int driverNumber, double? expected)
    {
        var mix = RaceTraceWorkflowService.ResolveDimmingMix(
            driverNumber,
            selectedDriverNumbers: new HashSet<int> { 81 },
            focusedDriverNumber: null,
            hoveredDriverNumber: null);

        Assert.Equal(expected, mix);
    }

    [Theory]
    [InlineData(4, null)]
    [InlineData(81, null)]
    [InlineData(63, 0.90)]
    public void ResolveDimmingMix_WithFocusedAndHover_KeepsBothPrimary(int driverNumber, double? expected)
    {
        var mix = RaceTraceWorkflowService.ResolveDimmingMix(
            driverNumber,
            selectedDriverNumbers: new HashSet<int>(),
            focusedDriverNumber: 4,
            hoveredDriverNumber: 81);

        Assert.Equal(expected, mix);
    }

    [Theory]
    [InlineData(4, 0.75)]
    [InlineData(81, null)]
    public void ResolveDimmingMix_WithHoverOnly_DimsNonHovered(int driverNumber, double? expected)
    {
        var mix = RaceTraceWorkflowService.ResolveDimmingMix(
            driverNumber,
            selectedDriverNumbers: new HashSet<int>(),
            focusedDriverNumber: null,
            hoveredDriverNumber: 81);

        Assert.Equal(expected, mix);
    }

    private static RaceTraceDriverSeries CreateSeries(int driverNumber)
    {
        return new RaceTraceDriverSeries(
            DriverNumber: driverNumber,
            DriverName: driverNumber.ToString(),
            DriverCode: driverNumber.ToString(),
            TeamColour: "FFFFFF",
            IsSolidLine: true,
            IsVisible: true,
            Xs: [1],
            Ys: [0.0]);
    }
}
