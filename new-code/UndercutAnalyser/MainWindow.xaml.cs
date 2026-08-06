using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Infrastructure;
using UndercutAnalyser.Services;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private int? _currentMeetingKey;
    private IReadOnlyList<EventLap> _currentLaps = Array.Empty<EventLap>();
    private IReadOnlyList<Driver> _currentDrivers = Array.Empty<Driver>();
    private double _fuelSecondsPer10Kg = 0.3;
    private double _fuelKg = 110;

    // TODO: move these to the Parameters menu view model and bind editable controls.
    public double FuelSecondsPer10Kg
    {
        get => _fuelSecondsPer10Kg;
        set => SetField(ref _fuelSecondsPer10Kg, value);
    }

    // TODO: move these to the Parameters menu view model and bind editable controls.
    public double FuelKg
    {
        get => _fuelKg;
        set => SetField(ref _fuelKg, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        var vm = new EventSelectorViewModel();
        DataContext = vm;

        Loaded += async (_, _) =>
        {
            var now = DateTime.UtcNow.Year;
            await vm.LoadAsync(2023, now).ConfigureAwait(false);
        };

        EventButton.Click += async (_, _) =>
        {
            var dlg = new EventSelectorWindow(vm);
            var res = dlg.ShowDialog();
            if (res != true || vm.SelectedEvent is null)
            {
                return;
            }

            SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year})";

            try
            {
                var meetingKey = vm.SelectedEvent.MeetingKey;
                if (_currentMeetingKey == meetingKey)
                {
                    return;
                }

                _currentMeetingKey = meetingKey;
                _currentLaps = Array.Empty<EventLap>();
                _currentDrivers = Array.Empty<Driver>();
                RawDataButton.IsEnabled = false;

                SelectedEventDisplay.Text = "Loading race data...";

                var client = new OpenF1RaceDataClient();
                var sessions = await client.GetSessionsByMeetingKeyAsync(meetingKey).ConfigureAwait(false);
                var raceSession = sessions.FirstOrDefault(s =>
                    string.Equals(s.SessionName, "Race", StringComparison.OrdinalIgnoreCase) ||
                    (s.SessionName?.IndexOf("Race", StringComparison.OrdinalIgnoreCase) >= 0));

                if (raceSession is null)
                {
                    raceSession = sessions.Count > 0 ? sessions[0] : null;
                }

                if (raceSession is not null)
                {
                    var sessionKey = raceSession.SessionKey;
                    var laps = await client.GetLapsBySessionKeyAsync(sessionKey).ConfigureAwait(false);
                    var drivers = await client.GetDriversByMeetingAndSessionAsync(meetingKey, sessionKey).ConfigureAwait(false);
                    var raceControlMessages = await client.GetRaceControlMessagesBySessionKeyAsync(sessionKey).ConfigureAwait(false);

                    var reference = ReferenceLapTimeCalculator.Calculate(
                        laps,
                        raceControlMessages,
                        FuelSecondsPer10Kg,
                        FuelKg);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _currentLaps = laps;
                        _currentDrivers = drivers;
                        RawDataButton.IsEnabled = true;
                        SelectedEventDisplay.Text =
                            $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) - Rows: {reference.TotalLapRows}, Session laps: {reference.MaxSessionLapNumber}, Drivers: {drivers.Count}, " +
                            $"Reference sum (fuel-adjusted): {reference.SumLapTimeSeconds:F3}s, avg: {(reference.AverageLapTimeSeconds.HasValue ? reference.AverageLapTimeSeconds.Value.ToString("F3") : "N/A")}s, " +
                            $"Fuel/lap: {reference.FuelEffectPerLapSeconds:F3}s, Clean laps: {reference.IncludedLaps}";
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedEventDisplay.Text = $"{vm.SelectedEvent.RaceName} ({vm.SelectedEvent.Year}) - No race session found";
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RawDataButton.IsEnabled = false;
                    SelectedEventDisplay.Text = $"Error loading race data: {ex.Message}";
                });
            }
        };

        RawDataButton.Click += (_, _) =>
        {
            if (_currentMeetingKey is null)
            {
                return;
            }

            var rawDataView = new RawDataView(_currentLaps, _currentDrivers)
            {
                Owner = this
            };
            rawDataView.Show();
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
