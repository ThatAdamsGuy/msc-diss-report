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
            // default: last 5 years
            var now = DateTime.UtcNow.Year;
            await vm.LoadAsync(now - 4, now).ConfigureAwait(false);
        };
    }
}