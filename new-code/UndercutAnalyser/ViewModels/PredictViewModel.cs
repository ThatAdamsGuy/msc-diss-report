using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.ViewModels
{
    /// <summary>
    /// A driver entry in the attacking-driver/target-driver selector ComboBoxes.
    /// </summary>
    public sealed class DriverItem
    {
        public int DriverNumber { get; init; }
        public string Code { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string TeamName { get; init; } = string.Empty;

        /// <summary>
        /// Returns the display label shown in ComboBox selections.
        /// </summary>
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// A lap entry in the decision-lap selector ComboBox.
    /// </summary>
    public sealed class LapItem
    {
        public int LapNumber { get; init; }

        /// <summary>
        /// Returns the display label shown in decision-lap selection controls.
        /// </summary>
        public override string ToString() => $"Lap {LapNumber}";
    }

    /// <summary>
    /// Holds all input state and the most recent result for the Predict window.
    /// The window code-behind derives reference pace and tyre state from loaded
    /// race data and writes them here; the user may then override values before
    /// running the prediction.
    /// </summary>
    public sealed class PredictViewModel : INotifyPropertyChanged
    {
        // ── Scenario inputs ──────────────────────────────────────────────────

        private DriverItem? _attacker;
        private DriverItem? _target;
        private LapItem? _decisionLap;

        public ObservableCollection<DriverItem> Drivers { get; } = new();
        public ObservableCollection<LapItem> Laps { get; } = new();

        public DriverItem? Attacker
        {
            get => _attacker;
            set => SetField(ref _attacker, value);
        }

        public DriverItem? Target
        {
            get => _target;
            set => SetField(ref _target, value);
        }

        public LapItem? DecisionLap
        {
            get => _decisionLap;
            set => SetField(ref _decisionLap, value);
        }

        // ── Attacking driver state at decision lap (Sector Line Two) ─────────

        private double _attackerReferencePace = 90.0;
        private string _attackerCurrentCompound = "SOFT";
        private int _attackerCurrentTyreAge;
        private string _attackerReplCompound = "SOFT";
        private int _attackerReplAge;

        public double AttackerReferencePace
        {
            get => _attackerReferencePace;
            set => SetField(ref _attackerReferencePace, value);
        }

        public string AttackerCurrentCompound
        {
            get => _attackerCurrentCompound;
            set => SetField(ref _attackerCurrentCompound, value);
        }

        public int AttackerCurrentTyreAge
        {
            get => _attackerCurrentTyreAge;
            set => SetField(ref _attackerCurrentTyreAge, value);
        }

        public string AttackerReplCompound
        {
            get => _attackerReplCompound;
            set => SetField(ref _attackerReplCompound, value);
        }

        public int AttackerReplAge
        {
            get => _attackerReplAge;
            set => SetField(ref _attackerReplAge, value);
        }

        // ── Target driver state at decision lap (Sector Line Two) ───────────

        private double _targetReferencePace = 90.0;
        private string _targetCurrentCompound = "SOFT";
        private int _targetCurrentTyreAge;
        private string _targetReplCompound = "SOFT";
        private int _targetReplAge;

        public double TargetReferencePace
        {
            get => _targetReferencePace;
            set => SetField(ref _targetReferencePace, value);
        }

        public string TargetCurrentCompound
        {
            get => _targetCurrentCompound;
            set => SetField(ref _targetCurrentCompound, value);
        }

        public int TargetCurrentTyreAge
        {
            get => _targetCurrentTyreAge;
            set => SetField(ref _targetCurrentTyreAge, value);
        }

        public string TargetReplCompound
        {
            get => _targetReplCompound;
            set => SetField(ref _targetReplCompound, value);
        }

        public int TargetReplAge
        {
            get => _targetReplAge;
            set => SetField(ref _targetReplAge, value);
        }

        // ── Initial gap ───────────────────────────────────────────────────────

        private double _initialGap = 1.5;

        /// <summary>
        /// Gap from attacking driver to target driver at the decision lap (Sector Line Two)
        /// (positive = attacking driver is behind).
        /// </summary>
        public double InitialGap
        {
            get => _initialGap;
            set => SetField(ref _initialGap, value);
        }

        // ── Model parameters ──────────────────────────────────────────────────

        private int _targetResponseLaps = 1;
        private double _pitLaneLoss = 22.0;
        private double _warmUpPenalty = 0.3;
        private double _marginalThreshold = 0.25;
        private bool _applyAttackerTraffic;
        private bool _applyTargetTraffic;
        private double _trafficPenalty = 0.3;

        public int TargetResponseLaps
        {
            get => _targetResponseLaps;
            set => SetField(ref _targetResponseLaps, Math.Max(1, value));
        }

        public double PitLaneLoss
        {
            get => _pitLaneLoss;
            set => SetField(ref _pitLaneLoss, value);
        }

        public double WarmUpPenalty
        {
            get => _warmUpPenalty;
            set => SetField(ref _warmUpPenalty, value);
        }

        public double MarginalThreshold
        {
            get => _marginalThreshold;
            set => SetField(ref _marginalThreshold, Math.Max(0.0, value));
        }

        public bool ApplyAttackerTraffic
        {
            get => _applyAttackerTraffic;
            set => SetField(ref _applyAttackerTraffic, value);
        }

        public bool ApplyTargetTraffic
        {
            get => _applyTargetTraffic;
            set => SetField(ref _applyTargetTraffic, value);
        }

        public double TrafficPenalty
        {
            get => _trafficPenalty;
            set => SetField(ref _trafficPenalty, value);
        }

        // ── Result ────────────────────────────────────────────────────────────

        private PredictionResult? _result;
        private bool _hasResult;
        private string _resultError = string.Empty;

        public PredictionResult? Result
        {
            get => _result;
            set
            {
                SetField(ref _result, value);
                HasResult = value is not null;
            }
        }

        public bool HasResult
        {
            get => _hasResult;
            private set => SetField(ref _hasResult, value);
        }

        public string ResultError
        {
            get => _resultError;
            set => SetField(ref _resultError, value);
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Standard setter helper that updates a backing field and notifies WPF bindings.
        /// </summary>
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
