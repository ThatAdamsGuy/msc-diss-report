using System.Windows;
using System.Windows.Media;
using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser.Tests;

public sealed class DriverLapRowTests
{
    #region Individual and cumulative cell value behavior
    // These tests exist to protect lap text/value generation rules for both normal and cumulative
    // modes, including missing-lap and reference-row handling.

    [Fact]
    public void Indexer_IndividualMode_ReturnsFormattedLapTextOrEmpty()
    {
        var row = CreateRow(
            cumulativeMode: false,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [1] = Lap(1, 90.1234f),
                [2] = Lap(2, null)
            });

        Assert.Equal("90.123", row["1"]);
        Assert.Equal(string.Empty, row["2"]);
        Assert.Equal(string.Empty, row["3"]);
        Assert.Equal(string.Empty, row["not-a-lap"]);
        Assert.Same(Brushes.Transparent, row["bg:not-a-lap"]);
        Assert.Same(Brushes.Transparent, row["border:not-a-lap"]);
        Assert.Equal(new Thickness(0), row["thickness:not-a-lap"]);
    }

    [Fact]
    public void Indexer_CumulativeMode_SumsKnownLapsUpToRequestedLap()
    {
        var row = CreateRow(
            cumulativeMode: true,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [1] = Lap(1, 90f),
                [2] = Lap(2, 91.5f),
                [3] = Lap(3, null),
                [4] = Lap(4, 95f)
            },
            orderedLapNumbers: [1, 2, 3, 4]);

        Assert.Equal("181.500", row["2"]);
        Assert.Equal("181.500", row["3"]);
        Assert.Equal("276.500", row["4"]);
    }

    [Fact]
    public void Indexer_CumulativeMode_NonReferenceRowWithoutRequestedLap_ReturnsEmpty()
    {
        var row = CreateRow(
            cumulativeMode: true,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [1] = Lap(1, 90f)
            },
            orderedLapNumbers: [1, 2, 3]);

        Assert.Equal(string.Empty, row["2"]);
    }

    [Fact]
    public void ReferenceRow_UsesReferenceValueForAllLaps_IndividualAndCumulative()
    {
        var individual = CreateRow(cumulativeMode: false, isReferenceRow: true, referenceLapSeconds: 100.0, orderedLapNumbers: [1, 2, 3]);
        var cumulative = CreateRow(cumulativeMode: true, isReferenceRow: true, referenceLapSeconds: 100.0, orderedLapNumbers: [1, 2, 3]);

        Assert.Equal("100.000", individual["2"]);
        Assert.Equal("300.000", cumulative["3"]);
    }

    [Fact]
    public void Indexer_UsesSectorOneMetric_InIndividualAndCumulativeModes()
    {
        var individual = CreateRow(
            cumulativeMode: false,
            metric: RawDataValueMetric.SectorOneTime,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [1] = new EventLap { LapNumber = 1, LapDuration = 90.0f, DurationSector1 = 30.100f },
                [2] = new EventLap { LapNumber = 2, LapDuration = 91.0f, DurationSector1 = 31.200f }
            },
            orderedLapNumbers: [1, 2]);

        var cumulative = CreateRow(
            cumulativeMode: true,
            metric: RawDataValueMetric.SectorOneTime,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [1] = new EventLap { LapNumber = 1, LapDuration = 90.0f, DurationSector1 = 30.100f },
                [2] = new EventLap { LapNumber = 2, LapDuration = 91.0f, DurationSector1 = 31.200f }
            },
            orderedLapNumbers: [1, 2]);

        Assert.Equal("31.200", individual["2"]);
        Assert.Equal("121.200", cumulative["2"]);
    }

    [Fact]
    public void Indexer_UsesSectorTwoMetric_InIndividualAndCumulativeModes()
    {
        var individual = CreateRow(
            cumulativeMode: false,
            metric: RawDataValueMetric.SectorTwoTime,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [1] = new EventLap { LapNumber = 1, LapDuration = 90.0f, DurationSector1 = 30.0f, DurationSector2 = 31.0f },
                [2] = new EventLap { LapNumber = 2, LapDuration = 91.0f, DurationSector1 = 31.0f, DurationSector2 = 32.0f }
            },
            orderedLapNumbers: [1, 2]);

        var cumulative = CreateRow(
            cumulativeMode: true,
            metric: RawDataValueMetric.SectorTwoTime,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [1] = new EventLap { LapNumber = 1, LapDuration = 90.0f, DurationSector1 = 30.0f, DurationSector2 = 31.0f },
                [2] = new EventLap { LapNumber = 2, LapDuration = 91.0f, DurationSector1 = 31.0f, DurationSector2 = 32.0f }
            },
            orderedLapNumbers: [1, 2]);

        Assert.Equal("63.000", individual["2"]);
        Assert.Equal("153.000", cumulative["2"]);
    }

    #endregion

    #region Detail, background, border, and thickness metadata
    // These tests exist to lock tooltip/detail payloads and visual metadata routing used by
    // raw-data cell templates.

    [Fact]
    public void DetailKey_ForDriverLap_IncludesJsonCompoundAndPitFlags()
    {
        var row = CreateRow(
            cumulativeMode: false,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [7] = Lap(7, 92.2f)
            },
            compoundByLap: new Dictionary<int, string> { [7] = "SOFT" },
            pitLaps: [7]);

        var detail = Assert.IsType<string>(row["detail:7"]);

        Assert.Contains("\"lap_number\": 7", detail, StringComparison.Ordinal);
        Assert.Contains("Matched compound: SOFT", detail, StringComparison.Ordinal);
        Assert.Contains("Inferred pit lap: Yes", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailKey_ForDriverLap_UsesUnknownCompoundFallback()
    {
        var row = CreateRow(
            cumulativeMode: false,
            lapsByNumber: new Dictionary<int, EventLap>
            {
                [7] = Lap(7, 92.2f)
            },
            compoundByLap: new Dictionary<int, string> { [7] = "   " },
            pitLaps: []);

        var detail = Assert.IsType<string>(row["detail:7"]);

        Assert.Contains("Matched compound: Unknown", detail, StringComparison.Ordinal);
        Assert.Contains("Inferred pit lap: No", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailKey_ForReferenceRow_ReturnsReferenceSummary()
    {
        var row = CreateRow(cumulativeMode: false, isReferenceRow: true, referenceLapSeconds: 99.8, orderedLapNumbers: [1, 2, 3]);

        var detail = Assert.IsType<string>(row["detail:2"]);

        Assert.Equal("Reference Lap\nLap 2: 99.800 s", detail);
    }

    [Fact]
    public void DetailKey_ForReferenceRow_WithNonLapMetric_ReturnsReferenceLapOnly()
    {
        var row = CreateRow(
            cumulativeMode: false,
            metric: RawDataValueMetric.SectorOneTime,
            isReferenceRow: true,
            referenceLapSeconds: 99.8,
            orderedLapNumbers: [1, 2, 3]);

        var detail = Assert.IsType<string>(row["detail:2"]);

        Assert.Equal("Reference Lap", detail);
    }

    [Fact]
    public void BackgroundKey_UsesReferenceAndPitStyling()
    {
        var referenceRow = CreateRow(cumulativeMode: false, isReferenceRow: true, referenceLapSeconds: 100, orderedLapNumbers: [1]);
        var driverRow = CreateRow(cumulativeMode: false, pitLaps: [3]);

        Assert.Same(Brushes.Gainsboro, referenceRow["bg:1"]);
        Assert.Same(Brushes.Orange, driverRow["bg:3"]);
        Assert.Same(Brushes.Transparent, driverRow["bg:2"]);
    }

    [Theory]
    [InlineData("SOFT", nameof(Brushes.Red))]
    [InlineData("Medium", nameof(Brushes.Yellow))]
    [InlineData("hard", nameof(Brushes.White))]
    [InlineData("UNKNOWN", nameof(Brushes.Black))]
    [InlineData("", nameof(Brushes.Black))]
    [InlineData("INTERMEDIATE", nameof(Brushes.Transparent))]
    public void BorderKey_MapsCompoundToExpectedBrush(string compound, string expectedBrushName)
    {
        var row = CreateRow(cumulativeMode: false, compoundByLap: new Dictionary<int, string> { [5] = compound });

        var brush = Assert.IsType<SolidColorBrush>(row["border:5"]);
        var expected = (SolidColorBrush)typeof(Brushes).GetProperty(expectedBrushName)!.GetValue(null)!;

        Assert.Equal(expected.Color, brush.Color);
    }

    [Fact]
    public void ThicknessKey_ReturnsTwoWhenCompoundExistsElseZero()
    {
        var row = CreateRow(cumulativeMode: false, compoundByLap: new Dictionary<int, string> { [1] = "SOFT" });

        Assert.Equal(new Thickness(2), row["thickness:1"]);
        Assert.Equal(new Thickness(0), row["thickness:2"]);
    }

    #endregion

    private static DriverLapRow CreateRow(
        bool cumulativeMode,
        Dictionary<int, EventLap>? lapsByNumber = null,
        Dictionary<int, string>? compoundByLap = null,
        HashSet<int>? pitLaps = null,
        IReadOnlyList<int>? orderedLapNumbers = null,
        RawDataValueMetric metric = RawDataValueMetric.LapTime,
        bool isReferenceRow = false,
        double? referenceLapSeconds = null) =>
        new(
            driverName: "Driver",
            lapsByNumber: lapsByNumber ?? new Dictionary<int, EventLap>(),
            compoundByLap: compoundByLap ?? new Dictionary<int, string>(),
            pitLaps: pitLaps ?? [],
            orderedLapNumbers: orderedLapNumbers ?? [1, 2, 3],
            cumulativeMode: cumulativeMode,
            metric: metric,
            isReferenceRow: isReferenceRow,
            referenceLapSeconds: referenceLapSeconds);

    private static EventLap Lap(int lapNumber, float? duration) =>
        new()
        {
            LapNumber = lapNumber,
            LapDuration = duration
        };
}
