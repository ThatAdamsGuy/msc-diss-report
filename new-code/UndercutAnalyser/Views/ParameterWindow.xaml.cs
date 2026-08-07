using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser
{
    public partial class ParameterWindow : Window
    {
        public ObservableCollection<TyreParameterRow> TyreRows { get; }

        public double FuelSecondsPer10Kg { get; private set; }

        public double FuelKg { get; private set; }

        public IReadOnlyList<TyreParameterRow> ResultTyreRows => TyreRows.Select(x => x.Clone()).ToList();

        public ParameterWindow(double fuelSecondsPer10Kg, double fuelKg, IEnumerable<TyreParameterRow> tyreRows)
        {
            InitializeComponent();

            TyreRows = new ObservableCollection<TyreParameterRow>(tyreRows.Select(x => x.Clone()));
            FuelSecondsPer10Kg = System.Math.Round(fuelSecondsPer10Kg, 2);
            FuelKg = System.Math.Round(fuelKg, 2);

            DataContext = this;

            FuelSecondsPer10KgBox.Text = FuelSecondsPer10Kg.ToString("F2", CultureInfo.InvariantCulture);
            FuelKgBox.Text = FuelKg.ToString("F2", CultureInfo.InvariantCulture);

            FuelSecondsPer10KgBox.LostFocus += (_, _) => NormalizeFuelText(FuelSecondsPer10KgBox);
            FuelKgBox.LostFocus += (_, _) => NormalizeFuelText(FuelKgBox);

            OkButton.Click += (_, _) => Confirm();
            CancelButton.Click += (_, _) =>
            {
                DialogResult = false;
                Close();
            };
        }

        private void Confirm()
        {
            if (!TryReadFuelValue(FuelSecondsPer10KgBox.Text, out var secondsPer10Kg)
                || !TryReadFuelValue(FuelKgBox.Text, out var fuelKg))
            {
                MessageBox.Show(this, "Fuel fields must be valid numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FuelSecondsPer10Kg = secondsPer10Kg;
            FuelKg = fuelKg;

            DialogResult = true;
            Close();
        }

        private static void NormalizeFuelText(System.Windows.Controls.TextBox box)
        {
            if (TryReadFuelValue(box.Text, out var value))
            {
                box.Text = value.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        private static bool TryReadFuelValue(string? text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                value = System.Math.Round(parsed, 2);
                return true;
            }

            value = 0;
            return false;
        }
    }
}
