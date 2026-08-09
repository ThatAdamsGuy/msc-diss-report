using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Infrastructure;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.ViewModels
{
    /// <summary>
    /// View-model for event selection: loads meetings, supports filtering, and exposes
    /// current selection for downstream race-data loading.
    /// </summary>
    public sealed class EventSelectorViewModel : INotifyPropertyChanged
    {
        private readonly IEventDataProvider _provider;
        private EventMeeting? _selectedEvent;

        public ObservableCollection<EventMeeting> Events { get; } = new();

        public ICollectionView EventsView { get; }

        /// <summary>
        /// Creates the view-model and initialises grouped event view (grouped by year).
        /// </summary>
        public EventSelectorViewModel(IEventDataProvider? provider = null)
        {
            _provider = provider ?? new OpenF1RaceDataClient();
            EventsView = CollectionViewSource.GetDefaultView(Events);
            EventsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(EventMeeting.Year)));
        }

        /// <summary>
        /// Currently selected event (bindable).
        /// </summary>
        public EventMeeting? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (!Equals(_selectedEvent, value))
                {
                    _selectedEvent = value;
                    OnPropertyChanged(nameof(SelectedEvent));
                }
            }
        }

        /// <summary>
        /// Apply a simple text filter against RaceName and CircuitName. Null or empty clears filter.
        /// </summary>
        public void SetFilter(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                EventsView.Filter = null;
            }
            else
            {
                var lower = text.Trim().ToLowerInvariant();
                EventsView.Filter = o =>
                {
                    if (o is not EventMeeting er) return false;
                    return er.RaceName?.ToLowerInvariant().Contains(lower) == true
                           || er.CircuitName?.ToLowerInvariant().Contains(lower) == true
                           || er.Location?.ToLowerInvariant().Contains(lower) == true;
                };
            }
            OnPropertyChanged(nameof(EventsView));
        }

        /// <summary>
        /// Loads events from the data provider, filters by requested year range, and
        /// populates the observable list on the UI thread.
        /// </summary>
        public async Task LoadAsync(int startYear, int endYear)
        {
            Events.Clear();
            try
            {
                // Fetch all races in a single API call to avoid rate limiting.
                // The API returns all races regardless of year, so we call once.
                var allRaces = await _provider.GetRacesBySeasonAsync(startYear).ConfigureAwait(false);

                // Filter locally for the requested year range
                var filteredRaces = allRaces
                    .Where(r => r.Year >= startYear 
                        && r.Year <= endYear 
                        && r.MeetingName != "Pre-Season Testing" 
                        && r.DateEnd <= DateTime.Now)
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Date)
                    .ToList();

                // Marshal back to UI thread to update ObservableCollection
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var r in filteredRaces)
                    {
                        Events.Add(r);
                    }
                    // Ensure filter is cleared so all events are visible when dialog opens
                    SetFilter(null);
                    OnPropertyChanged(nameof(Events));
                });
            }
            catch (Exception ex)
            {
                UndercutAnalyser.Infrastructure.Logger.Error(ex, "LoadAsync");
                // API call failed; leave Events empty
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises PropertyChanged for WPF bindings.
        /// </summary>
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
