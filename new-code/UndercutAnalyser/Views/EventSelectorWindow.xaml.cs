using System.Windows;
using System.Windows.Controls;
using UndercutAnalyser.ViewModels;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser
{
    /// <summary>
    /// Dialog for selecting an event/meeting from the loaded season list.
    /// </summary>
    public partial class EventSelectorWindow : Window
    {
        private readonly EventSelectorViewModel _vm;

        /// <summary>
        /// Creates the selector dialog and wires filter/selection UI events.
        /// </summary>
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
            EventsListView.MouseDoubleClick += (_, __) =>
            {
                if (EventsListView.SelectedItem is not null)
                {
                    DialogResult = true;
                    Close();
                }
            };
            OkButton.Click += (_, __) => { DialogResult = true; Close(); };
            CancelButton.Click += (_, __) => { DialogResult = false; Close(); };

            // Watch for event collection changes to update status
            _vm.Events.CollectionChanged += (_, __) => UpdateStatus();
        }

        /// <summary>
        /// Updates the footer text to show whether events were successfully loaded.
        /// </summary>
        private void UpdateStatus()
        {
            var count = _vm.Events.Count;
            StatusText.Text = count == 0 ? "No events loaded. Check your internet connection." : $"{count} events available";
        }

        /// <summary>
        /// Returns the currently selected event when the dialog closes with OK.
        /// </summary>
        public EventMeeting? SelectedEvent => _vm.SelectedEvent;
    }
}
