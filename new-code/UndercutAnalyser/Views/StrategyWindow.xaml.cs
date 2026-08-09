using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser;

/// <summary>
/// Tabbed strategy window combining the Parameters and Predict workflows.
/// Parameter changes propagate immediately to MainWindow via <see cref="ParametersChanged"/>.
/// </summary>
public partial class StrategyWindow : Window
{
    // ── Parameters tab state ─────────────────────────────────────────────────

    public ObservableCollection<TyreParameterRow> TyreRows { get; }

    private double _fuelSecondsPer10Kg;
    private double _fuelKg;

    /// <summary>Current fuel seconds per 10 kg value (updated on LostFocus).</summary>
    public double FuelSecondsPer10Kg => _fuelSecondsPer10Kg;

    /// <summary>Current fuel kg value (updated on LostFocus).</summary>
    public double FuelKg => _fuelKg;

    /// <summary>
    /// Fired whenever any parameter (fuel or tyre) changes so MainWindow can
    /// re-render the race trace immediately.
    /// </summary>
    public event EventHandler? ParametersChanged;

    // ── Predict tab state ────────────────────────────────────────────────────

    private readonly IReadOnlyList<Driver> _drivers;
    private readonly IReadOnlyList<EventLap> _laps;
    private readonly IReadOnlyList<EventStint> _stints;
    private readonly IReadOnlyList<RaceControlMessage> _raceControlMessages;
    private readonly string _eventName;

    private readonly PredictViewModel _vm;

    // Predict tab backing fields (mirrors PredictViewModel for loose coupling)
    private bool _predictPopulated;
    private PredictionResult? _lastResult;

    // ── Scan tab result row ───────────────────────────────────────────────────

    private sealed class ScanResultRow
    {
        // Scenario identification
        public string Attacker   { get; init; } = string.Empty;
        public string Target     { get; init; } = string.Empty;
        public int    DecisionLap { get; init; }

        // Tyre state at decision point
        public string AttackerCompound { get; init; } = string.Empty;
        public int    AttackerTyreAge  { get; init; }
        public string TargetCompound   { get; init; } = string.Empty;
        public int    TargetTyreAge    { get; init; }

        // Gap context
        public string G0          { get; init; } = string.Empty;   // initial gap (s) formatted

        // Prediction outputs
        public string GapAtN1     { get; init; } = string.Empty;
        public string GapAtN2     { get; init; } = string.Empty;
        public string GapAtN3     { get; init; } = string.Empty;
        public string DeltaGN1    { get; init; } = string.Empty;
        public string Result      { get; init; } = string.Empty;   // Ahead / Marginal / Behind
        public bool   IsOpportunity { get; init; }                 // true = Ahead or Marginal
    }

    // ── Lap table row ────────────────────────────────────────────────────────

    private sealed class LapRow
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
        /// <summary>
        /// Gap G(lap) = G0 + attacker_elapsed − target_elapsed at the end of this lap.
        /// Only set for target rows (where both drivers have a completed lap).
        /// Empty string for attacker rows.
        /// </summary>
        public string Gap { get; init; } = string.Empty;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public StrategyWindow(
        double fuelSecondsPer10Kg,
        double fuelKg,
        IEnumerable<TyreParameterRow> tyreRows,
        string eventName,
        IReadOnlyList<Driver> drivers,
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<EventStint> stints,
        IReadOnlyList<RaceControlMessage> raceControlMessages)
    {
        _fuelSecondsPer10Kg = Math.Round(fuelSecondsPer10Kg, 2);
        _fuelKg = Math.Round(fuelKg, 2);
        _eventName = eventName;
        _drivers = drivers;
        _laps = laps;
        _stints = stints;
        _raceControlMessages = raceControlMessages;

        TyreRows = new ObservableCollection<TyreParameterRow>(tyreRows.Select(r => r.Clone()));

        _vm = new PredictViewModel();

        InitializeComponent();
        DataContext = this;

        // ── Parameters tab setup ───────────────────────────────────────────
        FuelSecondsPer10KgBox.Text = _fuelSecondsPer10Kg.ToString("F2", CultureInfo.InvariantCulture);
        FuelKgBox.Text = _fuelKg.ToString("F2", CultureInfo.InvariantCulture);

        // Notify MainWindow whenever any tyre row property changes
        foreach (var row in TyreRows)
            row.PropertyChanged += TyreRow_PropertyChanged;

        TyreGrid.ItemsSource = TyreRows;

        PopulateScanTab();
    }

