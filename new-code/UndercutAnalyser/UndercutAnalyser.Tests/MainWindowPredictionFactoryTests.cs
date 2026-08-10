using UndercutAnalyser.Domain.Prediction;
using UndercutAnalyser.Services;

namespace UndercutAnalyser.Tests;

public sealed class MainWindowPredictionFactoryTests
{
    [Fact]
    public void CreatePredictionRequest_MapsInputsToRequestFields_AndParsesCompounds()
    {
        var model = LapModelParameters.CreateDefault();

        var request = MainWindowPredictionFactory.CreatePredictionRequest(
            eventName: "Test Event",
            decisionLap: 12,
            initialGapSeconds: 1.234,
            attacker: new DriverScenarioInput("NOR", "Lando", 2, 91.2, "soft", 14),
            target: new DriverScenarioInput("ANT", "Kimi", 1, 91.5, "MEDIUM", 10),
            attackerReplacementTyre: new TyreSetSpecification(TyreCompound.Hard, 0),
            targetReplacementTyre: new TyreSetSpecification(TyreCompound.Soft, 3),
            targetResponseLaps: 2,
            modelParameters: model);

        Assert.Equal("Test Event", request.EventName);
        Assert.Equal(12, request.DecisionLap);
        Assert.Equal(1.234, request.InitialAttackerGapToTargetSeconds, 10);

        Assert.Equal("NOR", request.Attacker.DriverCode);
        Assert.Equal("Lando", request.Attacker.DisplayName);
        Assert.Equal(2, request.Attacker.Position);
        Assert.Equal(91.2, request.Attacker.ReferencePaceSeconds, 10);
        Assert.Equal(TyreCompound.Soft, request.Attacker.CurrentCompound);
        Assert.Equal(14, request.Attacker.CurrentTyreAgeLaps);

        Assert.Equal("ANT", request.Target.DriverCode);
        Assert.Equal("Kimi", request.Target.DisplayName);
        Assert.Equal(1, request.Target.Position);
        Assert.Equal(91.5, request.Target.ReferencePaceSeconds, 10);
        Assert.Equal(TyreCompound.Medium, request.Target.CurrentCompound);
        Assert.Equal(10, request.Target.CurrentTyreAgeLaps);

        Assert.Equal(2, request.TargetResponseLaps);
        Assert.Same(model, request.ModelParameters);
    }

    [Theory]
    [InlineData(UndercutClassification.PredictedAhead, "Ahead")]
    [InlineData(UndercutClassification.PredictedMarginal, "Marginal")]
    [InlineData(UndercutClassification.PredictedBehind, "Behind")]
    public void ToResultLabel_MapsClassificationToUiLabel(UndercutClassification input, string expected)
    {
        var actual = MainWindowPredictionFactory.ToResultLabel(input);
        Assert.Equal(expected, actual);
    }
}
