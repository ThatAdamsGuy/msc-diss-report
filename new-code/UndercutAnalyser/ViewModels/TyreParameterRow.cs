using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UndercutAnalyser.ViewModels
{
    /// <summary>
    /// Editable parameter row for one compound in the Parameters tab.
    /// </summary>
    public sealed class TyreParameterRow : INotifyPropertyChanged
    {
        private double _paceOffset;
        private double _degradationRate;
        private double _warmUpPenalty;
        private string _minLaps = string.Empty;
        private string _maxLaps = string.Empty;

        /// <summary>
        /// Creates one compound parameter row with editability flags.
        /// </summary>
        public TyreParameterRow(string compound, double paceOffset, double degradationRate, double warmUpPenalty, bool isEditable, bool isDegradationEditable = true)
        {
            Compound = compound;
            IsEditable = isEditable;
            IsDegradationEditable = isDegradationEditable;
            PaceOffset = paceOffset;
            DegradationRate = degradationRate;
            WarmUpPenalty = warmUpPenalty;
        }

        public string Compound { get; }

        /// <summary>Whether the pace offset field is editable. False for the Soft reference compound.</summary>
        public bool IsEditable { get; }

        /// <summary>Whether the degradation rate field is editable. True for all compounds including Soft.</summary>
        public bool IsDegradationEditable { get; }

        public double PaceOffset
        {
            get => _paceOffset;
            set => SetField(ref _paceOffset, Math.Round(value, 2));
        }

        public double DegradationRate
        {
            get => _degradationRate;
            set => SetField(ref _degradationRate, Math.Round(value, 2));
        }

        public double WarmUpPenalty
        {
            get => _warmUpPenalty;
            set => SetField(ref _warmUpPenalty, Math.Round(value, 2));
        }

        public string MinLaps
        {
            get => _minLaps;
            set => SetField(ref _minLaps, value ?? string.Empty);
        }

        public string MaxLaps
        {
            get => _maxLaps;
            set => SetField(ref _maxLaps, value ?? string.Empty);
        }

        /// <summary>
        /// Creates a deep copy so edits can be propagated safely between windows.
        /// </summary>
        public TyreParameterRow Clone()
        {
            return new TyreParameterRow(Compound, PaceOffset, DegradationRate, WarmUpPenalty, IsEditable, IsDegradationEditable)
            {
                MinLaps = MinLaps,
                MaxLaps = MaxLaps
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Standard setter helper that raises PropertyChanged when value changes.
        /// </summary>
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
