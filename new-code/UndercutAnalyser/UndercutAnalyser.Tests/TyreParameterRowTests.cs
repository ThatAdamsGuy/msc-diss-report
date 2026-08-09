using System.ComponentModel;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser.Tests;

public sealed class TyreParameterRowTests
{
    #region Value normalization and property semantics
    // These tests exist to protect rounding and null-normalization behavior used by parameter
    // editors so persisted values stay predictable.

    [Fact]
    public void Constructor_SetsInputsAndAppliesRounding()
    {
        var row = new TyreParameterRow("Medium", paceOffset: 0.567, degradationRate: 0.0349, isEditable: true, isDegradationEditable: false);

        Assert.Equal("Medium", row.Compound);
        Assert.True(row.IsEditable);
        Assert.False(row.IsDegradationEditable);
        Assert.Equal(0.57, row.PaceOffset, 10);
        Assert.Equal(0.03, row.DegradationRate, 10);
    }

    [Fact]
    public void PaceOffset_AndDegradationRate_RoundToTwoDecimals()
    {
        var row = new TyreParameterRow("Hard", paceOffset: 0, degradationRate: 0, isEditable: true);

        row.PaceOffset = 1.235;
        row.DegradationRate = 0.126;

        Assert.Equal(1.24, row.PaceOffset, 10);
        Assert.Equal(0.13, row.DegradationRate, 10);
    }

    [Fact]
    public void MinLaps_AndMaxLaps_NormalizeNullToEmpty()
    {
        var row = new TyreParameterRow("Soft", paceOffset: 0, degradationRate: 0, isEditable: false);

        row.MinLaps = null!;
        row.MaxLaps = null!;

        Assert.Equal(string.Empty, row.MinLaps);
        Assert.Equal(string.Empty, row.MaxLaps);
    }

    #endregion

    #region Clone and notification behavior
    // These tests exist to verify safe copying across windows and deterministic notification
    // signals for WPF binding refresh.

    [Fact]
    public void Clone_CreatesIndependentCopyWithSameValues()
    {
        var original = new TyreParameterRow("Medium", 0.5, 0.07, isEditable: true, isDegradationEditable: true)
        {
            MinLaps = "10",
            MaxLaps = "20"
        };

        var clone = original.Clone();
        clone.MinLaps = "11";
        clone.PaceOffset = 0.8;

        Assert.Equal("Medium", clone.Compound);
        Assert.Equal("10", original.MinLaps);
        Assert.Equal("11", clone.MinLaps);
        Assert.Equal(0.5, original.PaceOffset, 10);
        Assert.Equal(0.8, clone.PaceOffset, 10);
    }

    [Fact]
    public void SettingDifferentValue_RaisesPropertyChanged()
    {
        var row = new TyreParameterRow("Hard", 1.0, 0.04, isEditable: true);
        var raised = new List<string>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        row.PaceOffset = 1.3;
        row.DegradationRate = 0.09;
        row.MinLaps = "12";
        row.MaxLaps = "22";

        Assert.Contains(nameof(TyreParameterRow.PaceOffset), raised);
        Assert.Contains(nameof(TyreParameterRow.DegradationRate), raised);
        Assert.Contains(nameof(TyreParameterRow.MinLaps), raised);
        Assert.Contains(nameof(TyreParameterRow.MaxLaps), raised);
    }

    [Fact]
    public void SettingSameValue_DoesNotRaisePropertyChanged()
    {
        var row = new TyreParameterRow("Hard", 1.0, 0.04, isEditable: true)
        {
            MinLaps = "10",
            MaxLaps = "20"
        };

        var raised = new List<string>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        row.PaceOffset = 1.0;
        row.DegradationRate = 0.04;
        row.MinLaps = "10";
        row.MaxLaps = "20";

        Assert.Empty(raised);
    }

    #endregion
}
