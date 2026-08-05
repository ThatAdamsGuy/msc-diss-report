using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace UndercutAnalyser;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new ViewModels.EventSelectorViewModel();
        DataContext = vm;

        Loaded += async (_, _) =>
        {
            // OpenF1 has free historical data from 2023 onwards
            var now = DateTime.UtcNow.Year;
            await vm.LoadAsync(2023, now).ConfigureAwait(false);
        };

        EventButton.Click += (_, _) =>
        {
            var dlg = new EventSelectorWindow(vm);
            var res = dlg.ShowDialog();
            if (res == true && vm.SelectedEvent is not null)
            {
                // Update the display text with selected event
                SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year})";
            }
        };
    }
}