using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
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
    private List<TyreParameterRow> _tyreParameterRows =
    [
        new TyreParameterRow("Soft",   0.0, 0.10, isEditable: false, isDegradationEditable: true),
        new TyreParameterRow("Medium", 0.1, 0.07, isEditable: true,  isDegradationEditable: true),
        new TyreParameterRow("Hard",   0.2, 0.04, isEditable: true,  isDegradationEditable: true)
    ];
    private double _fuelSecondsPer10Kg = 0.3;
    private double _fuelKg = 110;
    private int _targetResponseLaps = 1;
    private double _marginalThreshold = 0.25;
    private double _trafficPenalty = 0.3;
    private bool _applyAttackerTraffic;
    private bool _applyTargetTraffic;
    private double _warmUpPenalty = 0.3;
    private double _pitLaneLoss = 22.0;
    private bool _suppressMainSettingsHandlers;
    private List<MainScanResultRow> _mainScanRows = [];
    private PredictionResult? _mainPredictResult;

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

    private void TyreGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RenderRaceTrace));
    }

    private void ScenarioSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        if (TryParseInt(TargetResponseBox.Text, out var r))
            _targetResponseLaps = Math.Max(1, r);

        if (TryParseDouble(MarginalThresholdBox.Text, out var m))
            _marginalThreshold = m;
    }

    private void TrafficSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        if (TryParseDouble(TrafficPenaltyBox.Text, out var t))
            _trafficPenalty = t;
    }

    private void TrafficFlags_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        _applyAttackerTraffic = ApplyAttackerTrafficCheckBox.IsChecked == true;
        _applyTargetTraffic = ApplyTargetTrafficCheckBox.IsChecked == true;
    }

    private void PitStopSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressMainSettingsHandlers) return;

        if (TryParseDouble(WarmUpPenaltyBox.Text, out var w))
            _warmUpPenalty = w;

        if (TryParseDouble(PitLaneLossBox.Text, out var p))
            _pitLaneLoss = p;
    }

    private static bool TryParseDouble(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);

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

    private sealed class MainPredictLapRow
    {
        public string DriverLabel { get; init; } = string.Empty;
        public int LapNumber { get; init; }
        public string LapType { get; init; } = string.Empty;
        public string Compound { get; init; } = string.Empty;
        public int TyreAge { get; init; }
        public string Base { get; init; } = string.Empty;
        public string CompoundOffset { get; init; } = string.Empty;
        public string Degradation { get; init; } = string.Empty;
        public string WarmUp { get; init; } = string.Empty;
        public string Traffic { get; init; } = string.Empty;
        public string PitLoss { get; init; } = string.Empty;
        public string Total { get; init; } = string.Empty;
        public string Cumulative { get; init; } = string.Empty;
        public string Gap { get; init; } = string.Empty;
    }

    private sealed class DecisionLapItem
    {
        public int LapNumber { get; init; }
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

        var vm = new EventSelectorViewModel();
        DataContext = vm;

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
            WarmUpPenaltyBox.Text = _warmUpPenalty.ToString("F1", CultureInfo.InvariantCulture);
            PitLaneLossBox.Text = _pitLaneLoss.ToString("F1", CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressMainSettingsHandlers = false;
        }

        IncludePitLapsCheckBox.Checked += (_, _) => RenderRaceTrace();
        IncludePitLapsCheckBox.Unchecked += (_, _) => RenderRaceTrace();
        IncludeScVscLapsCheckBox.Checked += (_, _) => RenderRaceTrace();
        IncludeScVscLapsCheckBox.Unchecked += (_, _) => RenderRaceTrace();
        ApplyFuelCorrectionCheckBox.Checked += (_, _) => RenderRaceTrace();
        ApplyFuelCorrectionCheckBox.Unchecked += (_, _) => RenderRaceTrace();
        LegendSortComboBox.SelectionChanged += (_, _) => RenderRaceTrace();

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

            SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) (Hover for Details)";
            SelectedEventDisplay.ToolTip = $"Event selected: {vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year})";

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
                PredictionScanButton.IsEnabled = false;
                ScanAllDriversButton.IsEnabled = false;
                ScanSingleDriverButton.IsEnabled = false;
                ScanSingleAttackerCombo.Items.Clear();
                ScanSingleTargetCombo.Items.Clear();
                ScanTargetLapCombo.Items.Clear();
                ScanStartingGapBox.Text = string.Empty;
                MainScanSingleGrid.ItemsSource = null;
                MainScanSingleStatusText.Text = string.Empty;
                _mainScanRows = [];
                GetMainScanExportButton()?.SetCurrentValue(IsEnabledProperty, false);
                SetRaceTraceEnabled(false);
                RenderRaceTrace();

                SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) (Hover for Details)";
                SelectedEventDisplay.ToolTip = "Loading race data...";

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
                        PredictionScanButton.IsEnabled = true;
                        ScanAllDriversButton.IsEnabled = true;
                        SetRaceTraceEnabled(true);
                        PopulateMainScanDriverSelectors();
                        MainScanSingleGrid.ItemsSource = null;
                        MainScanSingleStatusText.Text = string.Empty;
                        _mainScanRows = [];
                        _mainPredictResult = null;
                        GetMainScanExportButton()?.SetCurrentValue(IsEnabledProperty, false);
                        PredictExportCsvButton.IsEnabled = false;
                        PredictResultGroup.Visibility = Visibility.Collapsed;
                        PredictValidationText.Visibility = Visibility.Collapsed;
                        PredictInitialGapBox.Text = string.Empty;
                        PredictLapGrid.ItemsSource = null;
                        PopulatePredictSelectors();
                        SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) (Hover for Details)";
                        SelectedEventDisplay.ToolTip =
                            $"Rows: {reference.TotalLapRows}\nSession laps: {reference.MaxSessionLapNumber}\nDrivers: {drivers.Count}\n" +
                            $"Reference sum (fuel-adjusted): {reference.SumLapTimeSeconds:F3}s\nAverage: {(reference.AverageLapTimeSeconds.HasValue ? reference.AverageLapTimeSeconds.Value.ToString("F3") : "N/A")}s\n" +
                            $"Fuel/lap: {reference.FuelEffectPerLapSeconds:F3}s\nClean laps: {reference.IncludedLaps} (pit-out: {reference.ExcludedPitOutLaps}, pit-in: {reference.ExcludedPitInLaps}, SC/VSC: {reference.ExcludedSafetyCarLaps})";
                        RenderRaceTrace();
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SetRaceTraceEnabled(false);
                        SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) (Hover for Details)";
                        SelectedEventDisplay.ToolTip = "No race session found";
                        RenderRaceTrace();
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RawDataButton.IsEnabled = false;
                    PredictionScanButton.IsEnabled = false;
                    ScanAllDriversButton.IsEnabled = false;
                    ScanSingleDriverButton.IsEnabled = false;
                    ScanSingleAttackerCombo.Items.Clear();
                    ScanSingleTargetCombo.Items.Clear();
                    ScanTargetLapCombo.Items.Clear();
                    ScanStartingGapBox.Text = string.Empty;
                    MainScanSingleGrid.ItemsSource = null;
                    MainScanSingleStatusText.Text = string.Empty;
                    _mainScanRows = [];
                    GetMainScanExportButton()?.SetCurrentValue(IsEnabledProperty, false);
                    SetRaceTraceEnabled(false);
                    SelectedEventDisplay.Text = $"{vm.SelectedEvent?.RaceName} ({vm.SelectedEvent?.Year}) (Hover for Details)";
                    SelectedEventDisplay.ToolTip = $"Error loading race data: {ex.Message}";
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

        PredictionScanButton.Click += (_, _) =>
        {
            // Prediction now runs directly in MainWindow Predict tab.
        };

        ScanAllDriversButton.Click += async (_, _) =>
        {
            if (_currentLaps.Count == 0) return;

            ScanAllDriversButton.IsEnabled = false;
            MainScanSingleStatusText.Text = "Scanning all drivers…";

            try
            {
                var rows = await RunScanInMainAsync(_currentLaps.Select(l => l.DriverNumber).Distinct().ToList());
                PublishScanRows(rows);
            }
            catch (Exception ex)
            {
                MainScanSingleGrid.ItemsSource = null;
                MainScanSingleStatusText.Text = $"Scan failed: {ex.Message}";
                _mainScanRows = [];
                GetMainScanExportButton()?.SetCurrentValue(IsEnabledProperty, false);
            }
            finally
            {
                ScanAllDriversButton.IsEnabled = _currentLaps.Count > 0;
            }
        };

        ScanSingleDriverButton.Click += async (_, _) =>
        {
            if (ScanSingleAttackerCombo.SelectedItem is not MainScanDriverItem attacker) return;

            ScanSingleDriverButton.IsEnabled = false;
            MainScanSingleStatusText.Text = "Scanning…";

            try
            {
                var rows = await RunScanInMainAsync([attacker.DriverNumber]);
                PublishScanRows(rows);
            }
            catch (Exception ex)
            {
                MainScanSingleGrid.ItemsSource = null;
                MainScanSingleStatusText.Text = $"Scan failed: {ex.Message}";
                _mainScanRows = [];
                GetMainScanExportButton()?.SetCurrentValue(IsEnabledProperty, false);
            }
            finally
            {
                ScanSingleDriverButton.IsEnabled =
                    ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
                    ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
            }
        };

        ScanSingleAttackerCombo.SelectionChanged += (_, _) =>
        {
            RefreshScenarioFromSelection();
            ScanSingleDriverButton.IsEnabled =
                ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
                ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
        };

        ScanSingleTargetCombo.SelectionChanged += (_, _) =>
        {
            PopulateTargetLapChoices();
            RefreshScenarioFromSelection();
            ScanSingleDriverButton.IsEnabled =
                ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
                ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
        };

        ScanTargetLapCombo.SelectionChanged += (_, _) => RefreshScenarioFromSelection();

        PredictAttackerCombo.SelectionChanged += (_, _) => TryDerivePredictInitialGap();
        PredictTargetCombo.SelectionChanged += (_, _) => TryDerivePredictInitialGap();
        PredictDecisionLapCombo.SelectionChanged += (_, _) => TryDerivePredictInitialGap();

        if (GetMainScanExportButton() is { } exportButton)
            exportButton.Click += (_, _) => ExportMainScanCsv();

        RunPredictionButton.Click += (_, _) => RunPredictionFromMain();
        PredictExportCsvButton.Click += (_, _) => ExportPredictionCsvFromMain();


    }

    private async Task<List<MainScanResultRow>> RunScanInMainAsync(IReadOnlyList<int> driverNumbers)
    {
        var minAge = TryParseInt(ScanMinTyreAgeBox.Text, out var ma) && ma >= 0 ? ma : 5;
        var modelParams = BuildModelParametersFromMain();
        var safetyCarWindows = BuildSafetyCarWindows(_currentRaceControlMessages).ToList();

        return await Task.Run(() => RunScanInMain(driverNumbers, minAge, modelParams, safetyCarWindows));
    }

    private List<MainScanResultRow> RunScanInMain(
        IReadOnlyList<int> driverNumbers,
        int minAge,
        LapModelParameters modelParams,
        List<TimeWindow> safetyCarWindows)
    {
        var referencePaceByDriver = DeriveReferencePacePerDriver(safetyCarWindows);

        var lapIndex = _currentLaps
            .GroupBy(l => l.DriverNumber)
            .ToDictionary(g => g.Key,
                g => g.Where(l => l.DateStart.HasValue && l.LapDuration.HasValue)
                      .ToDictionary(l => l.LapNumber));

        var orderPerLap = BuildOnTrackOrderPerLap(lapIndex);
        var driversByNumber = _currentDrivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var results = new List<MainScanResultRow>();
        var predictor = new PitSequencePredictor(new LapTimePredictor());

        foreach (var attackerNumber in driverNumbers)
        {
            if (!lapIndex.TryGetValue(attackerNumber, out var attackerLapMap)) continue;

            var attackerCode = driversByNumber.TryGetValue(attackerNumber, out var ad)
                ? (string.IsNullOrWhiteSpace(ad.Code) ? attackerNumber.ToString() : ad.Code)
                : attackerNumber.ToString();

            var attackerRefPace = referencePaceByDriver.GetValueOrDefault(attackerNumber, 0.0);
            if (attackerRefPace <= 0) continue;

            foreach (var lapNumber in attackerLapMap.Keys.Order())
            {
                var attackerLap = attackerLapMap[lapNumber];
                if (!attackerLap.DateStart.HasValue || !attackerLap.LapDuration.HasValue) continue;
                if (attackerLap.IsPitOutLap) continue;
                if (IsInAnySafetyCarWindow(attackerLap, safetyCarWindows)) continue;

                var (attackerCompound, attackerTyreAge) = GetTyreStateAtLap(attackerNumber, lapNumber);
                if (attackerTyreAge < minAge) continue;

                if (!orderPerLap.TryGetValue(lapNumber, out var order)) continue;
                var attackerPos = order.IndexOf(attackerNumber);
                if (attackerPos <= 0) continue;

                var targetNumber = order[attackerPos - 1];
                if (!lapIndex.TryGetValue(targetNumber, out var targetLapMap)) continue;

                var targetCode = driversByNumber.TryGetValue(targetNumber, out var td)
                    ? (string.IsNullOrWhiteSpace(td.Code) ? targetNumber.ToString() : td.Code)
                    : targetNumber.ToString();

                if (!targetLapMap.TryGetValue(lapNumber, out var targetDecisionLap)) continue;
                if (targetDecisionLap.IsPitOutLap) continue;
                if (IsInAnySafetyCarWindow(targetDecisionLap, safetyCarWindows)) continue;

                var targetRefPace = referencePaceByDriver.GetValueOrDefault(targetNumber, 0.0);
                if (targetRefPace <= 0) continue;

                if (!attackerLap.LapTimeAtSectorTwoLine.HasValue || !targetDecisionLap.LapTimeAtSectorTwoLine.HasValue)
                    continue;

                var attackerSectorTwoLine = AsUtc(attackerLap.DateStart.Value).AddSeconds(attackerLap.LapTimeAtSectorTwoLine.Value);
                var targetSectorTwoLine = AsUtc(targetDecisionLap.DateStart.Value).AddSeconds(targetDecisionLap.LapTimeAtSectorTwoLine.Value);
                var g0 = (attackerSectorTwoLine - targetSectorTwoLine).TotalSeconds;

                if (g0 <= 0) continue;

                var (targetCompound, targetTyreAge) = GetTyreStateAtLap(targetNumber, lapNumber);

                var attackerReplCompound = ParseCompound(GetComboText(ScanAttackerReplCompoundCombo));
                var targetReplCompound = ParseCompound(GetComboText(ScanTargetReplCompoundCombo));
                var attackerReplAge = TryParseInt(ScanAttackerReplAgeBox.Text, out var ara) ? ara : 0;
                var targetReplAge = TryParseInt(ScanTargetReplAgeBox.Text, out var tra) ? tra : 0;

                var request = new PredictionRequest(
                    EventName: SelectedEventDisplay.Text,
                    DecisionLap: lapNumber,
                    InitialAttackerGapToTargetSeconds: g0,
                    Attacker: new DriverPredictionState(attackerCode, attackerCode, attackerPos + 1, attackerRefPace, ParseCompound(attackerCompound), attackerTyreAge),
                    Target: new DriverPredictionState(targetCode, targetCode, attackerPos, targetRefPace, ParseCompound(targetCompound), targetTyreAge),
                    AttackerReplacementTyre: new TyreSetSpecification(attackerReplCompound, attackerReplAge),
                    TargetReplacementTyre: new TyreSetSpecification(targetReplCompound, targetReplAge),
                    TargetResponseLaps: 1,
                    ModelParameters: modelParams);

                try
                {
                    var prediction = predictor.Predict(request);
                    results.Add(new MainScanResultRow
                    {
                        Attacker = attackerCode,
                        Target = targetCode,
                        DecisionLap = lapNumber,
                        AttackerCompound = attackerCompound,
                        AttackerTyreAge = attackerTyreAge,
                        TargetCompound = targetCompound,
                        TargetTyreAge = targetTyreAge,
                        G0 = $"{g0:+0.000;-0.000;0.000}",
                        GapAtTargetPitLapComplete = FormatGap(prediction.GapAtTargetPitLapCompleteSeconds),
                        GapAtTargetOutLapComplete = FormatGap(prediction.GapAtTargetOutLapCompleteSeconds),
                        GapAtBothDriversNormalLapComplete = FormatGap(prediction.GapAtBothDriversNormalLapCompleteSeconds),
                        DeltaGAtTargetPitLapComplete = $"{prediction.DeltaGAtTargetPitLapCompleteSeconds:+0.000;-0.000;0.000}",
                        Result = prediction.Classification switch
                        {
                            UndercutClassification.PredictedAhead => "Ahead",
                            UndercutClassification.PredictedMarginal => "Marginal",
                            _ => "Behind"
                        }
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new MainScanResultRow
                    {
                        Attacker = attackerCode,
                        Target = targetCode,
                        DecisionLap = lapNumber,
                        AttackerCompound = attackerCompound,
                        AttackerTyreAge = attackerTyreAge,
                        TargetCompound = targetCompound,
                        TargetTyreAge = targetTyreAge,
                        G0 = $"{g0:+0.000;-0.000;0.000}",
                        GapAtTargetPitLapComplete = "—",
                        GapAtTargetOutLapComplete = "—",
                        GapAtBothDriversNormalLapComplete = "—",
                        DeltaGAtTargetPitLapComplete = "—",
                        Result = $"Error: {ex.Message}"
                    });
                }
            }
        }

        return results;
    }

    private void PublishScanRows(List<MainScanResultRow> rows)
    {
        _mainScanRows = rows;
        MainScanSingleGrid.ItemsSource = rows;

        var opportunityCount = rows.Count(r =>
            string.Equals(r.Result, "Ahead", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.Result, "Marginal", StringComparison.OrdinalIgnoreCase));

        MainScanSingleStatusText.Text =
            $"{rows.Count} decision laps scanned — {opportunityCount} opportunit{(opportunityCount == 1 ? "y" : "ies")} found";

        GetMainScanExportButton()?.SetCurrentValue(IsEnabledProperty, rows.Count > 0);
    }

    private static Dictionary<int, List<int>> BuildOnTrackOrderPerLap(
        Dictionary<int, Dictionary<int, EventLap>> lapIndex)
    {
        var allLapNumbers = lapIndex.Values
            .SelectMany(d => d.Keys)
            .Distinct()
            .Order();

        var result = new Dictionary<int, List<int>>();

        foreach (var lapNum in allLapNumbers)
        {
            var sectorTwoCrossings = new List<(int driverNumber, DateTime crossing)>();
            foreach (var (driverNum, lapMap) in lapIndex)
            {
                if (!lapMap.TryGetValue(lapNum, out var lap)) continue;
                if (!lap.DateStart.HasValue || !lap.LapTimeAtSectorTwoLine.HasValue) continue;

                var crossing = AsUtc(lap.DateStart.Value).AddSeconds(lap.LapTimeAtSectorTwoLine.Value);
                sectorTwoCrossings.Add((driverNum, crossing));
            }

            result[lapNum] = sectorTwoCrossings
                .OrderBy(x => x.crossing)
                .Select(x => x.driverNumber)
                .ToList();
        }

        return result;
    }

    private (string compound, int age) GetTyreStateAtLap(int driverNumber, int lapNumber)
    {
        var stint = _currentStints
            .Where(s => s.DriverNumber == driverNumber
                     && s.LapStart <= lapNumber
                     && (s.LapEnd == null || s.LapEnd >= lapNumber))
            .OrderByDescending(s => s.StintNumber)
            .FirstOrDefault();

        if (stint is not null)
        {
            var age = lapNumber - stint.LapStart + stint.TyreAgeAtStart;
            return (stint.Compound, age);
        }

        var lastPitOutLap = _currentLaps
            .Where(l => l.DriverNumber == driverNumber
                     && l.LapNumber <= lapNumber
                     && l.IsPitOutLap)
            .Select(l => l.LapNumber)
            .DefaultIfEmpty(1)
            .Max();

        return ("UNKNOWN", lapNumber - lastPitOutLap);
    }

    private Dictionary<int, double> DeriveReferencePacePerDriver(IReadOnlyList<TimeWindow> safetyCarWindows)
    {
        return _currentLaps
            .GroupBy(l => l.DriverNumber)
            .Select(g =>
            {
                var pitOutLaps = g.Where(l => l.IsPitOutLap)
                                  .Select(l => l.LapNumber)
                                  .ToHashSet();

                var pitInLaps = g.Where(l => l.IsPitOutLap)
                                 .Select(l => l.LapNumber - 1)
                                 .Where(lap => lap >= 1 && !pitOutLaps.Contains(lap))
                                 .ToHashSet();

                var cleanLaps = g
                    .Where(l => l.LapDuration.HasValue && l.DateStart.HasValue
                             && !l.IsPitOutLap
                             && !pitInLaps.Contains(l.LapNumber)
                             && !IsInAnySafetyCarWindow(l, safetyCarWindows))
                    .ToList();

                var avg = cleanLaps.Count > 0
                    ? cleanLaps.Average(l => (double)l.LapDuration!.Value)
                    : 0.0;

                return (DriverNumber: g.Key, Pace: avg);
            })
            .Where(x => x.Pace > 0)
            .ToDictionary(x => x.DriverNumber, x => x.Pace);
    }

    private static bool IsInAnySafetyCarWindow(EventLap lap, IReadOnlyList<TimeWindow> windows)
    {
        if (!lap.DateStart.HasValue || !lap.LapDuration.HasValue)
            return false;

        var lapStart = AsUtc(lap.DateStart.Value);
        var lapEnd = lapStart.AddSeconds(lap.LapDuration.Value);

        return windows.Any(w => lapStart <= w.EndUtc && lapEnd >= w.StartUtc);
    }

    private void PopulateMainScanDriverSelectors()
    {
        ScanSingleAttackerCombo.Items.Clear();
        ScanSingleTargetCombo.Items.Clear();

        var driversByNumber = _currentDrivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var ordered = _currentLaps
            .Select(l => l.DriverNumber)
            .Distinct()
            .Where(driversByNumber.ContainsKey)
            .OrderBy(n => n)
            .Select(n =>
            {
                var d = driversByNumber[n];
                var code = string.IsNullOrWhiteSpace(d.Code) ? n.ToString(CultureInfo.InvariantCulture) : d.Code;
                var display = string.IsNullOrWhiteSpace(d.BroadcastName) ? code : $"{code} – {d.BroadcastName}";
                return new MainScanDriverItem
                {
                    DriverNumber = n,
                    Code = code,
                    DisplayName = display
                };
            })
            .ToList();

        foreach (var item in ordered)
        {
            ScanSingleAttackerCombo.Items.Add(item);
            ScanSingleTargetCombo.Items.Add(item);
        }

        if (ScanSingleAttackerCombo.Items.Count > 0)
            ScanSingleAttackerCombo.SelectedIndex = 0;

        if (ScanSingleTargetCombo.Items.Count > 1)
            ScanSingleTargetCombo.SelectedIndex = 1;
        else if (ScanSingleTargetCombo.Items.Count > 0)
            ScanSingleTargetCombo.SelectedIndex = 0;

        PopulateTargetLapChoices();
        RefreshScenarioFromSelection();

        ScanSingleDriverButton.IsEnabled =
            ScanSingleAttackerCombo.SelectedItem is MainScanDriverItem &&
            ScanSingleTargetCombo.SelectedItem is MainScanDriverItem;
    }

    private Button? GetMainScanExportButton() => FindName("MainScanExportCsvButton") as Button;

    private void PopulateTargetLapChoices()
    {
        ScanTargetLapCombo.Items.Clear();

        var targetNumber = (ScanSingleTargetCombo.SelectedItem as MainScanDriverItem)?.DriverNumber;
        if (targetNumber is null)
            return;

        var laps = _currentLaps
            .Where(l => l.DriverNumber == targetNumber.Value && l.DateStart.HasValue && l.LapTimeAtSectorTwoLine.HasValue)
            .Select(l => l.LapNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        foreach (var lap in laps)
            ScanTargetLapCombo.Items.Add(new DecisionLapItem { LapNumber = lap });

        if (ScanTargetLapCombo.Items.Count > 0)
            ScanTargetLapCombo.SelectedIndex = 0;
    }

    private void PopulatePredictSelectors()
    {
        PredictAttackerCombo.Items.Clear();
        PredictTargetCombo.Items.Clear();
        PredictDecisionLapCombo.Items.Clear();

        var driversByNumber = _currentDrivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var orderedDrivers = _currentLaps
            .Select(l => l.DriverNumber)
            .Distinct()
            .Where(driversByNumber.ContainsKey)
            .OrderBy(n => n)
            .Select(n =>
            {
                var d = driversByNumber[n];
                var code = string.IsNullOrWhiteSpace(d.Code) ? n.ToString(CultureInfo.InvariantCulture) : d.Code;
                var display = string.IsNullOrWhiteSpace(d.BroadcastName) ? code : $"{code} – {d.BroadcastName}";
                return new MainScanDriverItem { DriverNumber = n, Code = code, DisplayName = display };
            })
            .ToList();

        foreach (var item in orderedDrivers)
        {
            PredictAttackerCombo.Items.Add(item);
            PredictTargetCombo.Items.Add(item);
        }

        if (PredictAttackerCombo.Items.Count > 0) PredictAttackerCombo.SelectedIndex = 0;
        if (PredictTargetCombo.Items.Count > 1) PredictTargetCombo.SelectedIndex = 1;
        else if (PredictTargetCombo.Items.Count > 0) PredictTargetCombo.SelectedIndex = 0;

        var maxLap = _currentLaps.Count > 0 ? _currentLaps.Max(l => l.LapNumber) : 0;
        for (var lap = 1; lap <= maxLap; lap++)
            PredictDecisionLapCombo.Items.Add(new DecisionLapItem { LapNumber = lap });

        if (PredictDecisionLapCombo.Items.Count > 0)
            PredictDecisionLapCombo.SelectedIndex = Math.Min(Math.Max(0, maxLap / 2 - 1), PredictDecisionLapCombo.Items.Count - 1);

        TryDerivePredictInitialGap();
    }

    private void TryDerivePredictInitialGap()
    {
        if (PredictAttackerCombo.SelectedItem is not MainScanDriverItem attacker ||
            PredictTargetCombo.SelectedItem is not MainScanDriverItem target ||
            PredictDecisionLapCombo.SelectedItem is not DecisionLapItem decisionLap)
        {
            return;
        }

        var attackerLap = _currentLaps.FirstOrDefault(l =>
            l.DriverNumber == attacker.DriverNumber &&
            l.LapNumber == decisionLap.LapNumber &&
            l.DateStart.HasValue &&
            l.LapTimeAtSectorTwoLine.HasValue);

        var targetLap = _currentLaps.FirstOrDefault(l =>
            l.DriverNumber == target.DriverNumber &&
            l.LapNumber == decisionLap.LapNumber &&
            l.DateStart.HasValue &&
            l.LapTimeAtSectorTwoLine.HasValue);

        if (attackerLap is null || targetLap is null)
            return;

        var attackerSectorTwoLine = AsUtc(attackerLap.DateStart!.Value).AddSeconds(attackerLap.LapTimeAtSectorTwoLine!.Value);
        var targetSectorTwoLine = AsUtc(targetLap.DateStart!.Value).AddSeconds(targetLap.LapTimeAtSectorTwoLine!.Value);
        var g0 = (attackerSectorTwoLine - targetSectorTwoLine).TotalSeconds;

        PredictInitialGapBox.Text = g0.ToString("F3", CultureInfo.InvariantCulture);
    }

    private void RunPredictionFromMain()
    {
        PredictValidationText.Visibility = Visibility.Collapsed;
        PredictResultGroup.Visibility = Visibility.Collapsed;

        if (PredictAttackerCombo.SelectedItem is not MainScanDriverItem attacker)
        {
            PredictValidationText.Text = "Select an attacking driver.";
            PredictValidationText.Visibility = Visibility.Visible;
            return;
        }

        if (PredictTargetCombo.SelectedItem is not MainScanDriverItem target)
        {
            PredictValidationText.Text = "Select a target driver.";
            PredictValidationText.Visibility = Visibility.Visible;
            return;
        }

        if (attacker.DriverNumber == target.DriverNumber)
        {
            PredictValidationText.Text = "Attacking and target drivers must be different.";
            PredictValidationText.Visibility = Visibility.Visible;
            return;
        }

        if (PredictDecisionLapCombo.SelectedItem is not DecisionLapItem decisionLap)
        {
            PredictValidationText.Text = "Select a decision lap.";
            PredictValidationText.Visibility = Visibility.Visible;
            return;
        }

        if (!TryParseDouble(PredictInitialGapBox.Text, out var initialGap))
        {
            PredictValidationText.Text = "Initial gap must be numeric.";
            PredictValidationText.Visibility = Visibility.Visible;
            return;
        }

        var attackerRefPace = DeriveReferencePacePerDriver(BuildSafetyCarWindows(_currentRaceControlMessages))
            .GetValueOrDefault(attacker.DriverNumber, 0.0);
        var targetRefPace = DeriveReferencePacePerDriver(BuildSafetyCarWindows(_currentRaceControlMessages))
            .GetValueOrDefault(target.DriverNumber, 0.0);

        if (attackerRefPace <= 0 || targetRefPace <= 0)
        {
            PredictValidationText.Text = "Reference pace could not be derived. Adjust data filters or input state.";
            PredictValidationText.Visibility = Visibility.Visible;
            return;
        }

        var (attackerCompound, attackerTyreAge) = GetTyreStateAtLap(attacker.DriverNumber, decisionLap.LapNumber);
        var (targetCompound, targetTyreAge) = GetTyreStateAtLap(target.DriverNumber, decisionLap.LapNumber);

        var attackerReplCompound = ParseCompound(GetComboText(ScanAttackerReplCompoundCombo));
        var targetReplCompound = ParseCompound(GetComboText(ScanTargetReplCompoundCombo));
        var attackerReplAge = TryParseInt(ScanAttackerReplAgeBox.Text, out var ara) ? ara : 0;
        var targetReplAge = TryParseInt(ScanTargetReplAgeBox.Text, out var tra) ? tra : 0;

        var request = new PredictionRequest(
            EventName: SelectedEventDisplay.Text,
            DecisionLap: decisionLap.LapNumber,
            InitialAttackerGapToTargetSeconds: initialGap,
            Attacker: new DriverPredictionState(attacker.Code, attacker.DisplayName, 2, attackerRefPace, ParseCompound(attackerCompound), attackerTyreAge),
            Target: new DriverPredictionState(target.Code, target.DisplayName, 1, targetRefPace, ParseCompound(targetCompound), targetTyreAge),
            AttackerReplacementTyre: new TyreSetSpecification(attackerReplCompound, attackerReplAge),
            TargetReplacementTyre: new TyreSetSpecification(targetReplCompound, targetReplAge),
            TargetResponseLaps: _targetResponseLaps,
            ModelParameters: BuildModelParametersFromMain());

        try
        {
            var predictor = new PitSequencePredictor(new LapTimePredictor());
            var result = predictor.Predict(request);
            _mainPredictResult = result;
            RenderPredictionResult(result, attacker.Code, target.Code);
            PredictExportCsvButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            PredictValidationText.Text = $"Prediction failed: {ex.Message}";
            PredictValidationText.Visibility = Visibility.Visible;
        }
    }

    private LapModelParameters BuildModelParametersFromMain()
    {
        var offsets = new Dictionary<TyreCompound, double>
        {
            [TyreCompound.Soft] = 0.0,
            [TyreCompound.Medium] = 0.1,
            [TyreCompound.Hard] = 0.2
        };

        var degRates = new Dictionary<TyreCompound, double>
        {
            [TyreCompound.Soft] = 0.10,
            [TyreCompound.Medium] = 0.07,
            [TyreCompound.Hard] = 0.04
        };

        foreach (var row in _tyreParameterRows)
        {
            var c = ParseCompound(row.Compound);
            offsets[c] = row.PaceOffset;
            degRates[c] = row.DegradationRate;
        }

        return new LapModelParameters(
            CompoundOffsetsSeconds: offsets,
            DegradationRatesSecondsPerLap: degRates,
            WarmUp: new WarmUpModelParameters(_warmUpPenalty),
            PitLaneLossSeconds: _pitLaneLoss,
            MarginalThresholdSeconds: _marginalThreshold,
            Traffic: new TrafficModelParameters(_applyAttackerTraffic, _applyTargetTraffic, _trafficPenalty));
    }

    private void RenderPredictionResult(PredictionResult result, string attackerCode, string targetCode)
    {
        var (bg, text) = result.Classification switch
        {
            UndercutClassification.PredictedAhead => (WpfColors.Green, "UNDERCUT PREDICTED SUCCESSFUL"),
            UndercutClassification.PredictedMarginal => (WpfColors.DarkGoldenrod, "UNDERCUT MARGINAL"),
            _ => (WpfColors.Crimson, "UNDERCUT NOT PREDICTED")
        };

        PredictClassificationBadge.Background = new SolidColorBrush(bg);
        PredictClassificationText.Text = text;
        PredictG0Label.Text = $"G₀ = {result.InitialGapSeconds:+0.000;-0.000;0.000} s";

        var attackerByLap = result.AttackerLaps.ToDictionary(l => l.LapNumber, l => l.CumulativePredictionTimeSeconds);
        var rows = new List<MainPredictLapRow>();

        var allLaps = result.AttackerLaps.Select(l => (Lap: l, IsAttacker: true))
            .Concat(result.TargetLaps.Select(l => (Lap: l, IsAttacker: false)))
            .OrderBy(x => x.Lap.LapNumber)
            .ThenBy(x => x.IsAttacker ? 0 : 1);

        foreach (var (lap, isAttacker) in allLaps)
        {
            var breakdown = lap.Breakdown;
            string gapText = string.Empty;
            if (!isAttacker && attackerByLap.TryGetValue(lap.LapNumber, out var attackerElapsed))
            {
                var gap = result.InitialGapSeconds + attackerElapsed - lap.CumulativePredictionTimeSeconds;
                gapText = FormatGap(gap);
            }

            rows.Add(new MainPredictLapRow
            {
                DriverLabel = isAttacker ? attackerCode : targetCode,
                LapNumber = lap.LapNumber,
                LapType = lap.IsPitLap ? "Pit" : lap.IsOutLap ? "Out" : "Normal",
                Compound = lap.Compound.ToString(),
                TyreAge = lap.TyreAgeAtStart,
                Base = breakdown.ReferencePaceSeconds.ToString("F3", CultureInfo.InvariantCulture),
                CompoundOffset = FormatTerm(breakdown.CompoundOffsetSeconds),
                Degradation = FormatTerm(breakdown.DegradationSeconds),
                WarmUp = FormatTerm(breakdown.WarmUpSeconds),
                Traffic = FormatTerm(breakdown.TrafficSeconds),
                PitLoss = FormatTerm(breakdown.PitLossSeconds),
                Total = breakdown.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture),
                Cumulative = lap.CumulativePredictionTimeSeconds.ToString("F3", CultureInfo.InvariantCulture),
                Gap = gapText
            });
        }

        PredictLapGrid.ItemsSource = rows;
        PredictResultGroup.Visibility = Visibility.Visible;
    }

    private void ExportPredictionCsvFromMain()
    {
        if (_mainPredictResult is null)
            return;

        var dlg = new SaveFileDialog
        {
            Title = "Export prediction to CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"undercut_prediction_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dlg.ShowDialog(this) != true)
            return;

        var result = _mainPredictResult;
        var inv = CultureInfo.InvariantCulture;

        using var writer = new StreamWriter(dlg.FileName, append: false, encoding: Encoding.UTF8);
        writer.WriteLine("# Prediction result");
        writer.WriteLine($"# Event,{SelectedEventDisplay.Text}");
        writer.WriteLine($"# Classification,{result.Classification}");
        writer.WriteLine();
        writer.WriteLine("Metric,Value");
        writer.WriteLine($"InitialGapSeconds,{result.InitialGapSeconds.ToString("F3", inv)}");
        writer.WriteLine($"GapAtTargetPitLapCompleteSeconds,{result.GapAtTargetPitLapCompleteSeconds.ToString("F3", inv)}");
        writer.WriteLine($"GapAtTargetOutLapCompleteSeconds,{result.GapAtTargetOutLapCompleteSeconds.ToString("F3", inv)}");
        writer.WriteLine($"GapAtBothDriversNormalLapCompleteSeconds,{result.GapAtBothDriversNormalLapCompleteSeconds.ToString("F3", inv)}");
    }

    private static TyreCompound ParseCompound(string? compoundText)
    {
        return TyreCompoundParser.FromOpenF1String(compoundText);
    }

    private static string GetComboText(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem cbi)
            return cbi.Content?.ToString() ?? string.Empty;

        return combo.Text ?? string.Empty;
    }

    private static string FormatTerm(double seconds)
    {
        return seconds switch
        {
            > 0 => $"+{seconds:0.000}",
            < 0 => $"{seconds:0.000}",
            _ => "0.000"
        };
    }

    private static string FormatGap(double gap)
    {
        return $"{gap:+0.000;-0.000;0.000} s";
    }

    private void RefreshScenarioFromSelection()
    {
        if (ScanSingleAttackerCombo.SelectedItem is not MainScanDriverItem attacker ||
            ScanSingleTargetCombo.SelectedItem is not MainScanDriverItem target)
        {
            ScanStartingGapBox.Text = string.Empty;
            return;
        }

        if (ScanTargetLapCombo.SelectedItem is not DecisionLapItem lapItem)
        {
            PopulateTargetLapChoices();
            if (ScanTargetLapCombo.SelectedItem is not DecisionLapItem selectedLap)
            {
                ScanStartingGapBox.Text = string.Empty;
                return;
            }
            lapItem = selectedLap;
        }

        var attackerLap = _currentLaps.FirstOrDefault(l =>
            l.DriverNumber == attacker.DriverNumber &&
            l.LapNumber == lapItem.LapNumber &&
            l.DateStart.HasValue &&
            l.LapTimeAtSectorTwoLine.HasValue);

        var targetLap = _currentLaps.FirstOrDefault(l =>
            l.DriverNumber == target.DriverNumber &&
            l.LapNumber == lapItem.LapNumber &&
            l.DateStart.HasValue &&
            l.LapTimeAtSectorTwoLine.HasValue);

        if (attackerLap is null || targetLap is null)
        {
            ScanStartingGapBox.Text = string.Empty;
            return;
        }

        var attackerSectorTwoLine = AsUtc(attackerLap.DateStart!.Value).AddSeconds(attackerLap.LapTimeAtSectorTwoLine!.Value);
        var targetSectorTwoLine = AsUtc(targetLap.DateStart!.Value).AddSeconds(targetLap.LapTimeAtSectorTwoLine!.Value);
        var g0 = (attackerSectorTwoLine - targetSectorTwoLine).TotalSeconds;

        ScanStartingGapBox.Text = g0.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);
    }

    private void ExportMainScanCsv()
    {
        if (_mainScanRows.Count == 0)
            return;

        var dlg = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "scan_results_mainwindow.csv",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog(this) != true)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Lap,Attacking,Target,AttackerCompound,AttackerTyreAge,TargetCompound,TargetTyreAge,G0,GapTargetPitLapComplete,GapTargetOutLapComplete,GapBothDriversNormalLapComplete,DeltaGTargetPitLapComplete,Result");

        foreach (var row in _mainScanRows)
        {
            sb.AppendLine(string.Join(",",
                row.DecisionLap.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(row.Attacker),
                EscapeCsv(row.Target),
                EscapeCsv(row.AttackerCompound),
                row.AttackerTyreAge.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(row.TargetCompound),
                row.TargetTyreAge.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(row.G0),
                EscapeCsv(row.GapAtTargetPitLapComplete),
                EscapeCsv(row.GapAtTargetOutLapComplete),
                EscapeCsv(row.GapAtBothDriversNormalLapComplete),
                EscapeCsv(row.DeltaGAtTargetPitLapComplete),
                EscapeCsv(row.Result)));
        }

        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
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
                return ScottPlot.Color.FromHex(clean);
            }
            catch
            {
                // fallback below
            }
        }

        var fallback = new[]
        {
            ScottPlot.Colors.Blue,
            ScottPlot.Colors.Red,
            ScottPlot.Colors.Green,
            ScottPlot.Colors.Orange,
            ScottPlot.Colors.Purple,
            ScottPlot.Colors.Brown,
            ScottPlot.Colors.Teal,
            ScottPlot.Colors.Magenta
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
