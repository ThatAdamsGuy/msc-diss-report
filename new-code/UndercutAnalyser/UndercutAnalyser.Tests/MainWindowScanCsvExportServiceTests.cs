using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowScanCsvExportServiceTests
{
    [Fact]
    public void BuildCsv_IncludesHeaderAndRowProjectionInSchemaOrder()
    {
        var rows = new List<ScanRowData>
        {
            new(
                Attacker: "NOR",
                Target: "PIA",
                DecisionLap: 12,
                AttackerCompound: "SOFT",
                AttackerTyreAge: 9,
                TargetCompound: "MEDIUM",
                TargetTyreAge: 11,
                G0: "+1.250",
                GapAtTargetPitLapComplete: "+0.700 s",
                GapAtTargetOutLapComplete: "+0.550 s",
                GapAtBothDriversNormalLapComplete: "+0.420 s",
                DeltaGAtTargetPitLapComplete: "-0.550",
                Result: "Ahead")
        };

        var csv = MainWindowScanCsvExportService.BuildCsv(rows);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.None);
        Assert.StartsWith("Lap,Attacking,Target,AttackerCompound,AttackerTyreAge,TargetCompound,TargetTyreAge,G0,GapTargetPitLapComplete,GapTargetOutLapComplete,GapBothDriversNormalLapComplete,DeltaGTargetPitLapComplete,Result", lines[0]);
        Assert.Contains("12,NOR,PIA,SOFT,9,MEDIUM,11,+1.250,+0.700 s,+0.550 s,+0.420 s,-0.550,Ahead", lines[1]);
    }

    [Fact]
    public void BuildCsv_EscapesFieldsWithCommasQuotesAndNewlines()
    {
        var rows = new List<ScanRowData>
        {
            new(
                Attacker: "NOR,\"Lando\"",
                Target: "PIA",
                DecisionLap: 12,
                AttackerCompound: "SOFT",
                AttackerTyreAge: 9,
                TargetCompound: "MEDIUM",
                TargetTyreAge: 11,
                G0: "+1.250",
                GapAtTargetPitLapComplete: "+0.700 s",
                GapAtTargetOutLapComplete: "+0.550 s",
                GapAtBothDriversNormalLapComplete: "+0.420 s",
                DeltaGAtTargetPitLapComplete: "-0.550",
                Result: "Line1\nLine2")
        };

        var csv = MainWindowScanCsvExportService.BuildCsv(rows);

        Assert.Contains("\"NOR,\"\"Lando\"\"\"", csv);
        Assert.Contains("\"Line1", csv);
        Assert.Contains("Line2\"", csv);
    }
}
