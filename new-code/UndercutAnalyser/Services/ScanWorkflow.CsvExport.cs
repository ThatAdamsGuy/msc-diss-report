using System.Globalization;
using System.Text;

namespace UndercutAnalyser.Services;

/// <summary>
/// Builds scan CSV content from normalized scan rows using the shared export schema.
/// </summary>
public static partial class ScanWorkflowService
{
    /// <summary>
    /// Creates CSV text for scan rows, including header and escaped fields.
    /// </summary>
    public static string BuildCsv(IReadOnlyList<ScanRowData> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Lap,Attacking,Target,AttackerCompound,AttackerTyreAge,TargetCompound,TargetTyreAge,G0,GapTargetPitLapComplete,GapTargetOutLapComplete,GapBothDriversNormalLapComplete,DeltaGTargetPitLapComplete,DeltaGTargetOutLapComplete,DeltaGBothDriversNormalLapComplete,Result");

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                row.DecisionLap.ToString(CultureInfo.InvariantCulture),
                WorkspaceWorkflowService.EscapeCsv(row.Attacker),
                WorkspaceWorkflowService.EscapeCsv(row.Target),
                WorkspaceWorkflowService.EscapeCsv(row.AttackerCompound),
                row.AttackerTyreAge.ToString(CultureInfo.InvariantCulture),
                WorkspaceWorkflowService.EscapeCsv(row.TargetCompound),
                row.TargetTyreAge.ToString(CultureInfo.InvariantCulture),
                WorkspaceWorkflowService.EscapeCsv(row.G0),
                WorkspaceWorkflowService.EscapeCsv(row.GapAtTargetPitLapComplete),
                WorkspaceWorkflowService.EscapeCsv(row.GapAtTargetOutLapComplete),
                WorkspaceWorkflowService.EscapeCsv(row.GapAtBothDriversNormalLapComplete),
                WorkspaceWorkflowService.EscapeCsv(row.DeltaGAtTargetPitLapComplete),
                WorkspaceWorkflowService.EscapeCsv(row.DeltaGAtTargetOutLapComplete),
                WorkspaceWorkflowService.EscapeCsv(row.DeltaGAtBothDriversNormalLapComplete),
                WorkspaceWorkflowService.EscapeCsv(row.Result)));
        }

        return sb.ToString();
    }
}
