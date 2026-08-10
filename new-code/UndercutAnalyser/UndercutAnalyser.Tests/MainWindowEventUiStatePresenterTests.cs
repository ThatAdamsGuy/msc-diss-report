using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowEventUiStatePresenterTests
{
    [Fact]
    public void Selected_ReturnsExpectedDisplayAndDisabledControls()
    {
        var meeting = new EventMeeting { MeetingOfficialName = "Australian Grand Prix", Year = 2025 };

        var state = MainWindowEventUiStatePresenter.Selected(meeting);

        Assert.Equal("Australian Grand Prix (2025) (Hover for Details)", state.DisplayText);
        Assert.Equal("Event selected: Australian Grand Prix (2025)", state.TooltipText);
        Assert.False(state.EnableRawData);
        Assert.False(state.EnableRaceTrace);
    }

    [Fact]
    public void Loaded_ReturnsEnabledWorkflowControls()
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

        var state = MainWindowEventUiStatePresenter.Loaded(meeting, reference, driverCount: 20);

        Assert.True(state.EnableRawData);
        Assert.True(state.EnablePredictionScan);
        Assert.True(state.EnableScanAllDrivers);
        Assert.True(state.EnableRaceTrace);
        Assert.Contains("Rows: 25", state.TooltipText);
    }
}
