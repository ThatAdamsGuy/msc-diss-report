using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowScanOrchestrationServiceTests
{
    [Fact]
    public void Run_ReturnsSuccessRow_ForValidSingleCandidate()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 30f, LapDuration = 90f },
            new() { DriverNumber = 4, LapNumber = 10, DateStart = t0, DurationSector1 = 21f, DurationSector2 = 30f, LapDuration = 91f }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 4, Code = "NOR" },
            new() { DriverNumber = 81, Code = "PIA" }
        };

        (string compound, int age) TyreResolver(int driverNumber, int lapNumber)
            => (driverNumber == 4 ? "SOFT" : "MEDIUM", 8);

        var input = new MainWindowScanOrchestrationInput(
            DriverNumbers: [4],
            MinTyreAge: 5,
            EventName: "Test Event",
            AttackerReplacementTyre: new TyreSetSpecification(TyreCompound.Hard, 0),
            TargetReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 0),
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault(),
            SafetyCarWindows: [],
            Laps: laps,
            Drivers: drivers,
            TyreStateResolver: TyreResolver);

        var rows = ScanWorkflowService.Run(input);

        var row = Assert.Single(rows);
        Assert.Equal("NOR", row.Attacker);
        Assert.Equal("PIA", row.Target);
        Assert.Equal(10, row.DecisionLap);
    }

    [Fact]
    public void Run_ReturnsEmpty_WhenNoCandidatesPassEligibility()
    {
        var t0 = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var laps = new List<EventLap>
        {
            new() { DriverNumber = 81, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 30f, LapDuration = 90f },
            new() { DriverNumber = 4, LapNumber = 10, DateStart = t0, DurationSector1 = 20f, DurationSector2 = 30f, LapDuration = 90f }
        };

        var drivers = new List<Driver>
        {
            new() { DriverNumber = 4, Code = "NOR" },
            new() { DriverNumber = 81, Code = "PIA" }
        };

        (string compound, int age) TyreResolver(int driverNumber, int lapNumber)
            => ("SOFT", 1);

        var input = new MainWindowScanOrchestrationInput(
            DriverNumbers: [4],
            MinTyreAge: 5,
            EventName: "Test Event",
            AttackerReplacementTyre: new TyreSetSpecification(TyreCompound.Hard, 0),
            TargetReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 0),
            TargetResponseLaps: 1,
            ModelParameters: LapModelParameters.CreateDefault(),
            SafetyCarWindows: [],
            Laps: laps,
            Drivers: drivers,
            TyreStateResolver: TyreResolver);

        var rows = ScanWorkflowService.Run(input);

        Assert.Empty(rows);
    }
}
