using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser;

public sealed class RawDataView : Window
{
    private readonly DataGrid _rawDataGrid;

    public RawDataView(IReadOnlyList<EventLap> laps, IReadOnlyList<Driver> drivers)
    {
        Title = "Raw Lap Data";
        Height = 700;
        Width = 1200;

        var grid = new Grid
        {
            Margin = new Thickness(8)
        };

        _rawDataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.All,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        grid.Children.Add(_rawDataGrid);
        Content = grid;

        BuildGrid(laps, drivers);
    }

    private void BuildGrid(IReadOnlyList<EventLap> laps, IReadOnlyList<Driver> drivers)
    {
        var lapNumbers = laps
            .Select(l => l.LapNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToArray();

        var driverByNumber = drivers
            .GroupBy(d => d.DriverNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var lapsByDriver = laps
            .GroupBy(l => l.DriverNumber)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.LapNumber)
                    .ToDictionary(x => x.Key, x => x.First()));

        var rows = new List<DriverLapRow>();

        foreach (var driverNumber in lapsByDriver.Keys.OrderBy(n => n))
        {
            var driverName = driverByNumber.TryGetValue(driverNumber, out var driver)
                ? string.IsNullOrWhiteSpace(driver.FullName) ? driver.Code : driver.FullName
                : $"Driver {driverNumber}";

            rows.Add(new DriverLapRow(driverName, lapsByDriver[driverNumber]));
        }

        _rawDataGrid.Columns.Clear();
        _rawDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Driver",
            Binding = new Binding(nameof(DriverLapRow.DriverName)),
            Width = 220
        });

        foreach (var lapNumber in lapNumbers)
        {
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new Binding($"[{lapNumber}]"));
            textFactory.SetBinding(FrameworkElement.ToolTipProperty, new Binding($"[detail:{lapNumber}]"));
            textFactory.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));

            var template = new DataTemplate
            {
                VisualTree = textFactory
            };

            _rawDataGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = lapNumber.ToString(),
                CellTemplate = template,
                Width = 80
            });
        }

        _rawDataGrid.ItemsSource = rows;
    }
}
