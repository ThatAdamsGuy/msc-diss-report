using System;

namespace UndercutAnalyser.Domain.Prediction
{
    public enum TyreCompound
    {
        Soft,
        Medium,
        Hard,
        Intermediate,
        Wet,
        Unknown
    }

    public static class TyreCompoundParser
    {
        /// <summary>
        /// Parses an OpenF1 compound string (e.g. "SOFT", "MEDIUM", "HARD") into a
        /// <see cref="TyreCompound"/> value. Returns <see cref="TyreCompound.Unknown"/>
        /// for unrecognised or null inputs.
        /// </summary>
        public static TyreCompound FromOpenF1String(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TyreCompound.Unknown;
            }

            return value.Trim().ToUpperInvariant() switch
            {
                "SOFT"         => TyreCompound.Soft,
                "MEDIUM"       => TyreCompound.Medium,
                "HARD"         => TyreCompound.Hard,
                "INTERMEDIATE" => TyreCompound.Intermediate,
                "WET"          => TyreCompound.Wet,
                _              => TyreCompound.Unknown
            };
        }

        /// <summary>
        /// Returns a short display label for the compound (e.g. "S", "M", "H").
        /// </summary>
        public static string ToShortLabel(this TyreCompound compound) => compound switch
        {
            TyreCompound.Soft         => "S",
            TyreCompound.Medium       => "M",
            TyreCompound.Hard         => "H",
            TyreCompound.Intermediate => "I",
            TyreCompound.Wet          => "W",
            _                         => "?"
        };

        /// <summary>
        /// Returns true if the compound is a valid dry compound for undercut prediction.
        /// Wet and intermediate compounds are explicitly unsupported in the base model.
        /// </summary>
        public static bool IsDryCompound(this TyreCompound compound) =>
            compound is TyreCompound.Soft or TyreCompound.Medium or TyreCompound.Hard;
    }
}
