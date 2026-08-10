using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser.Tests;

public sealed class UiClassTests
{
    [Fact]
    public void EventSelectorWindow_InitialStatus_NoEventsLoadedMessage()
    {
        RunInSta(() =>
        {
            var vm = new EventSelectorViewModel(new StubEventDataProvider());
            var window = new EventSelectorWindow(vm);
            try
            {
                var status = Assert.IsType<TextBlock>(window.FindName("StatusText"));
                Assert.Equal("No events loaded. Check your internet connection.", status.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void EventSelectorWindow_InitialStatus_ShowsAvailableEventCount()
    {
        RunInSta(() =>
        {
            var vm = new EventSelectorViewModel(new StubEventDataProvider());
            vm.Events.Add(new EventMeeting
            {
                MeetingOfficialName = "Test GP",
                CircuitShortName = "Test Circuit",
                Location = "Test City",
                Year = 2025,
                DateStart = DateTime.UtcNow.AddDays(-1),
                DateEnd = DateTime.UtcNow.AddDays(-1)
            });

            var window = new EventSelectorWindow(vm);
            try
            {
                var status = Assert.IsType<TextBlock>(window.FindName("StatusText"));
                Assert.Equal("1 events available", status.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void EventSelectorWindow_FilterText_UpdatesVisibleItems()
    {
        RunInSta(() =>
        {
            var vm = new EventSelectorViewModel(new StubEventDataProvider());
            vm.Events.Add(CreateMeeting("Test GP Alpha", "Alpha Circuit", "Alpha City"));
            vm.Events.Add(CreateMeeting("Test GP Beta", "Beta Circuit", "Beta City"));

            var window = new EventSelectorWindow(vm);
            try
            {
                var filterBox = Assert.IsType<TextBox>(window.FindName("FilterBox"));

                filterBox.Text = "Beta";

                var filtered = vm.EventsView.Cast<object>().OfType<EventMeeting>().ToList();
                var onlyItem = Assert.Single(filtered);
                Assert.Equal("Test GP Beta", onlyItem.MeetingOfficialName);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void EventSelectorWindow_SelectionChanged_EnablesOkButton()
    {
        RunInSta(() =>
        {
            var vm = new EventSelectorViewModel(new StubEventDataProvider());
            vm.Events.Add(CreateMeeting("Test GP", "Test Circuit", "Test City"));

            var window = new EventSelectorWindow(vm);
            try
            {
                var listView = Assert.IsType<ListView>(window.FindName("EventsListView"));
                var okButton = Assert.IsType<Button>(window.FindName("OkButton"));

                listView.SelectedItem = vm.Events[0];

                Assert.True(okButton.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void EventSelectorWindow_CancelButton_SetsDialogResultFalse()
    {
        RunInSta(() =>
        {
            var vm = new EventSelectorViewModel(new StubEventDataProvider());
            vm.Events.Add(CreateMeeting("Test GP", "Test Circuit", "Test City"));

            var window = new EventSelectorWindow(vm);
            var cancelButton = Assert.IsType<Button>(window.FindName("CancelButton"));
            window.Loaded += (_, _) => cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var result = window.ShowDialog();

            Assert.False(result);
        });
    }

    [Fact]
    public void EventSelectorWindow_DoubleClickSelectedItem_SetsDialogResultTrue()
    {
        RunInSta(() =>
        {
            var vm = new EventSelectorViewModel(new StubEventDataProvider());
            vm.Events.Add(CreateMeeting("Test GP", "Test Circuit", "Test City"));

            var window = new EventSelectorWindow(vm);
            var listView = Assert.IsType<ListView>(window.FindName("EventsListView"));

            window.Loaded += (_, _) =>
            {
                listView.SelectedItem = listView.Items[0];
                var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = Control.MouseDoubleClickEvent
                };
                listView.RaiseEvent(args);
            };

            var result = window.ShowDialog();

            Assert.True(result);
        });
    }

    [Fact]
    public void RawDataView_BuildsSortedLapColumnsWithDriverColumn()
    {
        RunInSta(() =>
        {
            var laps = new List<EventLap>
            {
                new() { DriverNumber = 1, LapNumber = 3, LapDuration = 93.0f },
                new() { DriverNumber = 1, LapNumber = 1, LapDuration = 91.0f },
                new() { DriverNumber = 1, LapNumber = 2, LapDuration = 92.0f }
            };
            var drivers = new List<Driver> { new() { DriverNumber = 1, Code = "DRV" } };
            var view = new RawDataView(laps, drivers, [], reference: null, fuelSecondsPer10Kg: 0.3, fuelKg: 110);
            try
            {
                var grid = GetPrivateField<DataGrid>(view, "_rawDataGrid");
                Assert.Equal(4, grid.Columns.Count);
                Assert.Equal("Driver", grid.Columns[0].Header?.ToString());
                Assert.Equal("1", grid.Columns[1].Header?.ToString());
                Assert.Equal("2", grid.Columns[2].Header?.ToString());
                Assert.Equal("3", grid.Columns[3].Header?.ToString());
            }
            finally
            {
                view.Close();
            }
        });
    }

    [Fact]
    public void RawDataView_ItemsIncludeReferenceRow()
    {
        RunInSta(() =>
        {
            var laps = new List<EventLap>
            {
                new() { DriverNumber = 1, LapNumber = 1, LapDuration = 90.0f },
                new() { DriverNumber = 2, LapNumber = 1, LapDuration = 91.0f }
            };
            var drivers = new List<Driver>
            {
                new() { DriverNumber = 1, Code = "A" },
                new() { DriverNumber = 2, Code = "B" }
            };

            var view = new RawDataView(laps, drivers, [], reference: null, fuelSecondsPer10Kg: 0.3, fuelKg: 110);
            try
            {
                var grid = GetPrivateField<DataGrid>(view, "_rawDataGrid");
                var rows = Assert.IsAssignableFrom<IEnumerable<DriverLapRow>>(grid.ItemsSource).ToList();

                Assert.Equal(3, rows.Count);
                Assert.Contains(rows, r => r.DriverName == "Reference Lap");
            }
            finally
            {
                view.Close();
            }
        });
    }

    [Fact]
    public void RawDataView_CumulativeMode_RecomputesLapCells()
    {
        RunInSta(() =>
        {
            var laps = new List<EventLap>
            {
                new() { DriverNumber = 1, LapNumber = 1, LapDuration = 90.0f },
                new() { DriverNumber = 1, LapNumber = 2, LapDuration = 91.0f }
            };
            var drivers = new List<Driver> { new() { DriverNumber = 1, Code = "DRV" } };

            var view = new RawDataView(laps, drivers, [], reference: null, fuelSecondsPer10Kg: 0.3, fuelKg: 110);
            try
            {
                var grid = GetPrivateField<DataGrid>(view, "_rawDataGrid");
                var modeCombo = GetPrivateField<ComboBox>(view, "_modeCombo");

                var individualRows = Assert.IsAssignableFrom<IEnumerable<DriverLapRow>>(grid.ItemsSource).ToList();
                var individualDriverRow = individualRows.First(r => r.DriverName != "Reference Lap");
                Assert.Equal("91.000", individualDriverRow["2"]);

                modeCombo.SelectedItem = "Cumulative";

                var cumulativeRows = Assert.IsAssignableFrom<IEnumerable<DriverLapRow>>(grid.ItemsSource).ToList();
                var cumulativeDriverRow = cumulativeRows.First(r => r.DriverName != "Reference Lap");
                Assert.Equal("181.000", cumulativeDriverRow["2"]);
            }
            finally
            {
                view.Close();
            }
        });
    }

    private static EventMeeting CreateMeeting(string meetingName, string circuitName, string location)
    {
        return new EventMeeting
        {
            MeetingOfficialName = meetingName,
            CircuitShortName = circuitName,
            Location = location,
            Year = 2025,
            DateStart = DateTime.UtcNow.AddDays(-1),
            DateEnd = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(instance));
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class StubEventDataProvider : IEventDataProvider
    {
        public Task<IReadOnlyList<EventMeeting>> GetRacesBySeasonAsync(int year) => Task.FromResult<IReadOnlyList<EventMeeting>>([]);
        public Task<IReadOnlyList<EventSession>> GetSessionsByMeetingKeyAsync(int meetingKey) => Task.FromResult<IReadOnlyList<EventSession>>([]);
        public Task<IReadOnlyList<EventLap>> GetLapsBySessionKeyAsync(int sessionKey) => Task.FromResult<IReadOnlyList<EventLap>>([]);
        public Task<IReadOnlyList<Driver>> GetDriversByMeetingAndSessionAsync(int meetingKey, int sessionKey) => Task.FromResult<IReadOnlyList<Driver>>([]);
        public Task<IReadOnlyList<RaceControlMessage>> GetRaceControlMessagesBySessionKeyAsync(int sessionKey) => Task.FromResult<IReadOnlyList<RaceControlMessage>>([]);
        public Task<IReadOnlyList<EventStint>> GetStintsBySessionKeyAsync(int sessionKey) => Task.FromResult<IReadOnlyList<EventStint>>([]);
    }
}
