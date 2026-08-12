using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfBrush = System.Windows.Media.Brush;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColors = System.Windows.Media.Colors;
using ScottPlot;
using ScottPlot.WPF;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;
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
    private readonly HashSet<int> _selectedLegendDriverNumbers = [];
    private int? _hoveredDriverNumber;
    private int? _focusedDriverNumber;
    private bool _suppressLegendToggleHandlers;
    private List<TyreParameterRow> _tyreParameterRows =
    [
        new TyreParameterRow("Soft",   0.0, 0.10, 0.3, isEditable: false, isDegradationEditable: true),
        new TyreParameterRow("Medium", 0.1, 0.07, 0.3, isEditable: true,  isDegradationEditable: true),
        new TyreParameterRow("Hard",   0.2, 0.04, 0.3, isEditable: true,  isDegradationEditable: true)
    ];
    private double _fuelSecondsPer10Kg = 0.3;
    private double _fuelKg = 110;
    private int _targetResponseLaps = 1;
    private double _marginalThreshold = 0.25;
    private double _trafficPenalty = 0.3;
    private bool _applyAttackerTraffic;
    private bool _applyTargetTraffic;
    private double _pitLaneLoss = 22.0;
    private bool _suppressMainSettingsHandlers;
    private bool _suppressScanSelectionHandlers;
    private List<ScanRowData> _mainScanRows = [];
    private List<ScanRowData> _singleScanRows = [];
    private readonly IMainWindowEventDataClient _eventDataClient = new OpenF1RaceDataClient();
    private readonly EventSelectorViewModel _eventSelectorViewModel;

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
    /// Applies fuel-setting text edits to model state and refreshes the race trace when values change.
    /// </summary>
    private void FuelSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        var changed = false;

        if (TryParseDouble(FuelSecondsPer10KgBox.Text, out var s10))
        {
            var rounded = Math.Round(s10, 2);
            if (!rounded.Equals(FuelSecondsPer10Kg))
            {
                FuelSecondsPer10Kg = rounded;
                changed = true;
            }
        }

        if (TryParseDouble(FuelKgBox.Text, out var kg))
        {
            var rounded = Math.Round(kg, 2);
            if (!rounded.Equals(FuelKg))
            {
                FuelKg = rounded;
                changed = true;
            }
        }

        if (changed)
            RenderRaceTrace();
    }

    /// <summary>
    /// Triggers a deferred race-trace refresh after tyre parameter cell edits are committed.
    /// </summary>
    private void TyreGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RenderRaceTrace));
    }

    /// <summary>
    /// Updates scenario-level settings (target response laps and marginal threshold) from text inputs.
    /// </summary>
    private void ScenarioSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        if (TryParseInt(TargetResponseBox.Text, out var r))
            _targetResponseLaps = Math.Max(1, r);

        if (TryParseDouble(MarginalThresholdBox.Text, out var m))
            _marginalThreshold = Math.Max(0.0, m);
    }

    /// <summary>
    /// Updates traffic penalty value from the traffic settings input box.
    /// </summary>
    private void TrafficSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        if (TryParseDouble(TrafficPenaltyBox.Text, out var t))
            _trafficPenalty = t;
    }

    /// <summary>
    /// Applies attacker/target traffic toggle states from checkbox inputs.
    /// </summary>
    private void TrafficFlags_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        _applyAttackerTraffic = ApplyAttackerTrafficCheckBox.IsChecked == true;
        _applyTargetTraffic = ApplyTargetTrafficCheckBox.IsChecked == true;
    }

    /// <summary>
    /// Updates pit-stop model settings from text inputs.
    /// </summary>
    private void PitStopSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        if (TryParseDouble(PitLaneLossBox.Text, out var p))
            _pitLaneLoss = p;
    }

    /// <summary>
    /// Parses invariant-culture floating-point input used by numeric text boxes.
    /// </summary>
    private static bool TryParseDouble(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Parses invariant-culture integer input used by numeric text boxes.
    /// </summary>
    private static bool TryParseInt(string? text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private sealed class MainScanDriverItem
    {
        public int DriverNumber { get; init; }
        public string Code { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    private sealed class MainScanResultRow
    {
        public int DecisionLap { get; init; }
        public string Attacker { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public string AttackerCompound { get; init; } = string.Empty;
        public int AttackerTyreAge { get; init; }
        public string TargetCompound { get; init; } = string.Empty;
        public int TargetTyreAge { get; init; }
        public string G0 { get; init; } = string.Empty;
        public string GapAtTargetPitLapComplete { get; init; } = string.Empty;
        public string GapAtTargetOutLapComplete { get; init; } = string.Empty;
        public string GapAtBothDriversNormalLapComplete { get; init; } = string.Empty;
        public string DeltaGAtTargetPitLapComplete { get; init; } = string.Empty;
        public string Result { get; init; } = string.Empty;
    }

    private sealed class DecisionLapItem
    {
        public int LapNumber { get; init; }
        /// <summary>
        /// Returns lap number text used in selector controls.
        /// </summary>
        public override string ToString() => LapNumber.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Initialises the main workspace, wires UI events, and prepares the race trace panel.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        RaceTraceHost.Children.Add(_raceTracePlot);
        SetRaceTraceEnabled(false);
        RenderRaceTrace();

        _eventSelectorViewModel = new EventSelectorViewModel();
        DataContext = _eventSelectorViewModel;

        TyreGrid.ItemsSource = _tyreParameterRows;
        foreach (var row in _tyreParameterRows)
            row.PropertyChanged += (_, _) => RenderRaceTrace();

        _suppressMainSettingsHandlers = true;
        try
        {
            FuelSecondsPer10KgBox.Text = _fuelSecondsPer10Kg.ToString("F2", CultureInfo.InvariantCulture);
            FuelKgBox.Text = _fuelKg.ToString("F2", CultureInfo.InvariantCulture);
            TargetResponseBox.Text = _targetResponseLaps.ToString(CultureInfo.InvariantCulture);
            MarginalThresholdBox.Text = _marginalThreshold.ToString("F2", CultureInfo.InvariantCulture);
            TrafficPenaltyBox.Text = _trafficPenalty.ToString("F1", CultureInfo.InvariantCulture);
            ApplyAttackerTrafficCheckBox.IsChecked = _applyAttackerTraffic;
            ApplyTargetTrafficCheckBox.IsChecked = _applyTargetTraffic;
            PitLaneLossBox.Text = _pitLaneLoss.ToString("F1", CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressMainSettingsHandlers = false;
        }

        IncludePitLapsCheckBox.Checked += OnRaceTraceRefreshRequested;
        IncludePitLapsCheckBox.Unchecked += OnRaceTraceRefreshRequested;
        IncludeScVscLapsCheckBox.Checked += OnRaceTraceRefreshRequested;
        IncludeScVscLapsCheckBox.Unchecked += OnRaceTraceRefreshRequested;
        ApplyFuelCorrectionCheckBox.Checked += OnRaceTraceRefreshRequested;
        ApplyFuelCorrectionCheckBox.Unchecked += OnRaceTraceRefreshRequested;
        LegendSortComboBox.SelectionChanged += OnRaceTraceSortSelectionChanged;
        ShowPredictionMarkersCheckBox.Checked += OnPredictionMarkerOptionsChanged;
        ShowPredictionMarkersCheckBox.Unchecked += OnPredictionMarkerOptionsChanged;
        ShowPredictionGuidesCheckBox.Checked += OnPredictionMarkerOptionsChanged;
        ShowPredictionGuidesCheckBox.Unchecked += OnPredictionMarkerOptionsChanged;
        MarkerOnlySelectedAttackerCheckBox.Checked += OnPredictionMarkerOptionsChanged;
        MarkerOnlySelectedAttackerCheckBox.Unchecked += OnPredictionMarkerOptionsChanged;
        MarkerShowAheadCheckBox.Checked += OnPredictionMarkerOptionsChanged;
        MarkerShowAheadCheckBox.Unchecked += OnPredictionMarkerOptionsChanged;
        MarkerShowMarginalCheckBox.Checked += OnPredictionMarkerOptionsChanged;
        MarkerShowMarginalCheckBox.Unchecked += OnPredictionMarkerOptionsChanged;
        MarkerShowBehindCheckBox.Checked += OnPredictionMarkerOptionsChanged;
        MarkerShowBehindCheckBox.Unchecked += OnPredictionMarkerOptionsChanged;

        Loaded += OnMainWindowLoadedAsync;
        EventButton.Click += OnEventButtonClickAsync;
        RawDataButton.Click += OnRawDataButtonClick;
        PredictionScanButton.Click += OnPredictionScanButtonClick;
        ScanAllDriversButton.Click += OnScanAllDriversButtonClickAsync;
        ScanAllLapsButton.Click += OnScanAllLapsButtonClickAsync;
        SimulateAllUndercutsButton.Click += OnSimulateAllUndercutsButtonClickAsync;

        ScanSingleAttackerCombo.SelectionChanged += OnScanSingleAttackerSelectionChanged;
        ScanSingleTargetCombo.SelectionChanged += OnScanSingleTargetSelectionChanged;
        ScanTargetLapCombo.SelectionChanged += OnScanTargetLapSelectionChanged;

        GetMainScanShowAheadCheckBox()?.Checked += OnResultsFilterChanged;
        GetMainScanShowAheadCheckBox()?.Unchecked += OnResultsFilterChanged;
        GetMainScanShowMarginalCheckBox()?.Checked += OnResultsFilterChanged;
        GetMainScanShowMarginalCheckBox()?.Unchecked += OnResultsFilterChanged;
        GetMainScanShowBehindCheckBox()?.Checked += OnResultsFilterChanged;
        GetMainScanShowBehindCheckBox()?.Unchecked += OnResultsFilterChanged;

        ExportResultsCsvButton.Click += (_, _) => ExportDisplayedResultsCsv();
        ExportAllButton.Click += (_, _) => ExportAllScenarioData();
        ClearResultsButton.Click += (_, _) => ClearAllResultsAndRefreshGraph();
        ClearTraceSelectionButton.Click += (_, _) => ClearTraceSelection();
        SimulateSingleUndercutButton.Click += (_, _) => SimulateSingleUndercut();

    }

    private void OnRaceTraceRefreshRequested(object sender, RoutedEventArgs e)
    {
        RenderRaceTrace();
    }

    private void OnRaceTraceSortSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderRaceTrace();
    }

    private void OnPredictionMarkerOptionsChanged(object sender, RoutedEventArgs e)
    {
        RenderRaceTrace();
    }

    private void ClearTraceSelection()
    {
        _selectedLegendDriverNumbers.Clear();
        _focusedDriverNumber = null;
        _hoveredDriverNumber = null;
        RenderRaceTrace();
    }

    private void ClearAllResultsAndRefreshGraph()
    {
        _mainScanRows = [];
        _singleScanRows = [];
        MainScanSingleGrid.ItemsSource = null;
        MainScanSingleStatusText.Text = string.Empty;
        UpdateExportButtonsState();
        RenderRaceTrace();
    }

    private async void OnMainWindowLoadedAsync(object sender, RoutedEventArgs e)
    {
        var now = DateTime.UtcNow.Year;
        await _eventSelectorViewModel.LoadAsync(2023, now).ConfigureAwait(false);
    }

    private async void OnEventButtonClickAsync(object sender, RoutedEventArgs e)
    {
        var dlg = new EventSelectorWindow(_eventSelectorViewModel);
        var res = dlg.ShowDialog();
        if (res != true || _eventSelectorViewModel.SelectedEvent is null)
        {
            return;
        }

        ApplyEventUiState(EventWorkflowService.Selected(_eventSelectorViewModel.SelectedEvent));

        try
        {
            var meetingKey = _eventSelectorViewModel.SelectedEvent.MeetingKey;
            if (_currentMeetingKey == meetingKey)
            {
                return;
            }

            _currentMeetingKey = meetingKey;
            ResetLoadedEventDataState();
            ApplyEventUiState(EventWorkflowService.Loading(_eventSelectorViewModel.SelectedEvent));

            var loadResult = await EventWorkflowService.LoadAsync(
                _eventDataClient,
                meetingKey,
                FuelSecondsPer10Kg,
                FuelKg).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (loadResult.IsSessionCancelled)
                {
                    MessageBox.Show(
                        $"The race session for {_eventSelectorViewModel.SelectedEvent?.RaceName} has been cancelled.",
                        "Session Cancelled",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    ClearSelectedMeeting();
                    return;
                }

                if (!loadResult.HasRaceSession || loadResult.Reference is null)
                {
                    ApplyEventUiState(EventWorkflowService.NoRaceSession(_eventSelectorViewModel.SelectedEvent));
                    RenderRaceTrace();
                    return;
                }

                _currentLaps = loadResult.Laps;
                _currentDrivers = loadResult.Drivers;
                _currentStints = loadResult.Stints;
                _currentRaceControlMessages = loadResult.RaceControlMessages;
                _currentReference = loadResult.Reference;

                PopulateMainScanDriverSelectors();
                ClearAllDriversResults();
                ClearSingleScanResults();
                ApplyEventUiState(EventWorkflowService.Loaded(_eventSelectorViewModel.SelectedEvent, loadResult.Reference, loadResult.Drivers.Count));
                RenderRaceTrace();
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ResetLoadedEventDataState();
                ApplyEventUiState(EventWorkflowService.LoadError(_eventSelectorViewModel.SelectedEvent, ex.Message));
                RenderRaceTrace();
            });
        }
    }

    private void OnRawDataButtonClick(object sender, RoutedEventArgs e)
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
    }

    private void OnPredictionScanButtonClick(object sender, RoutedEventArgs e)
    {
        // Prediction now runs directly in MainWindow Predict tab.
    }

    private async void OnScanAllDriversButtonClickAsync(object sender, RoutedEventArgs e)
    {
        if (_currentLaps.Count == 0) return;

        ClearAllDriversResults("Scanning all drivers…");
        ClearSingleScanResults();
        ScanAllDriversButton.IsEnabled = false;

        try
        {
            var rows = await RunScanInMainAsync(_currentLaps.Select(l => l.DriverNumber).Distinct().ToList());
            PublishScanRows(rows);
        }
        catch (Exception ex)
        {
            ClearAllDriversResults($"Scan failed: {ex.Message}");
        }
        finally
        {
            ScanAllDriversButton.IsEnabled = _currentLaps.Count > 0;
        }
    }

    private async void OnSimulateAllUndercutsButtonClickAsync(object sender, RoutedEventArgs e)
    {
        if (ScanSingleAttackerCombo.SelectedItem is not MainScanDriverItem attacker) return;
        if (ScanSingleTargetCombo.SelectedItem is not MainScanDriverItem target) return;

        ClearAllDriversResults();
        ClearSingleScanResults("Scanning all laps for selected driver…");
        SimulateAllUndercutsButton.IsEnabled = false;

        try
        {
            var rows = await RunSingleDriverAllLapsAsync(attacker, target);
            PublishSingleScanRows(rows, $"{rows.Count} single-driver scenario{(rows.Count == 1 ? string.Empty : "s")} scanned.");
        }
        catch (Exception ex)
        {
            ClearSingleScanResults($"Scan failed: {ex.Message}");
        }
        finally
        {
            SimulateAllUndercutsButton.IsEnabled =
                ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
                ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
        }
    }

    private async void OnScanAllLapsButtonClickAsync(object sender, RoutedEventArgs e)
    {
        if (ScanSingleAttackerCombo.SelectedItem is not MainScanDriverItem attacker) return;

        ClearAllDriversResults();
        ClearSingleScanResults("Scanning all laps for selected driver…");
        ScanAllLapsButton.IsEnabled = false;

        try
        {
            var rows = await RunScanAllLapsForAttackerAsync(attacker);
            PublishSingleScanRows(rows, $"{rows.Count} scenario{(rows.Count == 1 ? string.Empty : "s")} scanned.");
        }
        catch (Exception ex)
        {
            ClearSingleScanResults($"Scan failed: {ex.Message}");
        }
        finally
        {
            ScanAllLapsButton.IsEnabled =
                ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem;
        }
    }

    private void OnScanSingleAttackerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressScanSelectionHandlers) return;

        RefreshScenarioFromSelection(forceAutoSelectTarget: true);
        SimulateAllUndercutsButton.IsEnabled =
            ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
            ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
        ScanAllLapsButton.IsEnabled =
            ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem;
    }

    private void OnScanSingleTargetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressScanSelectionHandlers) return;

        PopulateTargetLapChoices();
        RefreshScenarioFromSelection(forceAutoSelectTarget: false);
        SimulateAllUndercutsButton.IsEnabled =
            ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
            ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
    }

    private void OnScanTargetLapSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressScanSelectionHandlers) return;
        RefreshScenarioFromSelection(forceAutoSelectTarget: true);
    }

    private void OnResultsFilterChanged(object sender, RoutedEventArgs e)
    {
        if (_singleScanRows.Count > 0)
            RefreshSingleScanResultsView();
        else
            RefreshMainScanResultsView();
    }

    /// <summary>
    /// Collects scan inputs from UI state and runs the full-scan pipeline asynchronously.
    /// </summary>
    private async Task<List<ScanRowData>> RunScanInMainAsync(IReadOnlyList<int> driverNumbers)
    {
        var minAge = TryParseInt(ScanMinTyreAgeBox.Text, out var ma) && ma >= 0 ? ma : 5;
        var modelParams = BuildModelParametersFromMain();
        var safetyCarWindows = BuildSafetyCarWindows(_currentRaceControlMessages).ToList();

        var orchestrationInput = new MainWindowScanOrchestrationInput(
            DriverNumbers: driverNumbers,
            MinTyreAge: minAge,
            EventName: SelectedEventDisplay.Text,
            AttackerReplacementTyre: new TyreSetSpecification(
                ParseCompound(GetComboText(ScanAttackerReplCompoundCombo)),
                TryParseInt(ScanAttackerReplAgeBox.Text, out var ara) ? ara : 0),
            TargetReplacementTyre: new TyreSetSpecification(
                ParseCompound(GetComboText(ScanTargetReplCompoundCombo)),
                TryParseInt(ScanTargetReplAgeBox.Text, out var tra) ? tra : 0),
            TargetResponseLaps: 1,
            ModelParameters: modelParams,
            SafetyCarWindows: safetyCarWindows,
            Laps: _currentLaps,
            Drivers: _currentDrivers,
            TyreStateResolver: GetTyreStateAtLap);

        return await Task.Run(() => ScanWorkflowService.Run(orchestrationInput));
    }

    /// <summary>
    /// Runs single-driver scans for all laps, comparing the attacker against the target driver.
    /// </summary>
    private async Task<List<ScanRowData>> RunSingleDriverAllLapsAsync(MainScanDriverItem attacker, MainScanDriverItem target)
    {
        var rows = new List<ScanRowData>();

        // Get all valid decision laps for the attacker driver
        var availableLaps = WorkspaceWorkflowService.BuildTargetDecisionLapChoices(_currentLaps, attacker.DriverNumber);

        foreach (var lapNumber in availableLaps)
        {
            // Calculate the actual gap between attacker and target at this lap (not the static gap from the text box)
            string gapAtLap = GetGapBetweenDriversAtLap(attacker.DriverNumber, target.DriverNumber, lapNumber);
            if (string.IsNullOrEmpty(gapAtLap))
                continue; // Skip lap if gap cannot be calculated

            var runInput = new MainWindowSingleScanRunInput(
                EventName: SelectedEventDisplay.Text,
                Attacker: new PredictionSelection(attacker.DriverNumber, attacker.Code, attacker.DisplayName),
                Target: new PredictionSelection(target.DriverNumber, target.Code, target.DisplayName),
                DecisionLapNumber: lapNumber,
                StartingGapText: gapAtLap,
                AttackerPaceOverrideText: ScanAttackerPaceBox.Text,
                TargetPaceOverrideText: ScanTargetPaceBox.Text,
                DerivedReferencePaceByDriver: DeriveReferencePacePerDriver(BuildSafetyCarWindows(_currentRaceControlMessages)),
                AttackerCompound: GetTyreStateAtLap(attacker.DriverNumber, lapNumber).compound,
                AttackerTyreAge: GetTyreStateAtLap(attacker.DriverNumber, lapNumber).age,
                TargetCompound: GetTyreStateAtLap(target.DriverNumber, lapNumber).compound,
                TargetTyreAge: GetTyreStateAtLap(target.DriverNumber, lapNumber).age,
                AttackerReplacementCompoundText: GetComboText(ScanAttackerReplCompoundCombo),
                AttackerReplacementAgeText: ScanAttackerReplAgeBox.Text,
                TargetReplacementCompoundText: GetComboText(ScanTargetReplCompoundCombo),
                TargetReplacementAgeText: ScanTargetReplAgeBox.Text,
                TargetResponseLaps: _targetResponseLaps,
                ModelParameters: BuildModelParametersFromMain());

            var result = SingleScanWorkflowService.Run(runInput);
            if (result.IsSuccess && result.Row is not null)
            {
                rows.Add(result.Row);
            }
        }

        return await Task.FromResult(rows);
    }

    private async Task<List<ScanRowData>> RunScanAllLapsForAttackerAsync(MainScanDriverItem attacker)
    {
        var rows = new List<ScanRowData>();

        // Get all valid decision laps for the attacker driver
        var availableLaps = WorkspaceWorkflowService.BuildTargetDecisionLapChoices(_currentLaps, attacker.DriverNumber);

        // Build on-track order for each lap
        var lapIndex = ScanWorkflowService.BuildLapIndex(_currentLaps);
        var orderPerLap = ScanWorkflowService.BuildOnTrackOrderPerLap(lapIndex);

        foreach (var lapNumber in availableLaps)
        {
            // Find the driver ahead on track for this lap
            int? targetDriverNumber = null;
            if (orderPerLap.TryGetValue(lapNumber, out var order))
            {
                var attackerPos = order.IndexOf(attacker.DriverNumber);
                if (attackerPos > 0)
                    targetDriverNumber = order[attackerPos - 1];
            }

            if (!targetDriverNumber.HasValue) continue;

            // Get target driver details
            var targetDriver = _currentDrivers.FirstOrDefault(d => d.DriverNumber == targetDriverNumber.Value);
            if (targetDriver is null) continue;

            var targetCode = string.IsNullOrWhiteSpace(targetDriver.Code)
                ? targetDriver.DriverNumber.ToString(CultureInfo.InvariantCulture)
                : targetDriver.Code;

            // Calculate the actual gap between attacker and target at this lap (not the static gap from the text box)
            string gapAtLap = GetGapBetweenDriversAtLap(attacker.DriverNumber, targetDriver.DriverNumber, lapNumber);
            if (string.IsNullOrEmpty(gapAtLap))
                continue; // Skip lap if gap cannot be calculated

            var runInput = new MainWindowSingleScanRunInput(
                EventName: SelectedEventDisplay.Text,
                Attacker: new PredictionSelection(attacker.DriverNumber, attacker.Code, attacker.DisplayName),
                Target: new PredictionSelection(targetDriver.DriverNumber, targetCode, targetDriver.BroadcastName),
                DecisionLapNumber: lapNumber,
                StartingGapText: gapAtLap,
                AttackerPaceOverrideText: ScanAttackerPaceBox.Text,
                TargetPaceOverrideText: ScanTargetPaceBox.Text,
                DerivedReferencePaceByDriver: DeriveReferencePacePerDriver(BuildSafetyCarWindows(_currentRaceControlMessages)),
                AttackerCompound: GetTyreStateAtLap(attacker.DriverNumber, lapNumber).compound,
                AttackerTyreAge: GetTyreStateAtLap(attacker.DriverNumber, lapNumber).age,
                TargetCompound: GetTyreStateAtLap(targetDriver.DriverNumber, lapNumber).compound,
                TargetTyreAge: GetTyreStateAtLap(targetDriver.DriverNumber, lapNumber).age,
                AttackerReplacementCompoundText: GetComboText(ScanAttackerReplCompoundCombo),
                AttackerReplacementAgeText: ScanAttackerReplAgeBox.Text,
                TargetReplacementCompoundText: GetComboText(ScanTargetReplCompoundCombo),
                TargetReplacementAgeText: ScanTargetReplAgeBox.Text,
                TargetResponseLaps: _targetResponseLaps,
                ModelParameters: BuildModelParametersFromMain());

            var result = SingleScanWorkflowService.Run(runInput);
            if (result.IsSuccess && result.Row is not null)
            {
                rows.Add(result.Row);
            }
        }

        return await Task.FromResult(rows);
    }

    /// <summary>
    /// Publishes full-scan rows and refreshes the results view.
    /// </summary>
    private void PublishScanRows(List<ScanRowData> rows)
    {
        _mainScanRows = rows;
        RefreshMainScanResultsView();
        RenderRaceTrace();
    }

    /// <summary>
    /// Publishes single-scan rows, updates status text, and toggles single-scan export availability.
    /// </summary>
    private void PublishSingleScanRows(List<ScanRowData> rows, string status)
    {
        _singleScanRows = rows;
        RefreshSingleScanResultsView(status);
        RenderRaceTrace();
    }

    /// <summary>
    /// Clears single-scan grid data and status text, and updates export button states.
    /// </summary>
    private void ClearSingleScanResults(string status = "")
    {
        _singleScanRows = [];
        MainScanSingleGrid.ItemsSource = null;
        MainScanSingleStatusText.Text = status;
        UpdateExportButtonsState();
    }

    /// <summary>
    /// Clears full-scan grid data and status text, and updates export button states.
    /// </summary>
    private void ClearAllDriversResults(string status = "")
    {
        _mainScanRows = [];
        MainScanSingleGrid.ItemsSource = null;
        MainScanSingleStatusText.Text = status;
        UpdateExportButtonsState();
    }

    /// <summary>
    /// Applies main-scan result filters, refreshes displayed rows, and updates summary status text.
    /// </summary>
    private void RefreshMainScanResultsView()
    {
        var state = ScanWorkflowService.BuildMainScanViewState(
            allRows: _mainScanRows,
            showAhead: GetMainScanShowAheadCheckBox()?.IsChecked,
            showMarginal: GetMainScanShowMarginalCheckBox()?.IsChecked,
            showBehind: GetMainScanShowBehindCheckBox()?.IsChecked);

        MainScanSingleGrid.ItemsSource = state.FilteredRows;
        MainScanSingleStatusText.Text = state.StatusText;
        UpdateExportButtonsState();
    }

    /// <summary>
    /// Applies single-scan result filters, refreshes displayed rows, and updates status text.
    /// </summary>
    private void RefreshSingleScanResultsView(string? baseStatus = null)
    {
        var state = ScanWorkflowService.BuildSingleScanViewState(
            allRows: _singleScanRows,
            baseStatus: baseStatus,
            showAhead: GetMainScanShowAheadCheckBox()?.IsChecked,
            showMarginal: GetMainScanShowMarginalCheckBox()?.IsChecked,
            showBehind: GetMainScanShowBehindCheckBox()?.IsChecked);

        MainScanSingleGrid.ItemsSource = state.FilteredRows;
        MainScanSingleStatusText.Text = state.StatusText;
        UpdateExportButtonsState();
    }

    /// <summary>
    /// Resolves a driver's compound and tyre age at a given lap using stints/laps fallback logic.
    /// </summary>
    private (string compound, int age) GetTyreStateAtLap(int driverNumber, int lapNumber)
    {
        return WorkspaceWorkflowService.ResolveTyreStateAtLap(_currentStints, _currentLaps, driverNumber, lapNumber);
    }

    /// <summary>
    /// Calculates the gap between two drivers at a specific lap using absolute Sector 2 crossing timestamps.
    /// Returns the gap formatted as a string (positive = attacker behind target, negative = attacker ahead).
    /// Returns empty string if gap cannot be calculated.
    /// </summary>
    private string GetGapBetweenDriversAtLap(int attackerNumber, int targetNumber, int lapNumber)
    {
        var gap = WorkspaceWorkflowService.TryDeriveInitialGapSeconds(
            laps: _currentLaps,
            attackerDriverNumber: attackerNumber,
            targetDriverNumber: targetNumber,
            decisionLapNumber: lapNumber);

        if (!gap.HasValue)
            return string.Empty;

        return gap.Value.ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Derives per-driver reference pace from clean laps, excluding pit transitions and safety-car affected laps.
    /// </summary>
    private Dictionary<int, double> DeriveReferencePacePerDriver(IReadOnlyList<TimeWindow> safetyCarWindows)
    {
        return RaceTraceWorkflowService.DeriveReferencePacePerDriver(_currentLaps, safetyCarWindows);
    }

    /// <summary>
    /// Populates single-scan attacker/target driver selectors from current session participants.
    /// </summary>
    private void PopulateMainScanDriverSelectors()
    {
        var ordered = WorkspaceWorkflowService
            .BuildOrderedDriverSelections(_currentDrivers, _currentLaps)
            .Select(s => new MainScanDriverItem
            {
                DriverNumber = s.DriverNumber,
                Code = s.Code,
                DisplayName = s.DisplayName
            })
            .ToList();

        _suppressScanSelectionHandlers = true;
        try
        {
            ScanSingleAttackerCombo.ItemsSource = null;
            ScanSingleTargetCombo.ItemsSource = null;

            ScanSingleAttackerCombo.ItemsSource = ordered;
            ScanSingleTargetCombo.ItemsSource = ordered;

            if (ordered.Count > 0)
                ScanSingleAttackerCombo.SelectedIndex = 0;

            if (ordered.Count > 1)
                ScanSingleTargetCombo.SelectedIndex = 1;
            else if (ordered.Count > 0)
                ScanSingleTargetCombo.SelectedIndex = 0;

            PopulateTargetLapChoices();
        }
        finally
        {
            _suppressScanSelectionHandlers = false;
        }

        RefreshScenarioFromSelection();

        SimulateAllUndercutsButton.IsEnabled =
            ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
            ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
    }

    /// <summary>
    /// Runs a one-scenario undercut simulation from the single-scan panel and publishes the result row.
    /// </summary>
    private void SimulateSingleUndercut()
    {
        ClearAllDriversResults();
        ClearSingleScanResults();

        var attackerSelection = ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem attacker
            ? new PredictionSelection(attacker.DriverNumber, attacker.Code, attacker.DisplayName)
            : null;

        var targetSelection = ScanSingleTargetCombo.SelectedItem is MainScanDriverItem target
            ? new PredictionSelection(target.DriverNumber, target.Code, target.DisplayName)
            : null;

        var decisionLap = (ScanTargetLapCombo.SelectedItem as DecisionLapItem)?.LapNumber;
        var effectiveLap = decisionLap ?? 0;

        var (attackerCompound, attackerTyreAge) = GetTyreStateAtLap(attackerSelection?.DriverNumber ?? 0, effectiveLap);
        var (targetCompound, targetTyreAge) = GetTyreStateAtLap(targetSelection?.DriverNumber ?? 0, effectiveLap);

        var runInput = new MainWindowSingleScanRunInput(
            EventName: SelectedEventDisplay.Text,
            Attacker: attackerSelection,
            Target: targetSelection,
            DecisionLapNumber: decisionLap,
            StartingGapText: ScanStartingGapBox.Text,
            AttackerPaceOverrideText: ScanAttackerPaceBox.Text,
            TargetPaceOverrideText: ScanTargetPaceBox.Text,
            DerivedReferencePaceByDriver: DeriveReferencePacePerDriver(BuildSafetyCarWindows(_currentRaceControlMessages)),
            AttackerCompound: attackerCompound,
            AttackerTyreAge: attackerTyreAge,
            TargetCompound: targetCompound,
            TargetTyreAge: targetTyreAge,
            AttackerReplacementCompoundText: GetComboText(ScanAttackerReplCompoundCombo),
            AttackerReplacementAgeText: ScanAttackerReplAgeBox.Text,
            TargetReplacementCompoundText: GetComboText(ScanTargetReplCompoundCombo),
            TargetReplacementAgeText: ScanTargetReplAgeBox.Text,
            TargetResponseLaps: _targetResponseLaps,
            ModelParameters: BuildModelParametersFromMain());

        var result = SingleScanWorkflowService.Run(runInput);

        if (!result.IsSuccess || result.Row is null)
        {
            PublishSingleScanRows([], result.StatusMessage);
            return;
        }

        PublishSingleScanRows([result.Row], result.StatusMessage);
    }

    /// <summary>
    /// Populates decision-lap options for the selected single-scan target while preserving the prior lap choice when possible.
    /// </summary>
    private void PopulateTargetLapChoices()
    {
        var previousLap = (ScanTargetLapCombo.SelectedItem as DecisionLapItem)?.LapNumber;
        var targetNumber = (ScanSingleTargetCombo.SelectedItem as MainScanDriverItem)?.DriverNumber;

        var lapNumbers = WorkspaceWorkflowService.BuildTargetDecisionLapChoices(_currentLaps, targetNumber);
        var laps = lapNumbers
            .Select(n => new DecisionLapItem { LapNumber = n })
            .ToList();

        _suppressScanSelectionHandlers = true;
        try
        {
            ScanTargetLapCombo.ItemsSource = null;
            ScanTargetLapCombo.ItemsSource = laps;

            var selectedLap = WorkspaceWorkflowService.ChooseDecisionLap(lapNumbers, previousLap);
            ScanTargetLapCombo.SelectedItem = selectedLap.HasValue
                ? laps.FirstOrDefault(x => x.LapNumber == selectedLap.Value)
                : null;
        }
        finally
        {
            _suppressScanSelectionHandlers = false;
        }
    }

    /// <summary>
    /// Builds lap-model parameters from default values overridden by the current tyre parameter editor inputs.
    /// </summary>
    private LapModelParameters BuildModelParametersFromMain()
    {
        return WorkspaceWorkflowService.Build(
            tyreParameterRows: _tyreParameterRows,
            pitLaneLoss: _pitLaneLoss,
            marginalThreshold: _marginalThreshold,
            applyAttackerTraffic: _applyAttackerTraffic,
            applyTargetTraffic: _applyTargetTraffic,
            trafficPenalty: _trafficPenalty);
    }

    /// <summary>
    /// Returns the main-scan Ahead filter checkbox if it exists in the loaded visual tree.
    /// </summary>
    private CheckBox? GetMainScanShowAheadCheckBox() => FindName("MainScanShowAheadCheckBox") as CheckBox;

    /// <summary>
    /// Returns the main-scan Marginal filter checkbox if it exists in the loaded visual tree.
    /// </summary>
    private CheckBox? GetMainScanShowMarginalCheckBox() => FindName("MainScanShowMarginalCheckBox") as CheckBox;

    /// <summary>
    /// Returns the main-scan Behind filter checkbox if it exists in the loaded visual tree.
    /// </summary>
    private CheckBox? GetMainScanShowBehindCheckBox() => FindName("MainScanShowBehindCheckBox") as CheckBox;


    /// <summary>
    /// Parses OpenF1 tyre compound text into the internal compound enum used by the prediction model.
    /// </summary>
    private static TyreCompound ParseCompound(string? compoundText)
    {
        return TyreCompoundParser.FromOpenF1String(compoundText);
    }

    /// <summary>
    /// Returns the selected combo-box text, supporting both explicit ComboBoxItem selections and free text.
    /// </summary>
    private static string GetComboText(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem cbi)
            return cbi.Content?.ToString() ?? string.Empty;

        return combo.Text ?? string.Empty;
    }

    /// <summary>
    /// Refreshes single-scan scenario inputs from current driver/lap selections, including auto-targeting and derived starting gap.
    /// </summary>
    private void RefreshScenarioFromSelection(bool forceAutoSelectTarget = false)
    {
        var attackerSelection = ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem attacker
            ? new PredictionSelection(attacker.DriverNumber, attacker.Code, attacker.DisplayName)
            : null;

        int? decisionLap = (ScanTargetLapCombo.SelectedItem as DecisionLapItem)?.LapNumber;
        if (!decisionLap.HasValue)
        {
            PopulateTargetLapChoices();
            decisionLap = (ScanTargetLapCombo.SelectedItem as DecisionLapItem)?.LapNumber;
        }

        var selectedTarget = ScanSingleTargetCombo.SelectedItem is MainScanDriverItem targetItem
            ? new PredictionSelection(targetItem.DriverNumber, targetItem.Code, targetItem.DisplayName)
            : null;

        var availableTargets = (ScanSingleTargetCombo.ItemsSource as IEnumerable<MainScanDriverItem>
                               ?? ScanSingleTargetCombo.Items.Cast<object>().OfType<MainScanDriverItem>())
            .Select(x => x.DriverNumber)
            .ToHashSet();

        var scenario = SingleScanWorkflowService.Derive(new MainWindowSingleScanScenarioInput(
            Attacker: attackerSelection,
            SelectedTarget: selectedTarget,
            DecisionLapNumber: decisionLap,
            AvailableTargetDriverNumbers: availableTargets,
            Laps: _currentLaps,
            Stints: _currentStints,
            RaceControlMessages: _currentRaceControlMessages));

        var presentation = SingleScanWorkflowService.Build(
            scenario,
            selectedTargetDriverNumber: selectedTarget?.DriverNumber,
            forceAutoSelectTarget: forceAutoSelectTarget);

        // Only auto-select target if we're not in the initial population phase and the user hasn't manually selected
        if (!_suppressScanSelectionHandlers && presentation.ShouldAutoSelectTarget && presentation.TargetDriverToSelect.HasValue)
        {
            var targetItems = ScanSingleTargetCombo.ItemsSource as IEnumerable<MainScanDriverItem>
                              ?? ScanSingleTargetCombo.Items.Cast<object>().OfType<MainScanDriverItem>();
            var matchingTarget = targetItems.FirstOrDefault(x => x.DriverNumber == presentation.TargetDriverToSelect.Value);
            if (matchingTarget is not null)
            {
                _suppressScanSelectionHandlers = true;
                try
                {
                    ScanSingleTargetCombo.SelectedItem = matchingTarget;
                }
                finally
                {
                    _suppressScanSelectionHandlers = false;
                }
            }
        }

        ScanAttackerPaceBox.Text = presentation.AttackerPaceText;
        ScanTargetPaceBox.Text = presentation.TargetPaceText;
        ScanAttackerCurrentCompoundBox.Text = presentation.AttackerCompoundText;
        ScanAttackerTyreAgeBox.Text = presentation.AttackerTyreAgeText;
        ScanTargetCurrentCompoundBox.Text = presentation.TargetCompoundText;
        ScanTargetTyreAgeBox.Text = presentation.TargetTyreAgeText;

        ScanStartingGapBox.Text = presentation.StartingGapText;
    }

    /// <summary>
    /// Exports whichever scan results are currently displayed in the shared results table.
    /// </summary>
    private void ExportDisplayedResultsCsv()
    {
        var rows = GetDisplayedResultsRows();
        if (rows.Count == 0)
            return;

        ExportScanRowsCsv(rows.ToList(), "scan_results.csv");
    }

    /// <summary>
    /// Exports full scenario context (event, inputs, loaded data, and any results) as JSON for reproducibility.
    /// </summary>
    private void ExportAllScenarioData()
    {
        var payload = BuildScenarioExportPayload();
        var json = WorkspaceWorkflowService.SerializeScenarioExport(payload);

        WorkspaceWorkflowService.TrySaveCsv(
            owner: this,
            csvContent: json,
            options: new MainWindowCsvSaveOptions(
                Title: "Export full scenario",
                Filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName: "undercut_scenario_export.json",
                DefaultExt: ".json",
                Encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
    }

    /// <summary>
    /// Writes rows content to a user-selected file using the shared save helper.
    /// </summary>
    private void ExportScanRowsCsv(List<ScanRowData> rows, string defaultFileName)
    {
        if (rows.Count == 0)
            return;

        var csv = ScanWorkflowService.BuildCsv(rows);
        WorkspaceWorkflowService.TrySaveCsv(
            owner: this,
            csvContent: csv,
            options: new MainWindowCsvSaveOptions(
                Title: null,
                Filter: "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName: defaultFileName,
                DefaultExt: ".csv",
                Encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
    }

    private IReadOnlyList<ScanRowData> GetDisplayedResultsRows()
    {
        return WorkspaceWorkflowService.ResolveDisplayedResultsRows(_mainScanRows, _singleScanRows);
    }

    private void UpdateExportButtonsState()
    {
        ExportResultsCsvButton.SetCurrentValue(IsEnabledProperty, GetDisplayedResultsRows().Count > 0);
        ExportAllButton.SetCurrentValue(IsEnabledProperty, WorkspaceWorkflowService.HasExportableScenarioData(_currentMeetingKey, _currentLaps, _currentDrivers));
    }

    private ScenarioExportPayload BuildScenarioExportPayload()
    {
        var attacker = ScanSingleAttackerCombo.SelectedItem as MainScanDriverItem;
        var target = ScanSingleTargetCombo.SelectedItem as MainScanDriverItem;
        var lap = (ScanTargetLapCombo.SelectedItem as DecisionLapItem)?.LapNumber;

        return WorkspaceWorkflowService.BuildScenarioExportPayload(
            exportedAtUtc: DateTime.UtcNow,
            eventDisplay: SelectedEventDisplay.Text,
            meetingKey: _currentMeetingKey,
            selection: new ScenarioExportSelection(
                AttackerDriverNumber: attacker?.DriverNumber,
                AttackerCode: attacker?.Code,
                TargetDriverNumber: target?.DriverNumber,
                TargetCode: target?.Code,
                DecisionLap: lap),
            parameters: new ScenarioExportParameters(
                FuelSecondsPer10Kg: _fuelSecondsPer10Kg,
                FuelKg: _fuelKg,
                TargetResponseLaps: _targetResponseLaps,
                MarginalThreshold: _marginalThreshold,
                TrafficPenalty: _trafficPenalty,
                ApplyAttackerTraffic: _applyAttackerTraffic,
                ApplyTargetTraffic: _applyTargetTraffic,
                PitLaneLoss: _pitLaneLoss,
                ScanMinTyreAge: TryParseInt(ScanMinTyreAgeBox.Text, out var minAge) ? minAge : null,
                AttackerReplacementCompound: GetComboText(ScanAttackerReplCompoundCombo),
                AttackerReplacementAge: TryParseInt(ScanAttackerReplAgeBox.Text, out var attackerAge) ? attackerAge : null,
                TargetReplacementCompound: GetComboText(ScanTargetReplCompoundCombo),
                TargetReplacementAge: TryParseInt(ScanTargetReplAgeBox.Text, out var targetAge) ? targetAge : null),
            drivers: _currentDrivers,
            laps: _currentLaps,
            stints: _currentStints,
            raceControlMessages: _currentRaceControlMessages,
            mainScanRows: _mainScanRows,
            singleScanRows: _singleScanRows);
    }


    /// <summary>
    /// Rebuilds the engineering race trace using current filters and fuel-correction settings.
    /// Each driver's line shows cumulative lap-by-lap delta to a constant reference lap.
    /// </summary>
    private void RenderRaceTrace()
    {
        RenderRaceTraceWithHoverOpacity(isHoverRefresh: false);
    }

    /// <summary>
    /// Converts SC/VSC race-control messages into UTC activity windows for filtering laps.
    /// </summary>
    private static IReadOnlyList<TimeWindow> BuildSafetyCarWindows(IReadOnlyList<RaceControlMessage> raceControlMessages)
    {
        return RaceTimingDomainLogic.BuildSafetyCarWindows(raceControlMessages);
    }

    /// <summary>
    /// Adds one interactive legend toggle that controls visibility of a driver's trace line.
    /// Right-click isolates that single driver.
    /// </summary>
    private void AddLegendToggle(int driverNumber, string driverName, string driverCode, WpfBrush textBrush, bool isSolid, bool isVisible, ScottPlot.Color lineColor)
    {
        // Create a Grid to hold the colored box and label together
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        // Create the colored box with driver acronym
        var scottnColor = System.Windows.Media.Color.FromArgb(
            lineColor.A, lineColor.R, lineColor.G, lineColor.B);
        var coloredBox = new Border
        {
            Background = new WpfSolidColorBrush(scottnColor),
            BorderBrush = new WpfSolidColorBrush(WpfColors.DarkGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = driverCode,
                Foreground = new WpfSolidColorBrush(WpfColors.White),
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 11,
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Width = 28
            }
        };
        Grid.SetColumn(coloredBox, 0);
        grid.Children.Add(coloredBox);

        // Create the text label
        var textBlock = new TextBlock
        {
            Text = RaceTraceWorkflowService.BuildLegendLabel(driverName, isSolid),
            Foreground = textBrush,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        Grid.SetColumn(textBlock, 1);
        grid.Children.Add(textBlock);

        var toggle = new ToggleButton
        {
            IsChecked = _selectedLegendDriverNumbers.Contains(driverNumber),
            Margin = new Thickness(0, 0, 8, 6),
            Padding = new Thickness(8, 3, 8, 3),
            BorderThickness = new Thickness(1),
            BorderBrush = new WpfSolidColorBrush(WpfColors.LightGray),
            Content = grid
        };

        toggle.MouseEnter += (_, _) =>
        {
            _hoveredDriverNumber = driverNumber;
            UpdateTraceOpacityForHover();
        };

        toggle.MouseLeave += (_, _) =>
        {
            _hoveredDriverNumber = null;
            UpdateTraceOpacityForHover();
        };

        toggle.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // Handle left-click selection ourselves to avoid ToggleButton state churn during hover-driven rerenders.
            e.Handled = true;
        };

        toggle.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_suppressLegendToggleHandlers)
                return;

            var next = RaceTraceWorkflowService.ApplyLeftClickSelection(
                _selectedLegendDriverNumbers,
                _focusedDriverNumber,
                clickedDriverNumber: driverNumber);

            _selectedLegendDriverNumbers.Clear();
            foreach (var selected in next.SelectedDriverNumbers)
                _selectedLegendDriverNumbers.Add(selected);
            _focusedDriverNumber = next.FocusedDriverNumber;

            e.Handled = true;
            RenderRaceTrace();
        };

        toggle.PreviewMouseRightButtonDown += (_, e) =>
        {
            // Prevent ToggleButton's default right-click state change behavior.
            e.Handled = true;
        };

        toggle.PreviewMouseRightButtonUp += (_, e) =>
        {
            if (_suppressLegendToggleHandlers)
                return;

            var next = RaceTraceWorkflowService.ApplyRightClickFocus(
                _selectedLegendDriverNumbers,
                _focusedDriverNumber,
                clickedDriverNumber: driverNumber);

            _selectedLegendDriverNumbers.Clear();
            foreach (var selected in next.SelectedDriverNumbers)
                _selectedLegendDriverNumbers.Add(selected);
            _focusedDriverNumber = next.FocusedDriverNumber;

            e.Handled = true;
            RenderRaceTrace();
        };

        var traceLegendPanel = FindName("TraceLegendPanel") as WrapPanel;
        traceLegendPanel?.Children.Add(toggle);
    }

    private void UpdateTraceOpacityForHover()
    {
        if (_raceTracePlot.Plot == null || _raceTracePlot.Plot.PlottableList.Count == 0)
            return;

        // Re-render for hover opacity changes, but preserve the user's current pan/zoom viewport.
        RenderRaceTraceWithHoverOpacity(isHoverRefresh: true);
    }

    private void RenderRaceTraceWithHoverOpacity(bool isHoverRefresh)
    {
        var plot = _raceTracePlot.Plot;
        plot.Clear();
        var traceLegendPanel = FindName("TraceLegendPanel") as WrapPanel;
        traceLegendPanel?.Children.Clear();

        plot.Title("Engineering Race Trace (Constant Reference)");
        plot.XLabel("Lap Number");
        plot.YLabel("Cumulative Delta to Constant Reference (s)");

        var computation = RaceTraceWorkflowService.Compute(
            laps: _currentLaps,
            drivers: _currentDrivers,
            raceControlMessages: _currentRaceControlMessages,
            options: new RaceTraceComputationOptions(
                IncludePitLaps: IncludePitLapsCheckBox.IsChecked != false,
                IncludeScVscLaps: IncludeScVscLapsCheckBox.IsChecked != false,
                ApplyFuelCorrection: ApplyFuelCorrectionCheckBox.IsChecked != false,
                FuelSecondsPer10Kg: FuelSecondsPer10Kg,
                FuelKg: FuelKg,
                SortLegendByTeamThenNumber: LegendSortComboBox.SelectedIndex == 1),
            traceVisibilityByDriver: _traceVisibilityByDriver);

        _traceVisibilityByDriver.Clear();
        foreach (var kvp in computation.NormalizedVisibilityByDriver)
            _traceVisibilityByDriver[kvp.Key] = true;

        var prunedSelected = RaceTraceWorkflowService.PruneSelectedDrivers(_selectedLegendDriverNumbers, computation.Series);
        _selectedLegendDriverNumbers.Clear();
        foreach (var selected in prunedSelected)
            _selectedLegendDriverNumbers.Add(selected);

        _focusedDriverNumber = RaceTraceWorkflowService.PruneFocusedDriver(_focusedDriverNumber, computation.Series);

        if (!computation.HasRenderableData)
        {
            _raceTracePlot.Refresh();
            return;
        }

        var markerOptions = new RaceTracePredictionMarkerOptions(
            ShowMarkers: ShowPredictionMarkersCheckBox.IsChecked == true,
            ShowGuides: ShowPredictionGuidesCheckBox.IsChecked == true,
            OnlySelectedAttacker: MarkerOnlySelectedAttackerCheckBox.IsChecked == true,
            ShowAhead: MarkerShowAheadCheckBox.IsChecked != false,
            ShowMarginal: MarkerShowMarginalCheckBox.IsChecked != false,
            ShowBehind: MarkerShowBehindCheckBox.IsChecked != false);

        var selectedAttacker = ScanSingleAttackerCombo.SelectedItem as MainScanDriverItem;
        var markerSelections = RaceTraceWorkflowService.ResolvePredictionMarkerSelections(
            rows: GetDisplayedResultsRows(),
            drivers: _currentDrivers,
            options: markerOptions,
            selectedAttackerDriverNumber: selectedAttacker?.DriverNumber);

        var seriesByDriver = computation.Series.ToDictionary(s => s.DriverNumber, s => s);
        var displayColorByDriver = new Dictionary<int, Color>();
        var focusedPlottables = new List<IPlottable>();
        var hoveredPlottables = new List<IPlottable>();

        void RenderDriverSeries(RaceTraceDriverSeries series)
        {
            var lineColor = RaceTraceWorkflowService.ParseScottPlotColor(series.TeamColour, series.DriverNumber);
            AddLegendToggle(
                series.DriverNumber,
                series.DriverName,
                series.DriverCode,
                RaceTraceWorkflowService.ParseLegendBrush(series.TeamColour, series.DriverNumber),
                series.IsSolidLine,
                series.IsVisible,
                lineColor);

            if (!series.IsVisible)
                return;

            var displayColor = lineColor;
            var dimmingMix = RaceTraceWorkflowService.ResolveDimmingMix(
                driverNumber: series.DriverNumber,
                selectedDriverNumbers: _selectedLegendDriverNumbers,
                focusedDriverNumber: _focusedDriverNumber,
                hoveredDriverNumber: _hoveredDriverNumber);
            if (dimmingMix.HasValue)
                displayColor = displayColor.MixedWith(ScottPlot.Colors.White, dimmingMix.Value);

            displayColorByDriver[series.DriverNumber] = displayColor;

            var scatter = plot.Add.Scatter(series.Xs.ToArray(), series.Ys.ToArray());
            scatter.LineColor = displayColor;
            scatter.LineWidth = 2;
            scatter.LinePattern = series.IsSolidLine ? LinePattern.Solid : LinePattern.Dashed;
            scatter.MarkerSize = markerOptions.ShowMarkers ? 0 : 8;
            scatter.MarkerFillColor = displayColor;
            scatter.MarkerLineColor = displayColor;

            var isSelectedFocusedSeries = _selectedLegendDriverNumbers.Contains(series.DriverNumber);
            var isFocusedSeries = isSelectedFocusedSeries || (_focusedDriverNumber.HasValue && series.DriverNumber == _focusedDriverNumber.Value);
            var isHoveredSeries = _hoveredDriverNumber.HasValue && series.DriverNumber == _hoveredDriverNumber.Value;
            if (isFocusedSeries)
                focusedPlottables.Add(scatter);
            if (isHoveredSeries)
                hoveredPlottables.Add(scatter);

            if (series.Xs.Count > 0 && !string.IsNullOrEmpty(series.DriverCode))
            {
                var lastX = series.Xs[series.Xs.Count - 1];
                var lastY = series.Ys[series.Ys.Count - 1];

                var annotation = plot.Add.Text(series.DriverCode, lastX, lastY);
                annotation.LabelFontSize = 10;
                annotation.OffsetX = 5;
                annotation.OffsetY = 0;
                annotation.LabelFontColor = displayColor;

                if (isFocusedSeries)
                    focusedPlottables.Add(annotation);
                if (isHoveredSeries)
                    hoveredPlottables.Add(annotation);
            }
        }

        _suppressLegendToggleHandlers = true;
        try
        {
            foreach (var series in computation.Series)
                RenderDriverSeries(series);
        }
        finally
        {
            _suppressLegendToggleHandlers = false;
        }

        if (markerOptions.ShowMarkers && markerSelections.Count > 0)
        {
            var markerGroups = markerSelections
                .GroupBy(m => (m.AttackerDriverNumber, m.DecisionLap, m.Result))
                .Select(g => g.First())
                .ToList();

            foreach (var marker in markerGroups)
            {
                if (!seriesByDriver.TryGetValue(marker.AttackerDriverNumber, out var markerSeries))
                    continue;

                if (!markerSeries.IsVisible)
                    continue;

                if (!RaceTraceWorkflowService.TryResolveSeriesPointAtLap(markerSeries, marker.DecisionLap, out var x, out var y))
                    continue;

                var seriesColor = RaceTraceWorkflowService.ParseScottPlotColor(markerSeries.TeamColour, markerSeries.DriverNumber);
                var markerColor = displayColorByDriver.TryGetValue(marker.AttackerDriverNumber, out var dimmedColor)
                    ? dimmedColor
                    : seriesColor;

                var symbol = RaceTraceWorkflowService.ResolveMarkerSymbol(marker.Result);
                if (string.IsNullOrEmpty(symbol))
                    continue;

                var textMarker = plot.Add.Text(symbol, x, y);
                textMarker.LabelFontSize = 18;
                textMarker.LabelFontColor = markerColor;
                textMarker.Alignment = Alignment.MiddleCenter;

                var isSelectedFocusedMarker = _selectedLegendDriverNumbers.Contains(marker.AttackerDriverNumber);
                var isFocusedMarker = isSelectedFocusedMarker || (_focusedDriverNumber.HasValue && marker.AttackerDriverNumber == _focusedDriverNumber.Value);
                var isHoveredMarker = _hoveredDriverNumber.HasValue && marker.AttackerDriverNumber == _hoveredDriverNumber.Value;
                if (isFocusedMarker)
                    focusedPlottables.Add(textMarker);
                if (isHoveredMarker)
                    hoveredPlottables.Add(textMarker);

                if (markerOptions.ShowGuides)
                {
                    var vline = plot.Add.VerticalLine(marker.DecisionLap);
                    vline.LinePattern = LinePattern.Dashed;
                    vline.LineColor = markerColor.WithAlpha(80);
                    vline.LineWidth = 1;
                }
            }
        }

        var baseline = plot.Add.HorizontalLine(0);
        baseline.Text = "Constant Reference";
        baseline.LineWidth = 1.5f;
        baseline.LinePattern = LinePattern.Dashed;

        foreach (var plottable in focusedPlottables)
            plot.MoveToFront(plottable);

        foreach (var plottable in hoveredPlottables)
            plot.MoveToFront(plottable);

        if (!isHoverRefresh)
            plot.Axes.AutoScale();

        _raceTracePlot.Refresh();
    }

    /// <summary>
    /// Applies event-selection control/display state to MainWindow controls.
    /// </summary>
    private void ApplyEventUiState(MainWindowEventUiState state)
    {
        SelectedEventDisplay.Text = state.DisplayText;
        SelectedEventDisplay.ToolTip = state.TooltipText;
        RawDataButton.IsEnabled = state.EnableRawData;
        PredictionScanButton.IsEnabled = state.EnablePredictionScan;
        ScanAllDriversButton.IsEnabled = state.EnableScanAllDrivers;
        // Note: ScanSingleDriverButton.IsEnabled is managed by selection handlers in PopulateMainScanDriverSelectors
        // Do not override it here, as it should reflect the current attacker/target selections
        SetRaceTraceEnabled(state.EnableRaceTrace);
        UpdateExportButtonsState();
    }

    /// <summary>
    /// Clears loaded event data and resets dependent controls to empty state.
    /// </summary>
    private void ResetLoadedEventDataState()
    {
        _currentLaps = Array.Empty<EventLap>();
        _currentDrivers = Array.Empty<Driver>();
        _currentStints = Array.Empty<EventStint>();
        _currentRaceControlMessages = Array.Empty<RaceControlMessage>();
        _currentReference = null;
        _traceVisibilityByDriver.Clear();
        _selectedLegendDriverNumbers.Clear();
        _focusedDriverNumber = null;
        ScanSingleAttackerCombo.ItemsSource = null;
        ScanSingleTargetCombo.ItemsSource = null;
        ScanTargetLapCombo.ItemsSource = null;
        ScanStartingGapBox.Text = string.Empty;
        ClearAllDriversResults();
        ClearSingleScanResults();
    }

    private void ClearSelectedMeeting()
    {
        _currentMeetingKey = null;
        _eventSelectorViewModel.SelectedEvent = null;
        ResetLoadedEventDataState();
        ApplyEventUiState(EventWorkflowService.NoEventSelected());
        RenderRaceTrace();
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
