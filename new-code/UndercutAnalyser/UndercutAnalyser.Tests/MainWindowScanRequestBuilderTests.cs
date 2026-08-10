using UndercutAnalyser.Domain.Models;
using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowScanRequestBuilderTests
{
    [Fact]
    public void ResolveDriverCode_UsesDriverCodeOrFallsBackToNumber()
    {
        var drivers = new Dictionary<int, Driver>
        {
            [4] = new() { DriverNumber = 4, Code = "NOR" },
            [7] = new() { DriverNumber = 7, Code = "" }
        };

        Assert.Equal("NOR", ScanWorkflowService.ResolveDriverCode(drivers, 4));
        Assert.Equal("7", ScanWorkflowService.ResolveDriverCode(drivers, 7));
        Assert.Equal("99", ScanWorkflowService.ResolveDriverCode(drivers, 99));
    }

    [Fact]
    public void BuildExecutionContext_MapsCandidateIntoRequestAndMetadata()
    {
        var candidate = new ScanCandidate(
            AttackerNumber: 4,
            TargetNumber: 12,
            DecisionLap: 20,
            AttackerPosition: 3,
            InitialGapSeconds: 1.234);

        var drivers = new Dictionary<int, Driver>
        {
            [4] = new() { DriverNumber = 4, Code = "NOR" },
            [12] = new() { DriverNumber = 12, Code = "ANT" }
        };

        var paces = new Dictionary<int, double>
        {
            [4] = 91.2,
            [12] = 91.6
        };

        var model = LapModelParameters.CreateDefault();

        (string compound, int age) TyreResolver(int driver, int lap) =>
            driver == 4 ? ("soft", 8) : ("medium", 10);

        var context = ScanWorkflowService.BuildExecutionContext(
            candidate: candidate,
            driversByNumber: drivers,
            referencePaceByDriver: paces,
            tyreStateResolver: TyreResolver,
            eventName: "Test Event",
            attackerReplacementTyre: new TyreSetSpecification(TyreCompound.Hard, 0),
            targetReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 2),
            targetResponseLaps: 1,
            modelParameters: model);

        Assert.Equal("NOR", context.AttackerCode);
        Assert.Equal("ANT", context.TargetCode);
        Assert.Equal(91.2, context.AttackerReferencePace, 10);
        Assert.Equal(91.6, context.TargetReferencePace, 10);
        Assert.Equal("soft", context.AttackerCompound);
        Assert.Equal(8, context.AttackerTyreAge);
        Assert.Equal("medium", context.TargetCompound);
        Assert.Equal(10, context.TargetTyreAge);

        var request = context.Request;
        Assert.Equal("Test Event", request.EventName);
        Assert.Equal(20, request.DecisionLap);
        Assert.Equal(1.234, request.InitialAttackerGapToTargetSeconds, 10);
        Assert.Equal("NOR", request.Attacker.DriverCode);
        Assert.Equal(TyreCompound.Soft, request.Attacker.CurrentCompound);
        Assert.Equal("ANT", request.Target.DriverCode);
        Assert.Equal(TyreCompound.Medium, request.Target.CurrentCompound);
        Assert.Equal(1, request.TargetResponseLaps);
        Assert.Same(model, request.ModelParameters);
    }
}
