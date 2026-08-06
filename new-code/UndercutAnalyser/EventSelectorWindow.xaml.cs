using System.Windows;
using System.Windows.Controls;
using UndercutAnalyser.ViewModels;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser
{
    public partial class EventSelectorWindow : Window
    {
        private readonly EventSelectorViewModel _vm;

        public EventSelectorWindow(EventSelectorViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;

            // Update status on load
            UpdateStatus();

            FilterBox.TextChanged += (_, __) =>
            {
                _vm.SetFilter(FilterBox.Text);
                // Force collection view refresh when filter changes
                _vm.EventsView.Refresh();
            };

            EventsListView.SelectionChanged += (_, __) => OkButton.IsEnabled = EventsListView.SelectedItem != null;
            OkButton.Click += (_, __) => { DialogResult = true; Close(); };
            CancelButton.Click += (_, __) => { DialogResult = false; Close(); };

            // Watch for event collection changes to update status
            _vm.Events.CollectionChanged += (_, __) => UpdateStatus();
        }

        private void UpdateStatus()
        {
            var count = _vm.Events.Count;
            StatusText.Text = count == 0 ? "No events loaded. Check your internet connection." : $"{count} events available";
        }

        public EventMeeting? SelectedEvent => _vm.SelectedEvent;
    }
}
