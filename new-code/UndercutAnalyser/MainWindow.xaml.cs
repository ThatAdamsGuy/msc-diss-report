using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfBrush = System.Windows.Media.Brush;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColors = System.Windows.Media.Colors;
using ScottPlot;
using ScottPlot.WPF;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Infrastructure;
using UndercutAnalyser.Services;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser;

/// <summary>
/// Main application workspace for selecting events, loading race data, and viewing
/// the engineering race trace with configurable filtering options.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private int? _currentMeetingKey;
    private IReadOnlyList<EventLap> _currentLaps = Array.Empty<EventLap>();
    private IReadOnlyList<Driver> _currentDrivers = Array.Empty<Driver>();
    private IReadOnlyList<EventStint> _currentStints = Array.Empty<EventStint>();
    private IReadOnlyList<RaceControlMessage> _currentRaceControlMessages = Array.Empty<RaceControlMessage>();
    private ReferenceLapTimeResult? _currentReference;
    private readonly WpfPlot _raceTracePlot = new();
    private readonly Dictionary<int, bool> _traceVisibilityByDriver = new();
    private List<TyreParameterRow> _tyreParameterRows =
    [
        new TyreParameterRow("Soft",   0.0, 0.10, isEditable: false, isDegradationEditable: true),
        new TyreParameterRow("Medium", 0.5, 0.07, isEditable: true,  isDegradationEditable: true),
        new TyreParameterRow("Hard",   1.0, 0.04, isEditable: true,  isDegradationEditable: true)
    ];
    private double _fuelSecondsPer10Kg = 0.3;
    private double _fuelKg = 110;
    private StrategyWindow? _strategyWindow;

    public double FuelSecondsPer10Kg
    {
        get => _fuelSecondsPer10Kg;
        set => SetField(ref _fuelSecondsPer10Kg, value);
    }

    public double FuelKg
    {
        get => _fuelKg;
        set => SetField(ref _fuelKg, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Initialises the main workspace, wires UI events, and prepares the race trace panel.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        RaceTraceHost.Children.Add(_raceTracePlot);
        SetRaceTraceEnabled(false);
        RenderRaceTrace();

        var vm = new EventSelectorViewModel();
        DataContext = vm;

        IncludePitLapsCheckBox.Checked += (_, _) => RenderRaceTrace();
        IncludePitLapsCheckBox.Unchecked += (_, _) => RenderRaceTrace();
        IncludeScVscLapsCheckBox.Checked += (_, _) => RenderRaceTrace();
        IncludeScVscLapsCheckBox.Unchecked += (_, _) => RenderRaceTrace();
        ApplyFuelCorrectionCheckBox.Checked += (_, _) => RenderRaceTrace();
        ApplyFuelCorrectionCheckBox.Unchecked += (_, _) => RenderRaceTrace();
        LegendSortComboBox.SelectionChanged += (_, _) => RenderRaceTrace();

        ParametersButton.Click += (_, _) => OpenStrategyWindow(openOnPredict: false);

        Loaded += async (_, _) =>
        {
            var now = DateTime.UtcNow.Year;
            await vm.LoadAsync(2023, now).ConfigureAwait(false);
        };

        EventButton.Click += async (_, _) =>
        {
            var dlg = new EventSelectorWindow(vm);
            var res = dlg.ShowDialog();
            if (res != true || vm.SelectedEvent is null)
            {
                return;
            }

            SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year})";

            try
            {
                var meetingKey = vm.SelectedEvent.MeetingKey;
                if (_currentMeetingKey == meetingKey)
                {
                    return;
                }

                _currentMeetingKey = meetingKey;
                _currentLaps = Array.Empty<EventLap>();
                _currentDrivers = Array.Empty<Driver>();
                _currentStints = Array.Empty<EventStint>();
                _currentRaceControlMessages = Array.Empty<RaceControlMessage>();
                _currentReference = null;
                _traceVisibilityByDriver.Clear();
                RawDataButton.IsEnabled = false;
                PredictButton.IsEnabled = false;
                SetRaceTraceEnabled(false);
                RenderRaceTrace();

                SelectedEventDisplay.Text = "Loading race data...";

                var client = new OpenF1RaceDataClient();
                var sessions = await client.GetSessionsByMeetingKeyAsync(meetingKey).ConfigureAwait(false);
                var raceSession = sessions.FirstOrDefault(s =>
                    string.Equals(s.SessionName, "Race", StringComparison.OrdinalIgnoreCase) ||
                    (s.SessionName?.IndexOf("Race", StringComparison.OrdinalIgnoreCase) >= 0));

                if (raceSession is null)
                {
                    raceSession = sessions.Count > 0 ? sessions[0] : null;
                }

                if (raceSession is not null)
                {
                    var sessionKey = raceSession.SessionKey;
                    var laps = await client.GetLapsBySessionKeyAsync(sessionKey).ConfigureAwait(false);
                    var drivers = await client.GetDriversByMeetingAndSessionAsync(meetingKey, sessionKey).ConfigureAwait(false);
                    var raceControlMessages = await client.GetRaceControlMessagesBySessionKeyAsync(sessionKey).ConfigureAwait(false);
                    var stints = await client.GetStintsBySessionKeyAsync(sessionKey).ConfigureAwait(false);

                    var reference = ReferenceLapTimeCalculator.Calculate(
                        laps,
                        raceControlMessages,
                        FuelSecondsPer10Kg,
                        FuelKg);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _currentLaps = laps;
                        _currentDrivers = drivers;
                        _currentStints = stints;
                        _currentRaceControlMessages = raceControlMessages;
                        _currentReference = reference;
                        RawDataButton.IsEnabled = true;
                        PredictButton.IsEnabled = true;
                        SetRaceTraceEnabled(true);
                        SelectedEventDisplay.Text =
                            $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) - Rows: {reference.TotalLapRows}, Session laps: {reference.MaxSessionLapNumber}, Drivers: {drivers.Count}, " +
                            $"Reference sum (fuel-adjusted): {reference.SumLapTimeSeconds:F3}s, avg: {(reference.AverageLapTimeSeconds.HasValue ? reference.AverageLapTimeSeconds.Value.ToString("F3") : "N/A")}s, " +
                            $"Fuel/lap: {reference.FuelEffectPerLapSeconds:F3}s, Clean laps: {reference.IncludedLaps} (pit-out: {reference.ExcludedPitOutLaps}, pit-in: {reference.ExcludedPitInLaps}, SC/VSC: {reference.ExcludedSafetyCarLaps})";
                        RenderRaceTrace();
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SetRaceTraceEnabled(false);
                        SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) - No race session found";
                        RenderRaceTrace();
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RawDataButton.IsEnabled = false;
                    PredictButton.IsEnabled = false;
                    SetRaceTraceEnabled(false);
                    SelectedEventDisplay.Text = $"Error loading race data: {ex.Message}";
                    RenderRaceTrace();
                });
            }
        };

        RawDataButton.Click += (_, _) =>
        {
            if (_currentMeetingKey is null)
            {
                return;
            }

            var rawDataView = new RawDataView(
                _currentLaps,
                _currentDrivers,
                _currentStints,
                _currentReference,
                FuelSecondsPer10Kg,
                FuelKg)
            {
                Owner = this
            };
            rawDataView.Show();
        };

        PredictButton.Click += (_, _) => OpenStrategyWindow(openOnPredict: true);
    }

    /// <summary>
    /// Opens the strategy workspace window and selects either the Parameters or Predict tab.
    /// Reuses the same window instance so edits persist while the app is open.
    /// </summary>
    private void OpenStrategyWindow(bool openOnPredict)
    {
        if (_strategyWindow is null)
        {
            _strategyWindow = new StrategyWindow(
                FuelSecondsPer10Kg,
                FuelKg,
                _tyreParameterRows,
                SelectedEventDisplay.Text,
                _currentDrivers,
                _currentLaps,
                _currentStints,
                _currentRaceControlMessages)
            {
                Owner = this
            };

            _strategyWindow.ParametersChanged += (_, _) =>
            {
                FuelSecondsPer10Kg = _strategyWindow.FuelSecondsPer10Kg;
                FuelKg = _strategyWindow.FuelKg;
                _tyreParameterRows = _strategyWindow.GetTyreRowSnapshot().Select(r => r.Clone()).ToList();
                if (_currentLaps.Count > 0)
                    RenderRaceTrace();
            };
        }

        if (openOnPredict)
            _strategyWindow.MainTabs.SelectedItem = _strategyWindow.PredictTab;
        else
            _strategyWindow.MainTabs.SelectedItem = _strategyWindow.ParametersTab;

        _strategyWindow.Show();
        _strategyWindow.Activate();
    }

    /// <summary>
    /// Rebuilds the engineering race trace using current filters and fuel-correction settings.
    /// Each driver's line shows cumulative lap-by-lap delta to a constant reference lap.
    /// </summary>
    private void RenderRaceTrace()
    {
        var plot = _raceTracePlot.Plot;
        plot.Clear();
        var traceLegendPanel = FindName("TraceLegendPanel") as WrapPanel;
        traceLegendPanel?.Children.Clear();

        plot.Title("Engineering Race Trace (Constant Reference)");
        plot.XLabel("Lap Number");
        plot.YLabel("Cumulative Delta to Constant Reference (s)");

        if (_currentLaps.Count == 0)
        {
            _raceTracePlot.Refresh();
            return;
        }

        var includePitLaps = IncludePitLapsCheckBox.IsChecked != false;
        var includeScVscLaps = IncludeScVscLapsCheckBox.IsChecked != false;
        var applyFuelCorrection = ApplyFuelCorrectionCheckBox.IsChecked != false;

        var safetyCarWindows = BuildSafetyCarWindows(_currentRaceControlMessages);
        var maxSessionLapNumber = _currentLaps.Count > 0 ? _currentLaps.Max(l => l.LapNumber) : 0;
        var fuelEffectPerLapSeconds = applyFuelCorrection && maxSessionLapNumber > 0
            ? FuelSecondsPer10Kg * (FuelKg / 10.0) / maxSessionLapNumber
            : 0.0;

        var pitInLaps = BuildPitInLapLookup(_currentLaps);

        var displayEligibleLaps = _currentLaps
            .Where(l => l.LapDuration.HasValue && l.DateStart.HasValue)
            .Where(l => IsLapEligible(l, includePitLaps, includeScVscLaps, safetyCarWindows, pitInLaps))
            .Select(l => new
            {
                Lap = l,
                AdjustedLapSeconds = l.LapDuration!.Value - (fuelEffectPerLapSeconds * Math.Max(0, maxSessionLapNumber - l.LapNumber))
            })
            .ToList();

        if (displayEligibleLaps.Count == 0)
        {
            _raceTracePlot.Refresh();
            return;
        }

        // Build the constant reference from the shared clean-lap calculator so
        // diagnostics and plotted baseline always use the same derivation path.
        var referenceForTrace = ReferenceLapTimeCalculator.Calculate(
            _currentLaps,
            _currentRaceControlMessages,
            applyFuelCorrection ? FuelSecondsPer10Kg : 0.0,
            applyFuelCorrection ? FuelKg : 0.0);

        if (!referenceForTrace.AverageLapTimeSeconds.HasValue || referenceForTrace.AverageLapTimeSeconds.Value <= 0)
        {
            _raceTracePlot.Refresh();
            return;
        }

        var constantReference = referenceForTrace.AverageLapTimeSeconds.Value;

        var driverByNumber = _currentDrivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var lapsByDriver = displayEligibleLaps
            .GroupBy(x => x.Lap.DriverNumber)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Lap.LapNumber).ToList());

        var solidByDriver = lapsByDriver.Keys.ToDictionary(
            driverNumber => driverNumber,
            driverNumber => ShouldUseSolidLineForDriver(driverNumber, lapsByDriver.Keys, driverByNumber));

        var sortByTeamThenNumber = LegendSortComboBox.SelectedIndex == 1;
        var orderedDriverSeries = sortByTeamThenNumber
            ? lapsByDriver
                .OrderBy(x => GetTeamSortKey(x.Key, driverByNumber))
                .ThenBy(x => x.Key)
            : lapsByDriver
                .OrderBy(x => x.Key);

        var orderedDriverSeriesList = orderedDriverSeries.ToList();

        var activeDriverNumbers = orderedDriverSeriesList.Select(x => x.Key).ToHashSet();
        var staleKeys = _traceVisibilityByDriver.Keys.Where(k => !activeDriverNumbers.Contains(k)).ToList();
        foreach (var staleKey in staleKeys)
        {
            _traceVisibilityByDriver.Remove(staleKey);
        }

        foreach (var kvp in orderedDriverSeriesList)
        {
            var driverNumber = kvp.Key;
            var driverLaps = kvp.Value;

            var xs = new List<double>(driverLaps.Count);
            var ys = new List<double>(driverLaps.Count);

            var cumulativeDelta = 0.0;
            foreach (var item in driverLaps)
            {
                var lapNumber = item.Lap.LapNumber;
                var lapDelta = constantReference - item.AdjustedLapSeconds;
                cumulativeDelta += lapDelta;

                xs.Add(lapNumber);
                ys.Add(cumulativeDelta);
            }

            if (xs.Count == 0)
            {
                continue;
            }

            var driverName = driverByNumber.TryGetValue(driverNumber, out var driver)
                ? BuildLegendDriverName(driver, driverNumber)
                : driverNumber.ToString(CultureInfo.InvariantCulture);

            var teamColour = driverByNumber.TryGetValue(driverNumber, out var d) ? d.TeamColour : string.Empty;
            var lineColor = ParseScottPlotColor(teamColour, driverNumber);
            var isSolid = solidByDriver[driverNumber];
            var isVisible = !_traceVisibilityByDriver.TryGetValue(driverNumber, out var storedVisible) || storedVisible;
            _traceVisibilityByDriver[driverNumber] = isVisible;

            AddLegendToggle(driverNumber, driverName, ParseLegendBrush(teamColour, driverNumber), isSolid, isVisible);

            var displayColor = isVisible ? lineColor : lineColor.MixedWith(ScottPlot.Colors.White, 0.85);

            var scatter = plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            scatter.LineColor = displayColor;
            scatter.LineWidth = 2;
            scatter.LinePattern = isSolid ? LinePattern.Solid : LinePattern.Dashed;
            scatter.MarkerSize = 8;
            scatter.MarkerFillColor = displayColor;
            scatter.MarkerLineColor = displayColor;
        }

        // Future extension: add an alternative trace mode that plots absolute reference pace
        // rather than cumulative delta-to-reference.

        var baseline = plot.Add.HorizontalLine(0);
        baseline.Text = "Constant Reference";
        baseline.LineWidth = 1.5f;
        baseline.LinePattern = LinePattern.Dashed;

        plot.Axes.AutoScale();
        _raceTracePlot.Refresh();
    }

    /// <summary>
    /// Derives pit-in laps from pit-out flags (pit-in is lapNumber - 1 for the same driver).
    /// This allows pit-in exclusion even though OpenF1 exposes only pit-out flags.
    /// </summary>
    private static HashSet<(int DriverNumber, int LapNumber)> BuildPitInLapLookup(IReadOnlyList<EventLap> laps)
    {
        var lookup = new HashSet<(int DriverNumber, int LapNumber)>();

        foreach (var pitOutLap in laps.Where(l => l.IsPitOutLap && l.LapNumber > 1))
        {
            lookup.Add((pitOutLap.DriverNumber, pitOutLap.LapNumber - 1));
        }

        return lookup;
    }

    /// <summary>
    /// Returns whether a lap passes current plotting filters (pit-lap and SC/VSC inclusion).
    /// </summary>
    private static bool IsLapEligible(
        EventLap lap,
        bool includePitLaps,
        bool includeScVscLaps,
        IReadOnlyList<TimeWindow> safetyCarWindows,
        HashSet<(int DriverNumber, int LapNumber)> pitInLaps)
    {
        if (!includePitLaps)
        {
            if (lap.IsPitOutLap || pitInLaps.Contains((lap.DriverNumber, lap.LapNumber)))
            {
                return false;
            }
        }

        if (!includeScVscLaps && lap.DateStart.HasValue && lap.LapDuration.HasValue)
        {
            var lapStartUtc = AsUtc(lap.DateStart.Value);
            var lapEndUtc = lapStartUtc.AddSeconds(lap.LapDuration.Value);
            if (safetyCarWindows.Any(window => IntersectsWindow(lapStartUtc, lapEndUtc, window)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts SC/VSC race-control messages into UTC activity windows for filtering laps.
    /// </summary>
    private static IReadOnlyList<TimeWindow> BuildSafetyCarWindows(IReadOnlyList<RaceControlMessage> raceControlMessages)
    {
        var windows = new List<TimeWindow>();
        DateTime? vscStart = null;
        DateTime? scStart = null;

        foreach (var message in raceControlMessages
                     .Where(m => string.Equals(m.Category, "SafetyCar", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(m.Category, "Safety Car", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.Date))
        {
            if (!message.Date.HasValue)
            {
                continue;
            }

            var timestamp = AsUtc(message.Date.Value);
            var text = (message.Message ?? string.Empty).Trim().ToUpperInvariant();

            if (text.Contains("VSC DEPLOYED", StringComparison.Ordinal))
            {
                vscStart = timestamp;
                continue;
            }

            if (text.Contains("VSC ENDING", StringComparison.Ordinal))
            {
                if (vscStart.HasValue)
                {
                    windows.Add(new TimeWindow(vscStart.Value, timestamp));
                    vscStart = null;
                }

                continue;
            }

            if (text.Contains("SAFETY CAR DEPLOYED", StringComparison.Ordinal))
            {
                scStart = timestamp;
                continue;
            }

            if (text.Contains("SAFETY CAR IN THIS LAP", StringComparison.Ordinal) && scStart.HasValue)
            {
                windows.Add(new TimeWindow(scStart.Value, timestamp));
                scStart = null;
            }
        }

        return windows;
    }

    /// <summary>
    /// True when any portion of a lap interval overlaps the supplied SC/VSC window.
    /// </summary>
    private static bool IntersectsWindow(DateTime lapStartUtc, DateTime lapEndUtc, TimeWindow window)
    {
        return lapStartUtc <= window.EndUtc
               && lapEndUtc >= window.StartUtc;
    }

    /// <summary>
    /// Normalises DateTime values to UTC for consistent cross-lap time comparisons.
    /// </summary>
    private static DateTime AsUtc(DateTime input)
    {
        if (input.Kind == DateTimeKind.Utc)
        {
            return input;
        }

        if (input.Kind == DateTimeKind.Local)
        {
            return input.ToUniversalTime();
        }

        return DateTime.SpecifyKind(input, DateTimeKind.Utc);
    }

    /// <summary>
    /// Returns the median of a sorted numeric array (or 0 for empty input).
    /// </summary>
    private static double Median(double[] orderedValues)
    {
        if (orderedValues.Length == 0)
        {
            return 0;
        }

        var mid = orderedValues.Length / 2;
        return orderedValues.Length % 2 == 0
            ? (orderedValues[mid - 1] + orderedValues[mid]) / 2.0
            : orderedValues[mid];
    }

    /// <summary>
    /// Chooses line style so teammates can be distinguished: one solid, one dashed.
    /// </summary>
    private static bool ShouldUseSolidLineForDriver(
        int driverNumber,
        IEnumerable<int> plottedDriverNumbers,
        IReadOnlyDictionary<int, Driver> driverByNumber)
    {
        if (!driverByNumber.TryGetValue(driverNumber, out var currentDriver))
        {
            return true;
        }

        var groupKey = GetTeamGroupKey(currentDriver, driverNumber);

        var teammateNumbers = plottedDriverNumbers
            .Where(n => driverByNumber.TryGetValue(n, out var d) && GetTeamGroupKey(d, n) == groupKey)
            .ToArray();

        if (teammateNumbers.Length <= 1)
        {
            return true;
        }

        return driverNumber == teammateNumbers.Min();
    }

    /// <summary>
    /// Builds a stable grouping key for teammate detection (team name, then colour fallback).
    /// </summary>
    private static string GetTeamGroupKey(Driver driver, int driverNumber)
    {
        if (!string.IsNullOrWhiteSpace(driver.TeamName))
        {
            return "team:" + driver.TeamName.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(driver.TeamColour))
        {
            return "colour:" + driver.TeamColour.Trim().ToUpperInvariant();
        }

        return "driver:" + driverNumber.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Provides a legend sort key that clusters drivers by team where possible.
    /// </summary>
    private static string GetTeamSortKey(int driverNumber, IReadOnlyDictionary<int, Driver> driverByNumber)
    {
        if (!driverByNumber.TryGetValue(driverNumber, out var driver))
        {
            return "ZZZ";
        }

        if (!string.IsNullOrWhiteSpace(driver.TeamName))
        {
            return driver.TeamName.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(driver.TeamColour))
        {
            return driver.TeamColour.Trim().ToUpperInvariant();
        }

        return "ZZZ";
    }

    /// <summary>
    /// Builds a readable legend label combining driver code and broadcast name.
    /// </summary>
    private static string BuildLegendDriverName(Driver driver, int driverNumber)
    {
        var code = string.IsNullOrWhiteSpace(driver.Code)
            ? driverNumber.ToString(CultureInfo.InvariantCulture)
            : driver.Code;

        return string.IsNullOrWhiteSpace(driver.BroadcastName)
            ? code
            : $"{code} ({driver.BroadcastName})";
    }

    /// <summary>
    /// Adds one interactive legend toggle that controls visibility of a driver's trace line.
    /// Right-click isolates that single driver.
    /// </summary>
    private void AddLegendToggle(int driverNumber, string driverName, WpfBrush textBrush, bool isSolid, bool isVisible)
    {
        var stylePrefix = isSolid ? "━" : "┅";

        var toggle = new ToggleButton
        {
            IsChecked = isVisible,
            Margin = new Thickness(0, 0, 8, 6),
            Padding = new Thickness(8, 3, 8, 3),
            BorderThickness = new Thickness(1),
            BorderBrush = new WpfSolidColorBrush(WpfColors.LightGray),
            Content = new TextBlock
            {
                Text = $"{stylePrefix} {driverName}",
                Foreground = textBrush
            }
        };

        toggle.Checked += (_, _) =>
        {
            _traceVisibilityByDriver[driverNumber] = true;
            RenderRaceTrace();
        };

        toggle.Unchecked += (_, _) =>
        {
            _traceVisibilityByDriver[driverNumber] = false;
            RenderRaceTrace();
        };

        toggle.PreviewMouseRightButtonUp += (sender, e) =>
        {
            foreach (var key in _traceVisibilityByDriver.Keys.ToList())
            {
                _traceVisibilityByDriver[key] = key == driverNumber;
            }

            e.Handled = true;
            RenderRaceTrace();
        };

        var traceLegendPanel = FindName("TraceLegendPanel") as WrapPanel;
        traceLegendPanel?.Children.Add(toggle);
    }

    /// <summary>
    /// Parses a WPF brush from team colour hex, with deterministic fallback colours.
    /// </summary>
    private static WpfBrush ParseLegendBrush(string hex, int fallbackSeed)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            var clean = hex.Trim();
            if (!clean.StartsWith("#", StringComparison.Ordinal))
            {
                clean = "#" + clean;
            }

            try
            {
                var parsed = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(clean);
                return new WpfSolidColorBrush(parsed);
            }
            catch
            {
                // fallback below
            }
        }

        var fallback = new[]
        {
            WpfColors.Blue,
            WpfColors.Red,
            WpfColors.Green,
            WpfColors.Orange,
            WpfColors.Purple,
            WpfColors.Brown,
            WpfColors.Teal,
            WpfColors.Magenta
        };

        return new WpfSolidColorBrush(fallback[Math.Abs(fallbackSeed) % fallback.Length]);
    }

    /// <summary>
    /// Parses a ScottPlot colour from team colour hex, with deterministic fallback colours.
    /// </summary>
    private static ScottPlot.Color ParseScottPlotColor(string hex, int fallbackSeed)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            var clean = hex.Trim();
            if (!clean.StartsWith("#", StringComparison.Ordinal))
            {
                clean = "#" + clean;
            }

            try
            {
                return Color.FromHex(clean);
            }
            catch
            {
                // fallback below
            }
        }

        var fallback = new[]
        {
            Colors.Blue,
            Colors.Red,
            Colors.Green,
            Colors.Orange,
            Colors.Purple,
            Colors.Brown,
            Colors.Teal,
            Colors.Magenta
        };

        return fallback[Math.Abs(fallbackSeed) % fallback.Length];
    }

    /// <summary>
    /// Enables or disables race-trace interaction and dims the plot when disabled.
    /// </summary>
    private void SetRaceTraceEnabled(bool enabled)
    {
        _raceTracePlot.UserInputProcessor.IsEnabled = enabled;
        _raceTracePlot.Opacity = enabled ? 1.0 : 0.6;
    }

    /// <summary>
    /// Standard property setter helper that raises PropertyChanged only when value changes.
    /// </summary>
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
