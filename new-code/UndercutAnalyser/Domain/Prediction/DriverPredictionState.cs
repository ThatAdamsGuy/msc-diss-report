namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// The observed state of a single driver at the decision point.
    /// This is the starting condition from which the lap-by-lap prediction is built.
    ///
    /// ReferencePaceSeconds is driver-specific. It represents the driver's clean lap time
    /// baseline excluding all tyre-related effects (compound offset, degradation, warm-up).
    /// It can be auto-derived from clean historic laps or entered manually by the engineer.
    ///
    /// CurrentTyreAgeLaps is the tyre age at the START of the decision lap (lap n).
    /// The pit lap prediction uses this age and then the replacement tyre begins at
    /// TyreSetSpecification.InitialAgeLaps on the out lap.
    /// </summary>
    public sealed record DriverPredictionState(
        string DriverCode,
        string DisplayName,
        int Position,
        double ReferencePaceSeconds,
        TyreCompound CurrentCompound,
        int CurrentTyreAgeLaps);
}
