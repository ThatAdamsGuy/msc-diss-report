using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser.Tests;

public sealed class EventSelectorViewModelTests
{
    #region Filtering behavior and selection notifications
    // These tests exist to protect user-facing selector behavior: textual filtering and
    // property change notifications that drive UI selection/binding updates.

    [Fact]
    public void SetFilter_MatchesRaceCircuitAndLocation_CaseInsensitive()
    {
        var vm = new EventSelectorViewModel(new StubEventDataProvider(_ => Task.FromResult<IReadOnlyList<EventMeeting>>([])));

        vm.Events.Add(Meeting("FORMULA 1 BRITISH GRAND PRIX", "Silverstone", "UK", 2025));
        vm.Events.Add(Meeting("FORMULA 1 ITALIAN GRAND PRIX", "Monza", "Italy", 2025));
        vm.Events.Add(Meeting("FORMULA 1 MIAMI GRAND PRIX", "Miami", "USA", 2025));

        vm.SetFilter("monza");
        Assert.Single(vm.EventsView.Cast<EventMeeting>());

        vm.SetFilter("usa");
        Assert.Single(vm.EventsView.Cast<EventMeeting>());

        vm.SetFilter("  BRITISH ");
        Assert.Single(vm.EventsView.Cast<EventMeeting>());

        vm.SetFilter(null);
        Assert.Equal(3, vm.EventsView.Cast<EventMeeting>().Count());
    }

    [Fact]
    public void SetFilter_Whitespace_ClearsFilter()
    {
        var vm = new EventSelectorViewModel(new StubEventDataProvider(_ => Task.FromResult<IReadOnlyList<EventMeeting>>([])));
        vm.Events.Add(Meeting("A", "B", "C", 2025));

        vm.SetFilter("x");
        Assert.Empty(vm.EventsView.Cast<EventMeeting>());

        vm.SetFilter("   ");
        Assert.Single(vm.EventsView.Cast<EventMeeting>());
        Assert.Null(vm.EventsView.Filter);
    }

    [Fact]
    public void SelectedEvent_RaisesPropertyChangedOnlyWhenValueChanges()
    {
        var vm = new EventSelectorViewModel(new StubEventDataProvider(_ => Task.FromResult<IReadOnlyList<EventMeeting>>([])));
        var a = Meeting("A", "TrackA", "LocA", 2025);
        var b = Meeting("B", "TrackB", "LocB", 2025);

        var raised = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EventSelectorViewModel.SelectedEvent))
            {
                raised++;
            }
        };

        vm.SelectedEvent = a;
        vm.SelectedEvent = a;
        vm.SelectedEvent = b;

        Assert.Equal(2, raised);
    }

    #endregion

    #region LoadAsync guard behavior without WPF application bootstrap
    // These tests exist to pin safe failure behavior in headless/unit-test contexts where
    // Application.Current is unavailable and where provider faults must not crash callers.

    [Fact]
    public async Task LoadAsync_ProviderThrows_LeavesEventsEmptyAndDoesNotThrow()
    {
        var provider = new StubEventDataProvider(_ => throw new InvalidOperationException("boom"));
        var vm = new EventSelectorViewModel(provider);

        vm.Events.Add(Meeting("OLD", "Track", "Loc", 2020));

        await vm.LoadAsync(2024, 2025);

        Assert.Empty(vm.Events);
        Assert.Equal([2024], provider.YearsRequested);
    }

    [Fact]
    public async Task LoadAsync_WithoutApplicationCurrent_FailsSafelyAndLeavesEventsEmpty()
    {
        var provider = new StubEventDataProvider(_ => Task.FromResult<IReadOnlyList<EventMeeting>>(
        [
            Meeting("FORMULA 1 AUSTRALIAN GRAND PRIX", "Melbourne", "Australia", 2025)
        ]));

        var vm = new EventSelectorViewModel(provider);

        await vm.LoadAsync(2024, 2025);

        Assert.Empty(vm.Events);
        Assert.Equal([2024], provider.YearsRequested);
    }

    #endregion

    private static EventMeeting Meeting(string officialName, string circuit, string location, int year) =>
        new()
        {
            MeetingOfficialName = officialName,
            CircuitShortName = circuit,
            Location = location,
            Year = year,
            DateStart = DateTime.Now.AddDays(-2),
            DateEnd = DateTime.Now.AddDays(-1),
            MeetingName = "Race Weekend"
        };

    private sealed class StubEventDataProvider : IEventDataProvider
    {
        private readonly Func<int, Task<IReadOnlyList<EventMeeting>>> _fn;

        public StubEventDataProvider(Func<int, Task<IReadOnlyList<EventMeeting>>> fn)
        {
            _fn = fn;
        }

        public List<int> YearsRequested { get; } = [];

        public Task<IReadOnlyList<EventMeeting>> GetRacesBySeasonAsync(int year)
        {
            YearsRequested.Add(year);
            return _fn(year);
        }
    }
}
