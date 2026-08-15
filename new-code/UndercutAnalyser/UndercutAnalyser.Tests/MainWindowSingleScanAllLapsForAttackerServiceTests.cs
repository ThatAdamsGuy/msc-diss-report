using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowSingleScanAllLapsForAttackerServiceTests
{
    [Fact]
    public void RunAllLapsForAttacker_SkipsLeaderLap_AndCollectsRowsForLapsWithAheadTarget()
    {
        var input = CreateInput(
            laps: [
                CreateLap(1, 4, 0, 90f),
                CreateLap(1, 81, 2, 91f),
                CreateLap(2, 81, 92, 90f),
                CreateLap(2, 4, 94, 91f)
            ],
            decisionLaps: [1, 2]);

        var rows = SingleScanWorkflowService.RunAllLapsForAttacker(
            input,
            resolveGapText: (_, _, _) => "1.100",
            resolveTyreState: (_, _) => ("MEDIUM", 6));

        Assert.Single(rows);
        Assert.Equal("NOR", rows[0].Attacker);
        Assert.Equal("PIA", rows[0].Target);
    }

    [Fact]
    public void RunAllLapsForAttacker_TargetCanChangeBetweenLaps()
    {
        var input = CreateInput(
            laps: [
                CreateLap(1, 1, 0, 90f),
                CreateLap(1, 4, 2, 91f),
                CreateLap(1, 81, 3, 92f),
                CreateLap(2, 81, 92, 90f),
                CreateLap(2, 4, 94, 91f),
                CreateLap(2, 1, 96, 92f)
            ],
            decisionLaps: [1, 2]);

        var rows = SingleScanWorkflowService.RunAllLapsForAttacker(
            input,
            resolveGapText: (_, _, _) => "0.950",
            resolveTyreState: (_, _) => ("SOFT", 4));

        Assert.Equal(2, rows.Count);
        Assert.Equal("1", rows[0].Target);
        Assert.Equal("PIA", rows[1].Target);
    }

    [Fact]
    public void RunAllLapsForAttacker_SkipsWhenTargetDriverDataMissingOrGapMissing()
    {
        var input = CreateInput(
            laps: [
                CreateLap(1, 1, 0, 90f),
                CreateLap(1, 4, 2, 91f),
                CreateLap(2, 1, 92, 90f),
                CreateLap(2, 4, 94, 91f)
            ],
            decisionLaps: [1, 2],
            drivers: [new Driver { DriverNumber = 4, Code = "NOR", BroadcastName = "L NORRIS" }]);

        var rows = SingleScanWorkflowService.RunAllLapsForAttacker(
            input,
            resolveGapText: (_, _, lap) => lap == 2 ? string.Empty : "1.200",
            resolveTyreState: (_, _) => ("HARD", 7));

        Assert.Empty(rows);
    }

    private static MainWindowSingleScanAllLapsForAttackerInput CreateInput(
        IReadOnlyList<EventLap> laps,
        IReadOnlyList<int> decisionLaps,
        IReadOnlyList<Driver>? drivers = null)
    {
        var resolvedDrivers = drivers ??
        [
            new Driver { DriverNumber = 4, Code = "NOR", BroadcastName = "L NORRIS" },
            new Driver { DriverNumber = 81, Code = "PIA", BroadcastName = "O PIASTRI" },
            new Driver { DriverNumber = 1, Code = "1", BroadcastName = "M VERSTAPPEN" }
        ];

        return new MainWindowSingleScanAllLapsForAttackerInput(
            Attacker: new PredictionSelection(4, "NOR", "Lando Norris"),
            DecisionLapNumbers: decisionLaps,
            EventName: "Test Event",
            AttackerPaceOverrideText: "90.2",
            TargetPaceOverrideText: "90.4",
            DerivedReferencePaceByDriver: new Dictionary<int, double>
            {
                [4] = 90.2,
                [81] = 90.4,
                [1] = 90.1
            },
            AttackerReplacementCompoundText: "HARD",
            AttackerReplacementAgeText: "1",
            TargetReplacementCompoundText: "SOFT",
            TargetReplacementAgeText: "2",
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault(),
            Laps: laps,
            Drivers: resolvedDrivers);
    }

    private static EventLap CreateLap(int lapNumber, int driverNumber, int startOffsetSeconds, float lapDuration)
    {
        var start = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddSeconds(startOffsetSeconds);
        return new EventLap
        {
            DriverNumber = driverNumber,
            LapNumber = lapNumber,
            DateStart = start,
            LapDuration = lapDuration,
            DurationSector1 = 30f,
            DurationSector2 = 30f,
            DurationSector3 = lapDuration - 60f
        };
    }
}
