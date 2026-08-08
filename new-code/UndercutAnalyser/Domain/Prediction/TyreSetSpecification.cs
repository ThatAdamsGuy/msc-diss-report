namespace UndercutAnalyser.Domain.Prediction
{
    /// <summary>
    /// Specifies the replacement tyre set that a driver will fit during their pit stop.
    /// InitialAgeLaps is 0 for a brand-new set, or positive for a used set (e.g. a set
    /// scrubbed in qualifying). Degradation on the out lap is calculated from this initial age.
    /// </summary>
    public sealed record TyreSetSpecification(TyreCompound Compound, int InitialAgeLaps)
    {
        /// <summary>A new (unused) tyre set of the given compound.</summary>
        public static TyreSetSpecification NewSet(TyreCompound compound) =>
            new(compound, InitialAgeLaps: 0);
    }
}
