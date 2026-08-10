using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowModelParameterBuilderServiceTests
{
    [Fact]
    public void Build_WithNoRows_UsesBaselineCompoundMappingsAndScalarSettings()
    {
        var result = WorkspaceWorkflowService.Build(
            tyreParameterRows: [],
            warmUpPenalty: 0.42,
            pitLaneLoss: 21.3,
            marginalThreshold: 0.31,
            applyAttackerTraffic: true,
            applyTargetTraffic: false,
            trafficPenalty: 0.55);

        Assert.Equal(0.0, result.GetCompoundOffset(TyreCompound.Soft), 10);
        Assert.Equal(0.1, result.GetCompoundOffset(TyreCompound.Medium), 10);
        Assert.Equal(0.2, result.GetCompoundOffset(TyreCompound.Hard), 10);

        Assert.Equal(0.10, result.GetDegradationRate(TyreCompound.Soft), 10);
        Assert.Equal(0.07, result.GetDegradationRate(TyreCompound.Medium), 10);
        Assert.Equal(0.04, result.GetDegradationRate(TyreCompound.Hard), 10);

        Assert.Equal(0.42, result.WarmUp.OutLapPenaltySeconds, 10);
        Assert.Equal(21.3, result.PitLaneLossSeconds, 10);
        Assert.Equal(0.31, result.MarginalThresholdSeconds, 10);
        Assert.True(result.Traffic.ApplyToAttacker);
        Assert.False(result.Traffic.ApplyToTarget);
        Assert.Equal(0.55, result.Traffic.PenaltySeconds, 10);
    }

    [Fact]
    public void Build_WithRows_OverridesMatchingCompounds_AndMapsUnknownCompound()
    {
        var rows = new List<TyreParameterRow>
        {
            new("SOFT", 0.23, 0.19, isEditable: false),
            new("MEDIUM", 0.34, 0.12, isEditable: true),
            new("HARD", 0.45, 0.09, isEditable: true),
            new("C5", 0.77, 0.66, isEditable: true)
        };

        var result = WorkspaceWorkflowService.Build(
            tyreParameterRows: rows,
            warmUpPenalty: 0.3,
            pitLaneLoss: 22.0,
            marginalThreshold: 0.25,
            applyAttackerTraffic: false,
            applyTargetTraffic: true,
            trafficPenalty: 0.4);

        Assert.Equal(0.23, result.GetCompoundOffset(TyreCompound.Soft), 10);
        Assert.Equal(0.34, result.GetCompoundOffset(TyreCompound.Medium), 10);
        Assert.Equal(0.45, result.GetCompoundOffset(TyreCompound.Hard), 10);

        Assert.Equal(0.19, result.GetDegradationRate(TyreCompound.Soft), 10);
        Assert.Equal(0.12, result.GetDegradationRate(TyreCompound.Medium), 10);
        Assert.Equal(0.09, result.GetDegradationRate(TyreCompound.Hard), 10);

        Assert.Equal(0.77, result.GetCompoundOffset(TyreCompound.Unknown), 10);
        Assert.Equal(0.66, result.GetDegradationRate(TyreCompound.Unknown), 10);
    }
}