    // ── Tab selection — populate Predict tab lazily on first switch ──────────

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabs.SelectedItem == PredictTab && !_predictPopulated)
        {
            PopulatePredictTab();
            _predictPopulated = true;
        }
    }

    // ── Closing: hide instead of destroy so MainWindow can re-show it ────────

    private void StrategyWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PARAMETERS TAB
    // ══════════════════════════════════════════════════════════════════════════

    private void FuelBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var changed = false;

        if (TryParseDouble(FuelSecondsPer10KgBox.Text, out var s10))
        {
            _fuelSecondsPer10Kg = Math.Round(s10, 2);
            FuelSecondsPer10KgBox.Text = _fuelSecondsPer10Kg.ToString("F2", CultureInfo.InvariantCulture);
            changed = true;
        }

        if (TryParseDouble(FuelKgBox.Text, out var kg))
        {
            _fuelKg = Math.Round(kg, 2);
            FuelKgBox.Text = _fuelKg.ToString("F2", CultureInfo.InvariantCulture);
            changed = true;
        }

        if (changed)
            ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TyreRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetParamsButton_Click(object sender, RoutedEventArgs e)
    {
        _fuelSecondsPer10Kg = 0.3;
        _fuelKg = 110;
        FuelSecondsPer10KgBox.Text = _fuelSecondsPer10Kg.ToString("F2", CultureInfo.InvariantCulture);
        FuelKgBox.Text = _fuelKg.ToString("F2", CultureInfo.InvariantCulture);

        var defaults = DefaultTyreRows();
        for (var i = 0; i < TyreRows.Count && i < defaults.Count; i++)
        {
            TyreRows[i].PaceOffset = defaults[i].PaceOffset;
            TyreRows[i].DegradationRate = defaults[i].DegradationRate;
            TyreRows[i].MinLaps = defaults[i].MinLaps;
            TyreRows[i].MaxLaps = defaults[i].MaxLaps;
        }

        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns a fresh snapshot of the tyre rows for external use (e.g. MainWindow).</summary>
    public IReadOnlyList<TyreParameterRow> GetTyreRowSnapshot() =>
        TyreRows.Select(r => r.Clone()).ToList();

    private static List<TyreParameterRow> DefaultTyreRows() =>
    [
        new TyreParameterRow("Soft",   0, 0, isEditable: false, isDegradationEditable: true),
        new TyreParameterRow("Medium", 0, 0, isEditable: true,  isDegradationEditable: true),
        new TyreParameterRow("Hard",   0, 0, isEditable: true,  isDegradationEditable: true)
    ];

    // ══════════════════════════════════════════════════════════════════════════
    // PREDICT TAB
    // ══════════════════════════════════════════════════════════════════════════

    private void PopulatePredictTab()
    {
        PopulateDrivers();
        PopulateLaps();
        PopulateDefaultPredictParams();
    }

    private void PopulateDrivers()
    {
        var driversByNumber = _drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var ordered = _laps
            .GroupBy(l => l.DriverNumber)
            .Where(g => driversByNumber.ContainsKey(g.Key))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var d = driversByNumber[g.Key];
                var code = string.IsNullOrWhiteSpace(d.Code)
                    ? g.Key.ToString(CultureInfo.InvariantCulture) : d.Code;
                var display = string.IsNullOrWhiteSpace(d.BroadcastName) ? code : $"{code} – {d.BroadcastName}";
                return new DriverItem
                {
                    DriverNumber = g.Key,
                    Code = code,
                    DisplayName = display,
                    TeamName = d.TeamName ?? string.Empty
                };
            })
            .ToList();

        foreach (var item in ordered)
        {
            _vm.Drivers.Add(item);
            AttackerCombo.Items.Add(item);
            TargetCombo.Items.Add(item);
        }

        if (AttackerCombo.Items.Count > 0) AttackerCombo.SelectedIndex = 0;
        if (TargetCombo.Items.Count > 1) TargetCombo.SelectedIndex = 1;
    }

    private void PopulateLaps()
    {
        if (_laps.Count == 0) return;
        var max = _laps.Max(l => l.LapNumber);
        for (var i = 1; i <= max; i++)
        {
            var item = new LapItem { LapNumber = i };
            _vm.Laps.Add(item);
            DecisionLapCombo.Items.Add(item);
        }
        var mid = Math.Max(0, max / 2 - 1);
        DecisionLapCombo.SelectedIndex = Math.Min(mid, DecisionLapCombo.Items.Count - 1);
    }

    private void PopulateDefaultPredictParams()
    {
        PitLossBox.Text = _vm.PitLaneLoss.ToString("F1", CultureInfo.InvariantCulture);
        WarmUpBox.Text = _vm.WarmUpPenalty.ToString("F1", CultureInfo.InvariantCulture);
        TargetResponseBox.Text = _vm.TargetResponseLaps.ToString(CultureInfo.InvariantCulture);
        MarginalBox.Text = _vm.MarginalThreshold.ToString("F2", CultureInfo.InvariantCulture);
        TrafficPenaltyBox.Text = _vm.TrafficPenalty.ToString("F1", CultureInfo.InvariantCulture);
        InitialGapBox.Text = _vm.InitialGap.ToString("F3", CultureInfo.InvariantCulture);
        AttackerReplAgeBox.Text = "0";
        TargetReplAgeBox.Text = "0";
    }

    // ── Driver/lap selection ─────────────────────────────────────────────────

    private void AttackerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AttackerCombo.SelectedItem is DriverItem item)
        {
            _vm.Attacker = item;
            RefreshDriverState(item, isAttacker: true);
        }
    }

    private void TargetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetCombo.SelectedItem is DriverItem item)
        {
            _vm.Target = item;
            RefreshDriverState(item, isAttacker: false);
        }
    }

    private void DecisionLapCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DecisionLapCombo.SelectedItem is LapItem item)
            _vm.DecisionLap = item;

        if (_vm.Attacker is not null) RefreshDriverState(_vm.Attacker, isAttacker: true);
        if (_vm.Target is not null) RefreshDriverState(_vm.Target, isAttacker: false);
    }

    private void RefreshDriverState(DriverItem driver, bool isAttacker)
    {
        var lap = _vm.DecisionLap?.LapNumber ?? 1;

        var cleanLaps = GetCleanLapsForDriver(driver.DriverNumber);
        double? derivedPace = cleanLaps.Count > 0 ? cleanLaps.Average(l => l.LapDuration!.Value) : null;

        var stint = _stints
            .Where(s => s.DriverNumber == driver.DriverNumber
                     && s.LapStart <= lap
                     && (s.LapEnd == null || s.LapEnd >= lap))
            .OrderByDescending(s => s.StintNumber)
            .FirstOrDefault();

        var compound = stint?.Compound ?? "UNKNOWN";
        var tyreAge = stint is null ? 0 : (lap - stint.LapStart) + stint.TyreAgeAtStart;

        if (isAttacker)
        {
            if (derivedPace.HasValue)
            {
                AttackerPaceBox.Text = derivedPace.Value.ToString("F3", CultureInfo.InvariantCulture);
                AttackerPaceSource.Text = $"(derived, {cleanLaps.Count} laps)";
            }
            else
            {
                AttackerPaceSource.Text = "(no clean laps — enter manually)";
            }
            AttackerCurrentCompoundBox.Text = compound;
            AttackerTyreAgeBox.Text = tyreAge.ToString(CultureInfo.InvariantCulture);
            _vm.AttackerCurrentCompound = compound;
            _vm.AttackerCurrentTyreAge = tyreAge;
        }
        else
        {
            if (derivedPace.HasValue)
            {
                TargetPaceBox.Text = derivedPace.Value.ToString("F3", CultureInfo.InvariantCulture);
                TargetPaceSource.Text = $"(derived, {cleanLaps.Count} laps)";
            }
            else
            {
                TargetPaceSource.Text = "(no clean laps — enter manually)";
            }
            TargetCurrentCompoundBox.Text = compound;
            TargetTyreAgeBox.Text = tyreAge.ToString(CultureInfo.InvariantCulture);
            _vm.TargetCurrentCompound = compound;
            _vm.TargetCurrentTyreAge = tyreAge;
        }

        TryDeriveInitialGap();
    }

    private List<EventLap> GetCleanLapsForDriver(int driverNumber)
    {
        var windows = BuildSafetyCarWindows();
        var pitInLaps = GetDerivedPitInLapNumbers(driverNumber);

        return _laps
            .Where(l => l.DriverNumber == driverNumber
                     && l.LapDuration.HasValue
                     && l.DateStart.HasValue
                     && !l.IsPitOutLap
                     && !pitInLaps.Contains(l.LapNumber)
                     && !IsInAnySafetyCarWindow(l, windows))
            .ToList();
    }

    private void TryDeriveInitialGap()
    {
        var attacker = _vm.Attacker;
        var target = _vm.Target;
        var decisionLap = _vm.DecisionLap?.LapNumber;
        if (attacker is null || target is null || decisionLap is null) return;

        // G0 is the gap at Sector Line Two on the decision lap.
        var attackerLap = _laps.FirstOrDefault(l =>
            l.DriverNumber == attacker.DriverNumber
            && l.LapNumber == decisionLap.Value
            && l.DateStart.HasValue
            && l.LapTimeAtSectorTwoLine.HasValue);

        var targetLap = _laps.FirstOrDefault(l =>
            l.DriverNumber == target.DriverNumber
            && l.LapNumber == decisionLap.Value
            && l.DateStart.HasValue
            && l.LapTimeAtSectorTwoLine.HasValue);

        if (attackerLap is null || targetLap is null) return;

        var attackerSectorTwoLine = ToUtc(attackerLap.DateStart!.Value)
            .AddSeconds(attackerLap.LapTimeAtSectorTwoLine!.Value);
        var targetSectorTwoLine = ToUtc(targetLap.DateStart!.Value)
            .AddSeconds(targetLap.LapTimeAtSectorTwoLine!.Value);

        // Positive = attacker is behind (standard sign convention)
        _vm.InitialGap = Math.Round((attackerSectorTwoLine - targetSectorTwoLine).TotalSeconds, 3);
        InitialGapBox.Text = _vm.InitialGap.ToString("F3", CultureInfo.InvariantCulture);
    }

    // ── Predict tab text-box / combo handlers ────────────────────────────────

    private void AttackerPaceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDouble(AttackerPaceBox.Text, out var v)) _vm.AttackerReferencePace = v;
    }

    private void TargetPaceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDouble(TargetPaceBox.Text, out var v)) _vm.TargetReferencePace = v;
    }

    private void InitialGapBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDouble(InitialGapBox.Text, out var v)) _vm.InitialGap = v;
    }

    private void AttackerReplCompoundCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.AttackerReplCompound = GetComboText(AttackerReplCompoundCombo);
    }

    private void TargetReplCompoundCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.TargetReplCompound = GetComboText(TargetReplCompoundCombo);
    }

    private void AttackerReplAgeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseInt(AttackerReplAgeBox.Text, out var v)) _vm.AttackerReplAge = v;
    }

    private void TargetReplAgeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseInt(TargetReplAgeBox.Text, out var v)) _vm.TargetReplAge = v;
    }

    private void PitLossBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDouble(PitLossBox.Text, out var v)) _vm.PitLaneLoss = v;
    }

    private void WarmUpBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDouble(WarmUpBox.Text, out var v)) _vm.WarmUpPenalty = v;
    }

    private void TargetResponseBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseInt(TargetResponseBox.Text, out var v)) _vm.TargetResponseLaps = v;
    }

    private void MarginalBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDouble(MarginalBox.Text, out var v)) _vm.MarginalThreshold = v;
    }

    private void TrafficPenaltyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDouble(TrafficPenaltyBox.Text, out var v)) _vm.TrafficPenalty = v;
    }

    private void TrafficCheck_Changed(object sender, RoutedEventArgs e)
    {
        _vm.ApplyAttackerTraffic = AttackerTrafficCheck.IsChecked == true;
        _vm.ApplyTargetTraffic = TargetTrafficCheck.IsChecked == true;
    }

    // ── Run prediction ───────────────────────────────────────────────────────

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Visibility = Visibility.Collapsed;
        ResultGroup.Visibility = Visibility.Collapsed;

        var error = ValidatePredictInputs();
        if (error is not null)
        {
            ValidationText.Text = error;
            ValidationText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var request = BuildPredictionRequest();
            var predictor = new PitSequencePredictor(new LapTimePredictor());
            var result = predictor.Predict(request);
            _vm.Result = result;
            _lastResult = result;
            PopulateResult(result);
        }
        catch (Exception ex)
        {
            ValidationText.Text = $"Prediction failed: {ex.Message}";
            ValidationText.Visibility = Visibility.Visible;
        }
    }

    private string? ValidatePredictInputs()
    {
        if (_vm.Attacker is null) return "Select an attacker driver.";
        if (_vm.Target is null) return "Select a target driver.";
        if (_vm.Attacker.DriverNumber == _vm.Target.DriverNumber) return "Attacker and target must be different drivers.";
        if (_vm.DecisionLap is null) return "Select a decision lap.";
        if (_vm.AttackerReferencePace <= 0) return "Attacker reference pace must be positive.";
        if (_vm.TargetReferencePace <= 0) return "Target reference pace must be positive.";
        if (_vm.PitLaneLoss < 0) return "Pit lane loss cannot be negative.";
        if (_vm.TargetResponseLaps < 1) return "Target response laps must be at least 1.";
        if (_vm.AttackerCurrentTyreAge < 0 || _vm.TargetCurrentTyreAge < 0) return "Tyre age cannot be negative.";
        if (_vm.AttackerReplAge < 0 || _vm.TargetReplAge < 0) return "Replacement tyre age cannot be negative.";
        return null;
    }

    private PredictionRequest BuildPredictionRequest()
    {
        var attacker = new DriverPredictionState(
            DriverCode: _vm.Attacker!.Code,
            DisplayName: _vm.Attacker.DisplayName,
            Position: 2,
            ReferencePaceSeconds: _vm.AttackerReferencePace,
            CurrentCompound: ParseCompound(_vm.AttackerCurrentCompound),
            CurrentTyreAgeLaps: _vm.AttackerCurrentTyreAge);

        var target = new DriverPredictionState(
            DriverCode: _vm.Target!.Code,
            DisplayName: _vm.Target.DisplayName,
            Position: 1,
            ReferencePaceSeconds: _vm.TargetReferencePace,
            CurrentCompound: ParseCompound(_vm.TargetCurrentCompound),
            CurrentTyreAgeLaps: _vm.TargetCurrentTyreAge);

        return new PredictionRequest(
            EventName: _eventName,
            DecisionLap: _vm.DecisionLap!.LapNumber,
            InitialAttackerGapToTargetSeconds: _vm.InitialGap,
            Attacker: attacker,
            Target: target,
            AttackerReplacementTyre: new TyreSetSpecification(ParseCompound(_vm.AttackerReplCompound), _vm.AttackerReplAge),
            TargetReplacementTyre: new TyreSetSpecification(ParseCompound(_vm.TargetReplCompound), _vm.TargetReplAge),
            TargetResponseLaps: _vm.TargetResponseLaps,
            ModelParameters: BuildModelParameters());
    }

    private LapModelParameters BuildModelParameters()
    {
        var offsets = new Dictionary<TyreCompound, double>
        {
            [TyreCompound.Soft] = 0.0,
            [TyreCompound.Medium] = 0.0,
            [TyreCompound.Hard] = 0.0
        };
        var degRates = new Dictionary<TyreCompound, double>
        {
            [TyreCompound.Soft] = 0.08,
            [TyreCompound.Medium] = 0.05,
            [TyreCompound.Hard] = 0.03
        };

        // Override from the Parameters tab (live values in TyreRows)
        foreach (var row in TyreRows)
        {
            var c = ParseCompound(row.Compound);
            offsets[c] = row.PaceOffset;
            degRates[c] = row.DegradationRate;
        }

        return new LapModelParameters(
            CompoundOffsetsSeconds: offsets,
            DegradationRatesSecondsPerLap: degRates,
            WarmUp: new WarmUpModelParameters(_vm.WarmUpPenalty),
            PitLaneLossSeconds: _vm.PitLaneLoss,
            MarginalThresholdSeconds: _vm.MarginalThreshold,
            Traffic: new TrafficModelParameters(
                ApplyToAttacker: _vm.ApplyAttackerTraffic,
                ApplyToTarget: _vm.ApplyTargetTraffic,
                PenaltySeconds: _vm.TrafficPenalty));
    }

    // ── Result display ────────────────────────────────────────────────────────

    private void PopulateResult(PredictionResult result)
    {
        var (bg, text) = result.Classification switch
        {
            UndercutClassification.PredictedAhead    => (Colors.Green,          "UNDERCUT PREDICTED SUCCESSFUL"),
            UndercutClassification.PredictedMarginal => (Colors.DarkGoldenrod,  "UNDERCUT MARGINAL"),
            _                                        => (Colors.Crimson,         "UNDERCUT NOT PREDICTED")
        };
        ClassificationBadge.Background = new SolidColorBrush(bg);
        ClassificationText.Text = text;

        // Show G0 alongside the gap cards so the arithmetic is traceable
        var g0Text = $"G₀ = {result.InitialGapSeconds:+0.000;-0.000;0.000} s  (gap at start of prediction window, positive = attacker behind)";
        G0Label.Text = g0Text;

        GapN1Label.Text   = FormatGap(result.GapAtN1Seconds);
        DeltaN1Label.Text = $"ΔG = {result.DeltaGAtN1Seconds:+0.000;-0.000;0.000} s";
        GapN2Label.Text   = FormatGap(result.GapAtN2Seconds);
        DeltaN2Label.Text = $"ΔG = {result.DeltaGAtN2Seconds:+0.000;-0.000;0.000} s";
        GapN3Label.Text   = FormatGap(result.GapAtN3Seconds);
        DeltaN3Label.Text = $"ΔG = {result.DeltaGAtN3Seconds:+0.000;-0.000;0.000} s";

        // Build lap table — interleave attacker and target, one row each per lap number.
        // The Gap column is shown on the target row (both drivers have finished that lap).
        var targetByLap = result.TargetLaps.ToDictionary(l => l.LapNumber, l => l.CumulativePredictionTimeSeconds);
        var attackerByLap = result.AttackerLaps.ToDictionary(l => l.LapNumber, l => l.CumulativePredictionTimeSeconds);

        var rows = new List<LapRow>();
        var allLaps = result.AttackerLaps.Select(l => (Lap: l, IsAttacker: true))
            .Concat(result.TargetLaps.Select(l => (Lap: l, IsAttacker: false)))
            .OrderBy(x => x.Lap.LapNumber)
            .ThenBy(x => x.IsAttacker ? 0 : 1);

        foreach (var (lap, isAttacker) in allLaps)
        {
            var b = lap.Breakdown;

            // Gap = G0 + attacker_elapsed − target_elapsed, shown on the target row
            string gapText = string.Empty;
            if (!isAttacker && attackerByLap.TryGetValue(lap.LapNumber, out var attElapsed))
            {
                var gap = result.InitialGapSeconds + attElapsed - lap.CumulativePredictionTimeSeconds;
                gapText = FormatGap(gap);
            }

            rows.Add(new LapRow
            {
                DriverLabel    = isAttacker ? (_vm.Attacker?.Code ?? "ATT") : (_vm.Target?.Code ?? "TAR"),
                LapNumber      = lap.LapNumber,
                LapType        = lap.IsPitLap ? "Pit" : lap.IsOutLap ? "Out" : "Normal",
                Compound       = lap.Compound.ToString(),
                TyreAge        = lap.TyreAgeAtStart,
                Base           = b.ReferencePaceSeconds.ToString("F3", CultureInfo.InvariantCulture),
                CompoundOffset = FormatTerm(b.CompoundOffsetSeconds),
                Degradation    = FormatTerm(b.DegradationSeconds),
                WarmUp         = FormatTerm(b.WarmUpSeconds),
                Traffic        = FormatTerm(b.TrafficSeconds),
                PitLoss        = FormatTerm(b.PitLossSeconds),
                Total          = b.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture),
                Cumulative     = lap.CumulativePredictionTimeSeconds.ToString("F3", CultureInfo.InvariantCulture),
                Gap            = gapText
            });
        }

        LapGrid.ItemsSource = rows;

        if (result.Warnings.Count > 0)
        {
            WarningList.ItemsSource = result.Warnings;
            WarningPanel.Visibility = Visibility.Visible;
        }
        else
        {
            WarningPanel.Visibility = Visibility.Collapsed;
        }

        ExportCsvButton.IsEnabled = true;
        ResultGroup.Visibility = Visibility.Visible;
    }

    private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export prediction to CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"undercut_prediction_{_eventName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            ExportCsv(dlg.FileName, _lastResult);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportCsv(string path, PredictionResult result)
    {
        var attCode = _vm.Attacker?.Code ?? "ATT";
        var tarCode = _vm.Target?.Code ?? "TAR";
        var inv = CultureInfo.InvariantCulture;

        using var writer = new System.IO.StreamWriter(path, append: false, encoding: System.Text.Encoding.UTF8);

        // ── Scenario header ────────────────────────────────────────────────────
        writer.WriteLine($"# Undercut Prediction Export");
        writer.WriteLine($"# Event,{_eventName}");
        writer.WriteLine($"# Decision lap,{_vm.DecisionLap?.LapNumber}");
        writer.WriteLine($"# Attacker,{attCode}");
        writer.WriteLine($"# Target,{tarCode}");
        writer.WriteLine($"# G0 (s),{result.InitialGapSeconds.ToString("F3", inv)}");
        writer.WriteLine($"# Pit loss (s),{_vm.PitLaneLoss.ToString("F1", inv)}");
        writer.WriteLine($"# Warm-up penalty (s),{_vm.WarmUpPenalty.ToString("F1", inv)}");
        writer.WriteLine($"# Target response (laps),{_vm.TargetResponseLaps}");
        writer.WriteLine($"# Marginal threshold (s),{_vm.MarginalThreshold.ToString("F2", inv)}");
        writer.WriteLine($"# Traffic penalty (s),{_vm.TrafficPenalty.ToString("F1", inv)}");
        writer.WriteLine($"# Apply attacker traffic,{_vm.ApplyAttackerTraffic}");
        writer.WriteLine($"# Apply target traffic,{_vm.ApplyTargetTraffic}");
        writer.WriteLine();

        // ── Summary ────────────────────────────────────────────────────────────
        writer.WriteLine($"# Classification,{result.Classification}");
        writer.WriteLine($"# GapAtN1 (s),{result.GapAtN1Seconds.ToString("F3", inv)}");
        writer.WriteLine($"# GapAtN2 (s),{result.GapAtN2Seconds.ToString("F3", inv)}");
        writer.WriteLine($"# GapAtN3 (s),{result.GapAtN3Seconds.ToString("F3", inv)}");
        writer.WriteLine($"# DeltaGAtN1 (s),{result.DeltaGAtN1Seconds.ToString("F3", inv)}");
        writer.WriteLine($"# DeltaGAtN2 (s),{result.DeltaGAtN2Seconds.ToString("F3", inv)}");
        writer.WriteLine($"# DeltaGAtN3 (s),{result.DeltaGAtN3Seconds.ToString("F3", inv)}");
        writer.WriteLine();

        // ── Lap breakdown ──────────────────────────────────────────────────────
        writer.WriteLine("Driver,Lap,Type,Compound,TyreAge,Base(s),CompoundOffset(s),Degradation(s),WarmUp(s),Traffic(s),PitLoss(s),Total(s),Cumulative(s),Gap(s)");

        var attackerByLap = result.AttackerLaps.ToDictionary(l => l.LapNumber, l => l.CumulativePredictionTimeSeconds);

        var allLaps = result.AttackerLaps.Select(l => (Lap: l, IsAttacker: true))
            .Concat(result.TargetLaps.Select(l => (Lap: l, IsAttacker: false)))
            .OrderBy(x => x.Lap.LapNumber)
            .ThenBy(x => x.IsAttacker ? 0 : 1);

        foreach (var (lap, isAttacker) in allLaps)
        {
            var b = lap.Breakdown;
            var lapType = lap.IsPitLap ? "Pit" : lap.IsOutLap ? "Out" : "Normal";
            var driverLabel = isAttacker ? attCode : tarCode;

            string gapText = string.Empty;
            if (!isAttacker && attackerByLap.TryGetValue(lap.LapNumber, out var attElapsed))
            {
                var gap = result.InitialGapSeconds + attElapsed - lap.CumulativePredictionTimeSeconds;
                gapText = gap.ToString("F3", inv);
            }

            writer.WriteLine(string.Join(",",
                driverLabel,
                lap.LapNumber,
                lapType,
                lap.Compound,
                lap.TyreAgeAtStart,
                b.ReferencePaceSeconds.ToString("F3", inv),
                b.CompoundOffsetSeconds.ToString("F3", inv),
                b.DegradationSeconds.ToString("F3", inv),
                b.WarmUpSeconds.ToString("F3", inv),
                b.TrafficSeconds.ToString("F3", inv),
                b.PitLossSeconds.ToString("F3", inv),
                b.TotalSeconds.ToString("F3", inv),
                lap.CumulativePredictionTimeSeconds.ToString("F3", inv),
                gapText));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SCAN TAB
    // ══════════════════════════════════════════════════════════════════════════

    private void PopulateScanTab()
    {
        var driversByNumber = _drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var ordered = _laps
            .GroupBy(l => l.DriverNumber)
            .Where(g => driversByNumber.ContainsKey(g.Key))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var d = driversByNumber[g.Key];
                var code = string.IsNullOrWhiteSpace(d.Code) ? g.Key.ToString(CultureInfo.InvariantCulture) : d.Code;
                var display = string.IsNullOrWhiteSpace(d.BroadcastName) ? code : $"{code} – {d.BroadcastName}";
                return new DriverItem { DriverNumber = g.Key, Code = code, DisplayName = display };
            });

        foreach (var item in ordered)
            ScanDriverCombo.Items.Add(item);

        if (ScanDriverCombo.Items.Count > 0)
            ScanDriverCombo.SelectedIndex = 0;
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScanDriverCombo.SelectedItem is not DriverItem driver) return;
        await RunScanAndDisplayAsync([driver.DriverNumber]);
    }

    private async void ScanAllButton_Click(object sender, RoutedEventArgs e)
    {
        var allNumbers = _laps.Select(l => l.DriverNumber).Distinct().ToList();
        await RunScanAndDisplayAsync(allNumbers);
    }

    private async Task RunScanAndDisplayAsync(IReadOnlyList<int> driverNumbers)
    {
        ScanButton.IsEnabled = false;
        ScanAllButton.IsEnabled = false;
        ScanExportCsvButton.IsEnabled = false;
        ScanProgressBar.Visibility = Visibility.Visible;
        ScanStatusText.Text = string.Empty;
        ScanRowCountText.Text = "Scanning…";

        // Snapshot UI-thread state before entering background task
        var minAge = TryParseInt(ScanMinTyreAgeBox.Text, out var ma) && ma >= 0 ? ma : 5;
        var modelParams    = BuildModelParameters();
        var scWindows      = BuildSafetyCarWindows();

        List<ScanResultRow> rows;
        try
        {
            rows = await Task.Run(() => RunScan(driverNumbers, minAge, modelParams, scWindows));
        }
        catch (Exception ex)
        {
            ScanRowCountText.Text = $"Scan failed: {ex.Message}";
            return;
        }
        finally
        {
            ScanProgressBar.Visibility = Visibility.Collapsed;
            ScanButton.IsEnabled = true;
            ScanAllButton.IsEnabled = true;
        }

        DisplayScanResults(rows);
    }

    private void DisplayScanResults(List<ScanResultRow> rows)
    {
        ScanGrid.ItemsSource = rows;
        var opportunityCount = rows.Count(r => r.IsOpportunity);
        ScanRowCountText.Text = $"{rows.Count} decision points scanned — {opportunityCount} opportunit{(opportunityCount == 1 ? "y" : "ies")} found";
        ScanExportCsvButton.IsEnabled = rows.Count > 0;
    }

    /// <summary>
    /// Core scan logic. For each driver number and each eligible lap, derives the
    /// driver directly ahead on track, computes G0 from Sector Line Two timing on
    /// the decision lap, then runs the prediction engine with r=1 using the current
    /// model parameters. Safe to call from a background thread — no UI access.
    /// </summary>
    private List<ScanResultRow> RunScan(
        IReadOnlyList<int> driverNumbers,
        int minAge,
        LapModelParameters modelParams,
        List<TimeWindow> safetyCarWindows)
    {
        var referencePaceByDriver = DeriveReferencePacePerDriver(safetyCarWindows);

        // Pre-index laps for fast lookup: driverNumber → (lapNumber → lap)
        var lapIndex = _laps
            .GroupBy(l => l.DriverNumber)
            .ToDictionary(g => g.Key,
                g => g.Where(l => l.DateStart.HasValue && l.LapDuration.HasValue)
                      .ToDictionary(l => l.LapNumber));

        // Pre-compute on-track finishing order per lap number
        var orderPerLap = BuildOnTrackOrderPerLap(lapIndex);

        var driversByNumber = _drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var results = new List<ScanResultRow>();
        var predictor = new PitSequencePredictor(new LapTimePredictor());

        foreach (var attackerNumber in driverNumbers)
        {
            if (!lapIndex.TryGetValue(attackerNumber, out var attackerLapMap)) continue;
            var attackerCode = driversByNumber.TryGetValue(attackerNumber, out var ad)
                ? (string.IsNullOrWhiteSpace(ad.Code) ? attackerNumber.ToString() : ad.Code)
                : attackerNumber.ToString();

            var attackerRefPace = referencePaceByDriver.GetValueOrDefault(attackerNumber, 0.0);
            if (attackerRefPace <= 0) continue; // no valid laps to derive pace

            foreach (var lapNumber in attackerLapMap.Keys.Order())
            {
                var attackerLap = attackerLapMap[lapNumber];

                // Eligibility: must have complete timing and not a pit-out lap
                if (!attackerLap.DateStart.HasValue || !attackerLap.LapDuration.HasValue) continue;
                if (attackerLap.IsPitOutLap) continue;

                // Tyre age eligibility
                var (attackerCompound, attackerTyreAge) = GetTyreStateAtLap(attackerNumber, lapNumber);
                if (attackerTyreAge < minAge) continue;

                // Who is directly ahead on track at the end of this lap?
                if (!orderPerLap.TryGetValue(lapNumber, out var order)) continue;
                var attackerPos = order.IndexOf(attackerNumber);
                if (attackerPos <= 0) continue; // nobody ahead (P1) or not found

                var targetNumber = order[attackerPos - 1];
                if (!lapIndex.TryGetValue(targetNumber, out var targetLapMap)) continue;

                var targetCode = driversByNumber.TryGetValue(targetNumber, out var td)
                    ? (string.IsNullOrWhiteSpace(td.Code) ? targetNumber.ToString() : td.Code)
                    : targetNumber.ToString();

                var targetRefPace = referencePaceByDriver.GetValueOrDefault(targetNumber, 0.0);
                if (targetRefPace <= 0) continue;

                // G0 is the gap at Sector Line Two on the decision lap.
                if (!attackerLapMap.TryGetValue(lapNumber, out var attackerDecisionLap)) continue;
                if (!targetLapMap.TryGetValue(lapNumber, out var targetDecisionLap)) continue;
                if (!attackerDecisionLap.DateStart.HasValue || !attackerDecisionLap.LapTimeAtSectorTwoLine.HasValue) continue;
                if (!targetDecisionLap.DateStart.HasValue || !targetDecisionLap.LapTimeAtSectorTwoLine.HasValue) continue;

                var attackerSectorTwoLine = ToUtc(attackerDecisionLap.DateStart.Value)
                    .AddSeconds(attackerDecisionLap.LapTimeAtSectorTwoLine.Value);
                var targetSectorTwoLine = ToUtc(targetDecisionLap.DateStart.Value)
                    .AddSeconds(targetDecisionLap.LapTimeAtSectorTwoLine.Value);
                var g0 = (attackerSectorTwoLine - targetSectorTwoLine).TotalSeconds;

                // Must be behind to have an undercut opportunity
                if (g0 <= 0) continue;

                var (targetCompound, targetTyreAge) = GetTyreStateAtLap(targetNumber, lapNumber);
                var attackerReplCompound = ParseCompound(_vm.AttackerReplCompound.Length > 0 ? _vm.AttackerReplCompound : "SOFT");
                var targetReplCompound   = ParseCompound(_vm.TargetReplCompound.Length > 0   ? _vm.TargetReplCompound   : "SOFT");

                var request = new PredictionRequest(
                    EventName: _eventName,
                    DecisionLap: lapNumber,
                    InitialAttackerGapToTargetSeconds: g0,
                    Attacker: new DriverPredictionState(
                        DriverCode: attackerCode,
                        DisplayName: attackerCode,
                        Position: attackerPos + 1,
                        ReferencePaceSeconds: attackerRefPace,
                        CurrentCompound: ParseCompound(attackerCompound),
                        CurrentTyreAgeLaps: attackerTyreAge),
                    Target: new DriverPredictionState(
                        DriverCode: targetCode,
                        DisplayName: targetCode,
                        Position: attackerPos,
                        ReferencePaceSeconds: targetRefPace,
                        CurrentCompound: ParseCompound(targetCompound),
                        CurrentTyreAgeLaps: targetTyreAge),
                    AttackerReplacementTyre: new TyreSetSpecification(attackerReplCompound, _vm.AttackerReplAge),
                    TargetReplacementTyre:   new TyreSetSpecification(targetReplCompound,   _vm.TargetReplAge),
                    TargetResponseLaps: 1,
                    ModelParameters: modelParams);

                PredictionResult prediction;
                try
                {
                    prediction = predictor.Predict(request);
                }
                catch (Exception ex)
                {
                    // Record a stub row so the failure is visible in the table
                    results.Add(new ScanResultRow
                    {
                        Attacker         = attackerCode,
                        Target           = targetCode,
                        DecisionLap      = lapNumber,
                        AttackerCompound = attackerCompound,
                        AttackerTyreAge  = attackerTyreAge,
                        TargetCompound   = targetCompound,
                        TargetTyreAge    = targetTyreAge,
                        G0               = $"{g0:+0.000;-0.000;0.000}",
                        GapAtN1          = "—",
                        GapAtN2          = "—",
                        GapAtN3          = "—",
                        DeltaGN1         = "—",
                        Result           = $"Error: {ex.Message}",
                        IsOpportunity    = false
                    });
                    continue;
                }

                var isOpportunity = prediction.Classification is
                    UndercutClassification.PredictedAhead or
                    UndercutClassification.PredictedMarginal;

                results.Add(new ScanResultRow
                {
                    Attacker          = attackerCode,
                    Target            = targetCode,
                    DecisionLap       = lapNumber,
                    AttackerCompound  = attackerCompound,
                    AttackerTyreAge   = attackerTyreAge,
                    TargetCompound    = targetCompound,
                    TargetTyreAge     = targetTyreAge,
                    G0                = $"{g0:+0.000;-0.000;0.000}",
                    GapAtN1           = FormatGap(prediction.GapAtN1Seconds),
                    GapAtN2           = FormatGap(prediction.GapAtN2Seconds),
                    GapAtN3           = FormatGap(prediction.GapAtN3Seconds),
                    DeltaGN1          = $"{prediction.DeltaGAtN1Seconds:+0.000;-0.000;0.000}",
                    Result            = prediction.Classification switch
                    {
                        UndercutClassification.PredictedAhead    => "Ahead",
                        UndercutClassification.PredictedMarginal => "Marginal",
                        _                                        => "Behind"
                    },
                    IsOpportunity     = isOpportunity
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Builds a per-lap list of driver numbers in on-track finishing order (ascending finish time).
    /// Only drivers with complete timing on that lap are included.
    /// </summary>
    private static Dictionary<int, List<int>> BuildOnTrackOrderPerLap(
        Dictionary<int, Dictionary<int, EventLap>> lapIndex)
    {
        // Collect all lap numbers across all drivers
        var allLapNumbers = lapIndex.Values
            .SelectMany(d => d.Keys)
            .Distinct()
            .Order();

        var result = new Dictionary<int, List<int>>();

        foreach (var lapNum in allLapNumbers)
        {
            var finishes = new List<(int driverNumber, DateTime finish)>();
            foreach (var (driverNum, lapMap) in lapIndex)
            {
                if (!lapMap.TryGetValue(lapNum, out var lap)) continue;
                if (!lap.DateStart.HasValue || !lap.LapDuration.HasValue) continue;
                var finish = ToUtc(lap.DateStart.Value).AddSeconds(lap.LapDuration.Value);
                finishes.Add((driverNum, finish));
            }
            result[lapNum] = finishes
                .OrderBy(f => f.finish)
                .Select(f => f.driverNumber)
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// Returns the compound and tyre age (laps on this tyre at the start of <paramref name="lapNumber"/>)
    /// from stint data. Falls back to "UNKNOWN" / 0 if no stint found.
    /// </summary>
    private (string compound, int age) GetTyreStateAtLap(int driverNumber, int lapNumber)
    {
        var stint = _stints
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

        // Fallback when stint data is unavailable: estimate tyre age from the lap sequence.
        // Count laps on the current set by finding the most recent pit-out lap at or before lapNumber.
        // DefaultIfEmpty(1) treats the first lap as the start of the opening stint.
        var lastPitOutLap = _laps
            .Where(l => l.DriverNumber == driverNumber
                     && l.LapNumber    <= lapNumber
                     && l.IsPitOutLap)
            .Select(l => l.LapNumber)
            .DefaultIfEmpty(1)
            .Max();

        return ("UNKNOWN", lapNumber - lastPitOutLap);
    }

    private HashSet<int> GetDerivedPitInLapNumbers(int driverNumber)
    {
        var pitOutLaps = _laps
            .Where(l => l.DriverNumber == driverNumber && l.IsPitOutLap)
            .Select(l => l.LapNumber)
            .ToHashSet();

        return pitOutLaps
            .Select(lap => lap - 1)
            .Where(lap => lap >= 1 && !pitOutLaps.Contains(lap))
            .ToHashSet();
    }

    /// <summary>
    /// Computes the average clean-lap time for every driver, excluding pit-out laps,
    /// derived pit-in laps (lap immediately before pit-out), and SC/VSC laps.
    /// </summary>
    private Dictionary<int, double> DeriveReferencePacePerDriver(List<TimeWindow> safetyCarWindows)
    {
        return _laps
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

    private void ScanExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScanGrid.ItemsSource is not List<ScanResultRow> rows || rows.Count == 0) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export scan results to CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"undercut_scan_{_eventName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var inv = CultureInfo.InvariantCulture;
            using var writer = new System.IO.StreamWriter(dlg.FileName, append: false, encoding: System.Text.Encoding.UTF8);
            writer.WriteLine("# Undercut Scan Export");
            writer.WriteLine($"# Event,{_eventName}");
            writer.WriteLine($"# Pit loss (s),{_vm.PitLaneLoss.ToString("F1", inv)}");
            writer.WriteLine($"# Warm-up penalty (s),{_vm.WarmUpPenalty.ToString("F1", inv)}");
            writer.WriteLine($"# Target response (laps),1");
            writer.WriteLine();
            writer.WriteLine("Lap,Attacker,Target,Att.Compound,Att.TyreAge,Tar.Compound,Tar.TyreAge,G0(s),GapN1(s),GapN2(s),GapN3(s),DeltaGN1(s),Result");

            foreach (var row in rows)
            {
                writer.WriteLine(string.Join(",",
                    row.DecisionLap,
                    row.Attacker,
                    row.Target,
                    row.AttackerCompound,
                    row.AttackerTyreAge,
                    row.TargetCompound,
                    row.TargetTyreAge,
                    row.G0,
                    row.GapAtN1,
                    row.GapAtN2,
                    row.GapAtN3,
                    row.DeltaGN1,
                    row.Result));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    // ══════════════════════════════════════════════════════════════════════════
    // SHARED HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static string FormatGap(double gap)
    {
        var sign = gap < 0 ? "▲ " : gap > 0 ? "▼ " : "= ";
        return $"{sign}{gap:+0.000;-0.000;0.000} s";
    }

    private static string FormatTerm(double v) =>
        v == 0.0 ? "0.000" : v.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);

    private static TyreCompound ParseCompound(string s) => (s?.ToUpperInvariant() ?? string.Empty) switch
    {
        "SOFT"         => TyreCompound.Soft,
        "MEDIUM"       => TyreCompound.Medium,
        "HARD"         => TyreCompound.Hard,
        "INTERMEDIATE" => TyreCompound.Intermediate,
        "WET"          => TyreCompound.Wet,
        _              => TyreCompound.Unknown
    };

    private static string GetComboText(ComboBox combo) =>
        combo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? string.Empty : string.Empty;

    private static bool TryParseDouble(string text, out double value) =>
        double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static bool TryParseInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private List<TimeWindow> BuildSafetyCarWindows()
    {
        var windows = new List<TimeWindow>();
        DateTime? vscStart = null;
        DateTime? scStart = null;

        foreach (var m in _raceControlMessages
                     .Where(m => string.Equals(m.Category, "SafetyCar", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(m.Category, "Safety Car", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.Date))
        {
            if (!m.Date.HasValue) continue;
            var ts = m.Date.Value.Kind == DateTimeKind.Utc ? m.Date.Value : DateTime.SpecifyKind(m.Date.Value, DateTimeKind.Utc);
            var msg = (m.Message ?? string.Empty).Trim().ToUpperInvariant();

            if (msg.Contains("VSC DEPLOYED"))          { vscStart = ts; continue; }
            if (msg.Contains("VSC ENDING") && vscStart.HasValue) { windows.Add(new(vscStart.Value, ts)); vscStart = null; continue; }
            if (msg.Contains("SAFETY CAR DEPLOYED"))   { scStart = ts; continue; }
            if ((msg.Contains("SAFETY CAR IN THIS LAP") || msg.Contains("SAFETY CAR WITHDRAWN")) && scStart.HasValue)
            {
                windows.Add(new(scStart.Value, ts)); scStart = null;
            }
        }

        return windows;
    }

    private bool IsInAnySafetyCarWindow(EventLap lap, List<TimeWindow> windows)
    {
        if (!lap.DateStart.HasValue || !lap.LapDuration.HasValue) return false;
        var start = lap.DateStart.Value.Kind == DateTimeKind.Utc ? lap.DateStart.Value : DateTime.SpecifyKind(lap.DateStart.Value, DateTimeKind.Utc);
        var end = start.AddSeconds(lap.LapDuration.Value);
        return windows.Any(w => start < w.EndUtc && end > w.StartUtc);
    }
}
