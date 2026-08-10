using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.ViewModels;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds lap-model parameters from editable tyre rows and scalar scenario settings.
/// </summary>
public static class MainWindowModelParameterBuilderService
{
    /// <summary>
    /// Produces the model parameters used by scan and prediction flows.
    /// </summary>
    public static LapModelParameters Build(
        IReadOnlyList<TyreParameterRow> tyreParameterRows,
        double warmUpPenalty,
        double pitLaneLoss,
        double marginalThreshold,
        bool applyAttackerTraffic,
        bool applyTargetTraffic,
        double trafficPenalty)
    {
        var offsets = new Dictionary<TyreCompound, double>
        {
            [TyreCompound.Soft] = 0.0,
            [TyreCompound.Medium] = 0.1,
            [TyreCompound.Hard] = 0.2
        };

        var degRates = new Dictionary<TyreCompound, double>
        {
            [TyreCompound.Soft] = 0.10,
            [TyreCompound.Medium] = 0.07,
            [TyreCompound.Hard] = 0.04
        };

        foreach (var row in tyreParameterRows)
        {
            var compound = TyreCompoundParser.FromOpenF1String(row.Compound);
            offsets[compound] = row.PaceOffset;
            degRates[compound] = row.DegradationRate;
        }

        return new LapModelParameters(
            CompoundOffsetsSeconds: offsets,
            DegradationRatesSecondsPerLap: degRates,
            WarmUp: new WarmUpModelParameters(warmUpPenalty),
            PitLaneLossSeconds: pitLaneLoss,
            MarginalThresholdSeconds: marginalThreshold,
            Traffic: new TrafficModelParameters(applyAttackerTraffic, applyTargetTraffic, trafficPenalty));
    }
}
