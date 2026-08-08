using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UndercutAnalyser.ViewModels
{
    public sealed class TyreParameterRow : INotifyPropertyChanged
    {
        private double _paceOffset;
        private double _degradationRate;
        private string _minLaps = string.Empty;
        private string _maxLaps = string.Empty;

        public TyreParameterRow(string compound, double paceOffset, double degradationRate, bool isEditable, bool isDegradationEditable = true)
        {
            Compound = compound;
            IsEditable = isEditable;
            IsDegradationEditable = isDegradationEditable;
            PaceOffset = paceOffset;
            DegradationRate = degradationRate;
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

        public TyreParameterRow Clone()
        {
            return new TyreParameterRow(Compound, PaceOffset, DegradationRate, IsEditable, IsDegradationEditable)
            {
                MinLaps = MinLaps,
                MaxLaps = MaxLaps
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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
