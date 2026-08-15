using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowEventUiStatePresenterTests
{
    [Fact]
    public void NoEventSelected_ReturnsBlankAndAllControlsDisabled()
    {
        var state = EventWorkflowService.NoEventSelected();

        Assert.Equal(string.Empty, state.DisplayText);
        Assert.Equal(string.Empty, state.TooltipText);
        Assert.False(state.EnableRawData);
        Assert.False(state.EnablePredictionScan);
        Assert.False(state.EnableScanAllDrivers);
        Assert.False(state.EnableScanSingleDriver);
        Assert.False(state.EnableRaceTrace);
    }

    [Fact]
    public void Selected_ReturnsExpectedDisplayAndDisabledControls()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };

        var state = EventWorkflowService.Selected(meeting);

        Assert.Equal("Australian Grand Prix (2025) (Hover for Details)", state.DisplayText);
        Assert.Equal("Event selected: Australian Grand Prix (2025)", state.TooltipText);
        Assert.False(state.EnableRawData);
        Assert.False(state.EnablePredictionScan);
        Assert.False(state.EnableScanAllDrivers);
        Assert.False(state.EnableScanSingleDriver);
        Assert.False(state.EnableRaceTrace);
    }

    [Fact]
    public void Loading_ReturnsExpectedDisplayAndLoadingTooltip()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };

        var state = EventWorkflowService.Loading(meeting);

        Assert.Equal("Australian Grand Prix (2025) (Hover for Details)", state.DisplayText);
        Assert.Equal("Loading race data...", state.TooltipText);
        Assert.False(state.EnableRawData);
        Assert.False(state.EnablePredictionScan);
        Assert.False(state.EnableScanAllDrivers);
        Assert.False(state.EnableScanSingleDriver);
        Assert.False(state.EnableRaceTrace);
    }

    [Fact]
    public void Loaded_ReturnsEnabledWorkflowControls_AndAverageWhenPresent()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };
        var reference = new ReferenceLapTimeResult(
            TotalLapRows: 25,
            MaxSessionLapNumber: 20,
            IncludedLaps: 10,
            ExcludedPitOutLaps: 1,
            ExcludedPitInLaps: 1,
            ExcludedSafetyCarLaps: 2,
            FuelEffectPerLapSeconds: 0.3,
            SumLapTimeSeconds: 905.0,
            AverageLapTimeSeconds: 90.5,
            SafetyCarWindows: []);

        var state = EventWorkflowService.Loaded(meeting, reference, driverCount: 20);

        Assert.True(state.EnableRawData);
        Assert.True(state.EnablePredictionScan);
        Assert.True(state.EnableScanAllDrivers);
        Assert.False(state.EnableScanSingleDriver);
        Assert.True(state.EnableRaceTrace);
        Assert.Contains("Rows: 25", state.TooltipText);
        Assert.Contains("Average: 90.500s", state.TooltipText);
        Assert.Contains("Drivers: 20", state.TooltipText);
    }

    [Fact]
    public void Loaded_UsesNaWhenAverageIsMissing()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };
        var reference = new ReferenceLapTimeResult(
            TotalLapRows: 12,
            MaxSessionLapNumber: 10,
            IncludedLaps: 0,
            ExcludedPitOutLaps: 0,
            ExcludedPitInLaps: 0,
            ExcludedSafetyCarLaps: 0,
            FuelEffectPerLapSeconds: 0.0,
            SumLapTimeSeconds: 0.0,
            AverageLapTimeSeconds: null,
            SafetyCarWindows: []);

        var state = EventWorkflowService.Loaded(meeting, reference, driverCount: 1);

        Assert.Contains("Average: N/As", state.TooltipText);
    }

    [Fact]
    public void NoRaceSession_ReturnsDisplayWithNoSessionTooltip()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };

        var state = EventWorkflowService.NoRaceSession(meeting);

        Assert.Equal("Australian Grand Prix (2025) (Hover for Details)", state.DisplayText);
        Assert.Equal("No race session found", state.TooltipText);
        Assert.False(state.EnableRawData);
        Assert.False(state.EnablePredictionScan);
        Assert.False(state.EnableScanAllDrivers);
        Assert.False(state.EnableScanSingleDriver);
        Assert.False(state.EnableRaceTrace);
    }

    [Fact]
    public void SessionCancelled_WithEvent_UsesDisplayText()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };

        var state = EventWorkflowService.SessionCancelled(meeting);

        Assert.Equal("Australian Grand Prix (2025) (Hover for Details)", state.DisplayText);
        Assert.Equal("Race session cancelled", state.TooltipText);
        Assert.False(state.EnableRawData);
        Assert.False(state.EnablePredictionScan);
        Assert.False(state.EnableScanAllDrivers);
        Assert.False(state.EnableScanSingleDriver);
        Assert.False(state.EnableRaceTrace);
    }

    [Fact]
    public void SessionCancelled_WithoutEvent_UsesBlankDisplayText()
    {
        var state = EventWorkflowService.SessionCancelled(selectedEvent: null);

        Assert.Equal(string.Empty, state.DisplayText);
        Assert.Equal("Race session cancelled", state.TooltipText);
    }

    [Fact]
    public void LoadError_WithEvent_UsesDisplayTextAndErrorMessage()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };

        var state = EventWorkflowService.LoadError(meeting, "boom");

        Assert.Equal("Australian Grand Prix (2025) (Hover for Details)", state.DisplayText);
        Assert.Equal("Error loading race data: boom", state.TooltipText);
        Assert.False(state.EnableRawData);
        Assert.False(state.EnablePredictionScan);
        Assert.False(state.EnableScanAllDrivers);
        Assert.False(state.EnableScanSingleDriver);
        Assert.False(state.EnableRaceTrace);
    }

    [Fact]
    public void LoadError_WithoutEvent_UsesBlankDisplayText()
    {
        var state = EventWorkflowService.LoadError(selectedEvent: null, errorMessage: "boom");

        Assert.Equal(string.Empty, state.DisplayText);
        Assert.Equal("Error loading race data: boom", state.TooltipText);
    }
}
