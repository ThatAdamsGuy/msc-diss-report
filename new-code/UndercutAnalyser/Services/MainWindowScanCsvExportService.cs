using System.Globalization;
using System.Text;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds scan CSV content from normalized scan rows using the shared export schema.
/// </summary>
public static class MainWindowScanCsvExportService
{
    /// <summary>
    /// Creates CSV text for scan rows, including header and escaped fields.
    /// </summary>
    public static string BuildCsv(IReadOnlyList<ScanRowData> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Lap,Attacking,Target,AttackerCompound,AttackerTyreAge,TargetCompound,TargetTyreAge,G0,GapTargetPitLapComplete,GapTargetOutLapComplete,GapBothDriversNormalLapComplete,DeltaGTargetPitLapComplete,Result");

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                row.DecisionLap.ToString(CultureInfo.InvariantCulture),
                MainWindowLogic.EscapeCsv(row.Attacker),
                MainWindowLogic.EscapeCsv(row.Target),
                MainWindowLogic.EscapeCsv(row.AttackerCompound),
                row.AttackerTyreAge.ToString(CultureInfo.InvariantCulture),
                MainWindowLogic.EscapeCsv(row.TargetCompound),
                row.TargetTyreAge.ToString(CultureInfo.InvariantCulture),
                MainWindowLogic.EscapeCsv(row.G0),
                MainWindowLogic.EscapeCsv(row.GapAtTargetPitLapComplete),
                MainWindowLogic.EscapeCsv(row.GapAtTargetOutLapComplete),
                MainWindowLogic.EscapeCsv(row.GapAtBothDriversNormalLapComplete),
                MainWindowLogic.EscapeCsv(row.DeltaGAtTargetPitLapComplete),
                MainWindowLogic.EscapeCsv(row.Result)));
        }

        return sb.ToString();
    }
}
