using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Infrastructure;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.ViewModels
{
    public sealed class EventSelectorViewModel : INotifyPropertyChanged
    {
        private readonly IEventDataProvider _provider;

        public ObservableCollection<EventRace> Events { get; } = new();

        public ICollectionView EventsView { get; }

        public EventSelectorViewModel(IEventDataProvider? provider = null)
        {
            _provider = provider ?? new ErgastApiClient();
            EventsView = CollectionViewSource.GetDefaultView(Events);
            EventsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(EventRace.Year)));
        }

        public async Task LoadAsync(int startYear, int endYear)
        {
            Events.Clear();
            for (int y = endYear; y >= startYear; y--)
            {
                try
                {
                    var races = await _provider.GetRacesBySeasonAsync(y).ConfigureAwait(false);
                    // marshal to UI-safe collection -- caller should call from UI thread, but we'll add items directly
                    foreach (var r in races)
                    {
                        Events.Add(r);
                    }
                }
                catch
                {
                    // ignore single-year failures for now
                }
            }
            OnPropertyChanged(nameof(Events));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
