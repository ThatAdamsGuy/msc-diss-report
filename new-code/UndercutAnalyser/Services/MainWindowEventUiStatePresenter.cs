using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services;

/// <summary>
/// Produces render-ready event-selection UI state for MainWindow controls and status text.
/// </summary>
public static class MainWindowEventUiStatePresenter
{
    /// <summary>
    /// State shown immediately after event selection, before data loading begins.
    /// </summary>
    public static MainWindowEventUiState Selected(EventMeeting selectedEvent)
    {
        return new MainWindowEventUiState(
            DisplayText: BuildDisplayText(selectedEvent),
            TooltipText: $"Event selected: {selectedEvent.RaceName} ({selectedEvent.Year})",
            EnableRawData: false,
            EnablePredictionScan: false,
            EnableScanAllDrivers: false,
            EnableScanSingleDriver: false,
            EnableRaceTrace: false);
    }

    /// <summary>
    /// State shown while race data for selected event is loading.
    /// </summary>
    public static MainWindowEventUiState Loading(EventMeeting selectedEvent)
    {
        return new MainWindowEventUiState(
            DisplayText: BuildDisplayText(selectedEvent),
            TooltipText: "Loading race data...",
            EnableRawData: false,
            EnablePredictionScan: false,
            EnableScanAllDrivers: false,
            EnableScanSingleDriver: false,
            EnableRaceTrace: false);
    }

    /// <summary>
    /// State shown when race data loaded successfully.
    /// </summary>
    public static MainWindowEventUiState Loaded(EventMeeting selectedEvent, ReferenceLapTimeResult reference, int driverCount)
    {
        return new MainWindowEventUiState(
            DisplayText: BuildDisplayText(selectedEvent),
            TooltipText:
                $"Rows: {reference.TotalLapRows}\nSession laps: {reference.MaxSessionLapNumber}\nDrivers: {driverCount}\n" +
                $"Reference sum (fuel-adjusted): {reference.SumLapTimeSeconds:F3}s\nAverage: {(reference.AverageLapTimeSeconds.HasValue ? reference.AverageLapTimeSeconds.Value.ToString("F3") : "N/A")}s\n" +
                $"Fuel/lap: {reference.FuelEffectPerLapSeconds:F3}s\nClean laps: {reference.IncludedLaps} (pit-out: {reference.ExcludedPitOutLaps}, pit-in: {reference.ExcludedPitInLaps}, SC/VSC: {reference.ExcludedSafetyCarLaps})",
            EnableRawData: true,
            EnablePredictionScan: true,
            EnableScanAllDrivers: true,
            EnableScanSingleDriver: false,
            EnableRaceTrace: true);
    }

    /// <summary>
    /// State shown when no race session is available for selected event.
    /// </summary>
    public static MainWindowEventUiState NoRaceSession(EventMeeting selectedEvent)
    {
        return new MainWindowEventUiState(
            DisplayText: BuildDisplayText(selectedEvent),
            TooltipText: "No race session found",
            EnableRawData: false,
            EnablePredictionScan: false,
            EnableScanAllDrivers: false,
            EnableScanSingleDriver: false,
            EnableRaceTrace: false);
    }

    /// <summary>
    /// State shown when race-data load fails.
    /// </summary>
    public static MainWindowEventUiState LoadError(EventMeeting? selectedEvent, string errorMessage)
    {
        var displayText = selectedEvent is null
            ? string.Empty
            : BuildDisplayText(selectedEvent);

        return new MainWindowEventUiState(
            DisplayText: displayText,
            TooltipText: $"Error loading race data: {errorMessage}",
            EnableRawData: false,
            EnablePredictionScan: false,
            EnableScanAllDrivers: false,
            EnableScanSingleDriver: false,
            EnableRaceTrace: false);
    }

    private static string BuildDisplayText(EventMeeting selectedEvent) =>
        $"{selectedEvent.RaceName} ({selectedEvent.Year}) (Hover for Details)";
}

/// <summary>
/// Render-ready event-selection control and display state.
/// </summary>
public sealed record MainWindowEventUiState(
    string DisplayText,
    string TooltipText,
    bool EnableRawData,
    bool EnablePredictionScan,
    bool EnableScanAllDrivers,
    bool EnableScanSingleDriver,
    bool EnableRaceTrace);
