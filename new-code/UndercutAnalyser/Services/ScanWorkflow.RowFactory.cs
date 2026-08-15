using System.Globalization;
using UndercutAnalyser.Domain.Prediction;

namespace UndercutAnalyser.Services;

/// <summary>
/// Normalized scan row values used for grid display and CSV export.
/// </summary>
public sealed record ScanRowData(
    string Attacker,
    string Target,
    int DecisionLap,
    string AttackerCompound,
    int AttackerTyreAge,
    string TargetCompound,
    int TargetTyreAge,
    string G0,
    string GapAtTargetPitLapComplete,
    string GapAtTargetOutLapComplete,
    string GapAtBothDriversNormalLapComplete,
    string DeltaGAtTargetPitLapComplete,
    string DeltaGAtTargetOutLapComplete,
    string DeltaGAtBothDriversNormalLapComplete,
    string Result);

public static partial class ScanWorkflowService
{
    /// <summary>
    /// Creates a scan result row for a successful prediction, including formatted gaps and classification label.
    /// </summary>
    public static ScanRowData CreateSuccess(
        string attacker,
        string target,
        int decisionLap,
        string attackerCompound,
        int attackerTyreAge,
        string targetCompound,
        int targetTyreAge,
        double g0,
        PredictionResult prediction)
    {
        return new ScanRowData(
            Attacker: attacker,
            Target: target,
            DecisionLap: decisionLap,
            AttackerCompound: attackerCompound,
            AttackerTyreAge: attackerTyreAge,
            TargetCompound: targetCompound,
            TargetTyreAge: targetTyreAge,
            G0: FormatSigned(g0),
            GapAtTargetPitLapComplete: FormatGap(prediction.GapAtTargetPitLapCompleteSeconds),
            GapAtTargetOutLapComplete: FormatGap(prediction.GapAtTargetOutLapCompleteSeconds),
            GapAtBothDriversNormalLapComplete: FormatGap(prediction.GapAtBothDriversNormalLapCompleteSeconds),
            DeltaGAtTargetPitLapComplete: FormatSigned(prediction.DeltaGAtTargetPitLapCompleteSeconds),
            DeltaGAtTargetOutLapComplete: FormatSigned(prediction.DeltaGAtTargetOutLapCompleteSeconds),
            DeltaGAtBothDriversNormalLapComplete: FormatSigned(prediction.DeltaGAtBothDriversNormalLapCompleteSeconds),
            Result: WorkspaceWorkflowService.ToResultLabel(prediction.Classification));
    }

    /// <summary>
    /// Creates a scan result row for a failed prediction attempt, preserving context fields and inserting placeholders for model outputs.
    /// </summary>
    public static ScanRowData CreateError(
        string attacker,
        string target,
        int decisionLap,
        string attackerCompound,
        int attackerTyreAge,
        string targetCompound,
        int targetTyreAge,
        double g0,
        string errorMessage)
    {
        return new ScanRowData(
            Attacker: attacker,
            Target: target,
            DecisionLap: decisionLap,
            AttackerCompound: attackerCompound,
            AttackerTyreAge: attackerTyreAge,
            TargetCompound: targetCompound,
            TargetTyreAge: targetTyreAge,
            G0: FormatSigned(g0),
            GapAtTargetPitLapComplete: "-",
            GapAtTargetOutLapComplete: "-",
            GapAtBothDriversNormalLapComplete: "-",
            DeltaGAtTargetPitLapComplete: "-",
            DeltaGAtTargetOutLapComplete: "-",
            DeltaGAtBothDriversNormalLapComplete: "-",
            Result: $"Error: {errorMessage}");
    }

    /// <summary>
    /// Formats a signed gap value with fixed three-decimal precision and seconds suffix.
    /// </summary>
    private static string FormatGap(double gap)
    {
        return $"{gap:+0.000;-0.000;0.000} s";
    }

    /// <summary>
    /// Formats a signed numeric value with fixed three-decimal precision and explicit sign.
    /// </summary>
    private static string FormatSigned(double value)
    {
        return value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);
    }
}
