using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Services;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser;

/// <summary>
/// Tabular inspector for raw lap/stint data, including a reference-lap row and
/// optional cumulative view for quick engineering diagnostics.
/// </summary>
public sealed class RawDataView : Window
{
    private readonly DataGrid _rawDataGrid;
    private readonly TextBlock _extraInfoText;
    private readonly ComboBox _modeCombo;

    private readonly IReadOnlyList<EventLap> _laps;
    private readonly IReadOnlyList<Driver> _drivers;
    private readonly IReadOnlyList<EventStint> _stints;
    private readonly ReferenceLapTimeResult? _reference;
    private readonly double _fuelSecondsPer10Kg;
    private readonly double _fuelKg;

    private int[] _lapNumbers = Array.Empty<int>();
    private bool _cumulativeMode;

    /// <summary>
    /// Creates the raw-data window and initialises grid layout and row content.
    /// </summary>
    public RawDataView(
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<Driver> drivers,
        IReadOnlyList<EventStint> stints,
        ReferenceLapTimeResult? reference,
        double fuelSecondsPer10Kg,
        double fuelKg)
    {
        _laps = laps;
        _drivers = drivers;
        _stints = stints;
        _reference = reference;
        _fuelSecondsPer10Kg = fuelSecondsPer10Kg;
        _fuelKg = fuelKg;

        Title = "Raw Lap Data";
        Height = 760;
        Width = 1320;

        var root = new Grid
        {
            Margin = new Thickness(8)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var extraInfoBorder = new Border
        {
            BorderBrush = Brushes.DarkGray,
            BorderThickness = new Thickness(1),
            Background = Brushes.WhiteSmoke,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8)
        };

        _extraInfoText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        extraInfoBorder.Child = _extraInfoText;
        Grid.SetRow(extraInfoBorder, 0);
        root.Children.Add(extraInfoBorder);

        var modePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        modePanel.Children.Add(new TextBlock
        {
            Text = "View:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        _modeCombo = new ComboBox
        {
            Width = 140,
            ItemsSource = new[] { "Individual", "Cumulative" },
            SelectedIndex = 0
        };
        _modeCombo.SelectionChanged += (_, _) =>
        {
            _cumulativeMode = string.Equals(_modeCombo.SelectedItem as string, "Cumulative", StringComparison.OrdinalIgnoreCase);
            RefreshRows();
        };
        modePanel.Children.Add(_modeCombo);

        Grid.SetRow(modePanel, 1);
        root.Children.Add(modePanel);

        _rawDataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.All,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalGridLinesBrush = Brushes.Black,
            VerticalGridLinesBrush = Brushes.Black,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(_rawDataGrid, 2);
        root.Children.Add(_rawDataGrid);

        Content = root;

        BuildGrid();
        RefreshRows();
        UpdateExtraInfo();
    }

    /// <summary>
    /// Builds dynamic lap-number columns for the raw data grid.
    /// </summary>
    private void BuildGrid()
    {
        _lapNumbers = _laps
            .Select(l => l.LapNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToArray();

        _rawDataGrid.Columns.Clear();
        _rawDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Driver",
            Binding = new Binding(nameof(DriverLapRow.DriverName)),
            Width = 220
        });

        foreach (var lapNumber in _lapNumbers)
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty, new Binding($"[bg:{lapNumber}]"));
            borderFactory.SetBinding(Border.BorderBrushProperty, new Binding($"[border:{lapNumber}]"));
            borderFactory.SetBinding(Border.BorderThicknessProperty, new Binding($"[thickness:{lapNumber}]"));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(2));

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new Binding($"[{lapNumber}]"));
            textFactory.SetBinding(FrameworkElement.ToolTipProperty, new Binding($"[detail:{lapNumber}]"));
            textFactory.SetValue(TextBlock.PaddingProperty, new Thickness(2));
            textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(textFactory);

            var template = new DataTemplate
            {
                VisualTree = borderFactory
            };

            _rawDataGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = lapNumber.ToString(),
                CellTemplate = template,
                Width = 82
            });
        }
    }

    /// <summary>
    /// Recomputes grid rows from current lap/stint data and selected view mode.
    /// </summary>
    private void RefreshRows()
    {
        var driverByNumber = _drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var lapsByDriver = _laps
            .GroupBy(l => l.DriverNumber)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.LapNumber)
                    .ToDictionary(x => x.Key, x => x.First()));

        var stintsByDriver = _stints
            .GroupBy(s => s.DriverNumber)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.LapStart).ToList());

        var rows = new List<DriverLapRow>();
        var orderedLapList = _lapNumbers.ToList();

        foreach (var driverNumber in lapsByDriver.Keys.OrderBy(n => n))
        {
            var driverName = driverByNumber.TryGetValue(driverNumber, out var driver)
                ? string.IsNullOrWhiteSpace(driver.FullName) ? driver.Code : driver.FullName
                : $"Driver {driverNumber}";

            var driverLaps = lapsByDriver[driverNumber];
            var pitLaps = BuildPitLaps(driverLaps);
            var compounds = BuildCompoundsByLap(driverNumber, stintsByDriver);

            rows.Add(new DriverLapRow(
                driverName,
                driverLaps,
                compounds,
                pitLaps,
                orderedLapList,
                _cumulativeMode));
        }

        rows.Add(new DriverLapRow(
            "Reference Lap",
            new Dictionary<int, EventLap>(),
            new Dictionary<int, string>(),
            new HashSet<int>(),
            orderedLapList,
            _cumulativeMode,
            isReferenceRow: true,
            referenceLapSeconds: _reference?.AverageLapTimeSeconds));

        _rawDataGrid.ItemsSource = rows;
    }

    /// <summary>
    /// Derives pit-in laps from pit-out markers for cell highlighting.
    /// </summary>
    private HashSet<int> BuildPitLaps(Dictionary<int, EventLap> driverLaps)
    {
        var pitLaps = new HashSet<int>();

        foreach (var lap in driverLaps.Values.Where(l => l.IsPitOutLap))
        {
            if (lap.LapNumber > 1)
            {
                pitLaps.Add(lap.LapNumber - 1);
            }
        }

        return pitLaps;
    }

    /// <summary>
    /// Expands stint ranges into lap-to-compound mapping for border colouring.
    /// </summary>
    private Dictionary<int, string> BuildCompoundsByLap(int driverNumber, Dictionary<int, List<EventStint>> stintsByDriver)
    {
        var compoundByLap = new Dictionary<int, string>();
        if (!stintsByDriver.TryGetValue(driverNumber, out var driverStints))
        {
            return compoundByLap;
        }

        foreach (var stint in driverStints)
        {
            if (stint.LapStart <= 0 || stint.LapEnd <= 0 || stint.LapEnd < stint.LapStart)
            {
                continue;
            }

            for (var lap = stint.LapStart; lap <= stint.LapEnd; lap++)
            {
                compoundByLap[lap] = stint.Compound;
            }
        }

        return compoundByLap;
    }

    /// <summary>
    /// Updates summary text describing reference, fuel assumptions, and data counts.
    /// </summary>
    private void UpdateExtraInfo()
    {
        var referenceAvg = _reference?.AverageLapTimeSeconds;
        var referenceAvgText = referenceAvg.HasValue ? referenceAvg.Value.ToString("F3") : "N/A";

        _extraInfoText.Text =
            $"Extra info  |  Rows: {_reference?.TotalLapRows ?? _laps.Count}  |  Session laps: {_reference?.MaxSessionLapNumber ?? _lapNumbers.Length}  |  " +
            $"Reference avg: {referenceAvgText}s  |  Fuel: {_fuelKg:F1} kg  |  Seconds per 10kg: {_fuelSecondsPer10Kg:F3}  |  " +
            $"Fuel/lap: {_reference?.FuelEffectPerLapSeconds.ToString("F3") ?? "N/A"}s  |  Clean laps: {_reference?.IncludedLaps.ToString() ?? "N/A"}  |  " +
            $"Excluded (pit-out/pit-in/SC): {_reference?.ExcludedPitOutLaps.ToString() ?? "N/A"}/{_reference?.ExcludedPitInLaps.ToString() ?? "N/A"}/{_reference?.ExcludedSafetyCarLaps.ToString() ?? "N/A"}  |  Stints: {_stints.Count}";
    }
}
