using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

public static class MainWindowScanExecutor
{
    /// <summary>
    /// Executes prediction requests for all scan contexts and returns table rows,
    /// converting per-context failures into error rows so one failure does not stop the batch.
    /// </summary>
    public static List<ScanRowData> Execute(
        IEnumerable<ScanExecutionContext> contexts,
        IPitSequencePredictor predictor)
    {
        var rows = new List<ScanRowData>();

        foreach (var context in contexts)
        {
            try
            {
                var prediction = predictor.Predict(context.Request);
                rows.Add(MainWindowScanRowFactory.CreateSuccess(
                    attacker: context.AttackerCode,
                    target: context.TargetCode,
                    decisionLap: context.DecisionLap,
                    attackerCompound: context.AttackerCompound,
                    attackerTyreAge: context.AttackerTyreAge,
                    targetCompound: context.TargetCompound,
                    targetTyreAge: context.TargetTyreAge,
                    g0: context.InitialGapSeconds,
                    prediction: prediction));
            }
            catch (Exception ex)
            {
                rows.Add(MainWindowScanRowFactory.CreateError(
                    attacker: context.AttackerCode,
                    target: context.TargetCode,
                    decisionLap: context.DecisionLap,
                    attackerCompound: context.AttackerCompound,
                    attackerTyreAge: context.AttackerTyreAge,
                    targetCompound: context.TargetCompound,
                    targetTyreAge: context.TargetTyreAge,
                    g0: context.InitialGapSeconds,
                    errorMessage: ex.Message));
            }
        }

        return rows;
    }
}
